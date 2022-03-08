using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Why handle Update() and FixedUpdate() in this Class?
// 1) Update()-/FixedUpdate()-Calls have a lot of Overhead
// 2) Avoiding FixedUpdate allows to shutdown Physics completely or change fixed Timesteps without glitching Unity Physics
// 3) Emulating Coroutines allows for timeScales greater than the 100x Unity-Limit
public class TimeController : MonoBehaviour
{
	public class Coroutine
	{
		public double timestamp = 0.0;
		public readonly IEnumerator<float> callback = null;
		public readonly bool isRealTime = false;

		public Coroutine(double timestamp, IEnumerator<float> callback, bool isRealTime)
		{
			this.timestamp = timestamp;
			this.callback = callback;
			this.isRealTime = isRealTime;
		}
	}

	private static TimeController instance = null;

	[SerializeField] private float[] timeScales = { };
	[SerializeField] private float fixedUpdateInterval = 0.02f;
	private InfoController infoController = null;
	private GravityWellController gravityWellController = null;
	private int currentTimeScaleIndex = 0;
	private HashSet<IUpdateListener> updateListeners = null;
	private HashSet<IFixedUpdateListener> fixedUpdateListeners = null;
	private double gameTime = 0.0;
	private float deltaTime = 0.0f;
	private double fixedTime = 0.0;
	private float timeScale = 1.0f;
	private HashSet<Coroutine> gameTimeCoroutines = null;
	private HashSet<Coroutine> realTimeCoroutines = null;
	private double nextGameTimestamp = double.MaxValue;
	private double nextRealTimestamp = double.MaxValue;
	private List<Coroutine> iteratableCoroutines = null;

	public static TimeController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		updateListeners = new HashSet<IUpdateListener>();
		fixedUpdateListeners = new HashSet<IFixedUpdateListener>();

		gameTimeCoroutines = new HashSet<Coroutine>();
		realTimeCoroutines = new HashSet<Coroutine>();
		iteratableCoroutines = new List<Coroutine>();

		instance = this;
	}

	private void Start()
	{
		infoController = InfoController.GetInstance();
		gravityWellController = GravityWellController.GetInstance();
	}

	private void Update()
	{
		deltaTime = Time.deltaTime * timeScale;
		gameTime += deltaTime;

		if(timeScale > MathUtil.EPSILON)
		{
			float fixedDeltaTime = GetFixedDeltaTime();
			while(fixedTime + fixedDeltaTime < gameTime)
			{
				fixedTime += fixedDeltaTime;

				foreach(IFixedUpdateListener listener in fixedUpdateListeners)
				{
					listener.FixedUpdateNotify();
				}

				if(timeScale <= 1.0f + MathUtil.EPSILON)
				{
					Physics2D.Simulate(fixedDeltaTime);
				}
			}
		}

		foreach(IUpdateListener listener in updateListeners)
		{
			listener.UpdateNotify();
		}

		if(gameTime >= nextGameTimestamp)
		{
			CallCoroutines(ref nextGameTimestamp, gameTime, gameTimeCoroutines);
		}
		float realTimeSinceStartup = Time.realtimeSinceStartup;
		if(realTimeSinceStartup >= nextRealTimestamp)
		{
			CallCoroutines(ref nextRealTimestamp, realTimeSinceStartup, realTimeCoroutines);
		}
	}

	public Coroutine StartCoroutine(IEnumerator<float> callback, bool isRealTime)
	{
		if(callback.MoveNext())
		{
			if(isRealTime)
			{
				return StartCoroutine(ref nextRealTimestamp, Time.realtimeSinceStartup, realTimeCoroutines, callback, isRealTime);
			}
			else
			{
				return StartCoroutine(ref nextGameTimestamp, gameTime, gameTimeCoroutines, callback, isRealTime);
			}
		}

		return null;
	}

	public bool StopCoroutine(Coroutine coroutine)
	{
		if(coroutine.isRealTime)
		{
			return realTimeCoroutines.Remove(coroutine);
		}
		else
		{
			return gameTimeCoroutines.Remove(coroutine);
		}
	}

	private Coroutine StartCoroutine(ref double nextTimestamp, double time, HashSet<Coroutine> coroutines, IEnumerator<float> callback, bool isRealTime)
	{
		// If callback returns a Value <= 0.0, wait a minimum Amount of Time (== until next Frame)
		double newTimestamp = time + (callback.Current > 0.0 ? callback.Current : MathUtil.EPSILON);
		Coroutine coroutine = new Coroutine(newTimestamp, callback, isRealTime);
		coroutines.Add(coroutine);

		if(newTimestamp < nextTimestamp)
		{
			nextTimestamp = newTimestamp;
		}

		return coroutine;
	}

	private void CallCoroutines(ref double nextTimestamp, double time, HashSet<Coroutine> coroutines)
	{
		if(nextTimestamp <= time)
		{
			nextTimestamp = double.MaxValue;
		}
		iteratableCoroutines.Clear();
		// Copy beforehand and iterate over Copy to enable starting/stopping other Coroutines from within Coroutines without throwing ConcurrentModificationExceptions
		iteratableCoroutines.AddRange(coroutines);
		foreach(Coroutine coroutine in iteratableCoroutines)
		{
			if(time >= coroutine.timestamp)
			{
				if(coroutine.callback.MoveNext())
				{
					// If callback returns a Value <= 0.0, wait a minimum Amount of Time (== until next Frame)
					double newTimestamp = (coroutine.callback.Current > 0.0) ? (coroutine.timestamp + coroutine.callback.Current) : (time + MathUtil.EPSILON);
					coroutine.timestamp = newTimestamp;
				}
				else
				{
					// Defuse timestamp, so that it does not screw up nextTimestamp
					coroutine.timestamp = double.MaxValue;

					coroutine.callback.Dispose();
					coroutines.Remove(coroutine);
				}
			}

			if(coroutine.timestamp < nextTimestamp)
			{
				nextTimestamp = coroutine.timestamp;
			}
		}
	}

	public void SetTimeScale(int timeScaleIndex)
	{
		if(timeScale > MathUtil.EPSILON)
		{
			if(currentTimeScaleIndex == 0 && timeScaleIndex != 0)
			{
				if(gravityWellController.AreCollisionsNearby() || !gravityWellController.OnRailAll())
				{
					infoController.AddMessage("Can not speed up Time");
					return;
				}
			}

			currentTimeScaleIndex = timeScaleIndex;
			timeScale = timeScales[timeScaleIndex];

			infoController.AddMessage("Set Time Speedup to " + timeScales[timeScaleIndex].ToString("F0") + "x");
		}
		else
		{
			infoController.AddMessage("Game is paused and can not be sped up or slowed down");
		}
	}

	public void TogglePause(bool pause)
	{
		if(pause)
		{
			timeScale = 0.0f;
		}
		else
		{
			timeScale = timeScales[currentTimeScaleIndex];
		}
	}

	public void AddUpdateListener(IUpdateListener listener)
	{
		updateListeners.Add(listener);
	}

	public void RemoveUpdateListener(IUpdateListener listener)
	{
		updateListeners.Remove(listener);
	}

	public void AddFixedUpdateListener(IFixedUpdateListener listener)
	{
		fixedUpdateListeners.Add(listener);
	}

	public void RemoveFixedUpdateListener(IFixedUpdateListener listener)
	{
		fixedUpdateListeners.Remove(listener);
	}

	public bool IsScaled()
	{
		return currentTimeScaleIndex != 0;
	}

	public float GetDeltaTime()
	{
		return deltaTime;
	}

	public float GetFixedDeltaTime()
	{
		return fixedUpdateInterval * timeScale;
	}

	public double GetTime()
	{
		return gameTime;
	}

	public double GetFixedTime()
	{
		return fixedTime;
	}
}
