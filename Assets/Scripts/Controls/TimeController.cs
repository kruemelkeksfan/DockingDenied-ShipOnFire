using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Why handle Update() and FixedUpdate() in this Class?
// 1) Update()-/FixedUpdate()-Calls have a lot of Overhead
// 2) Avoiding FixedUpdate allows to shutdown Physics completely or change fixed Timesteps without glitching Unity Physics
// 3) Emulating Coroutines allows for timeScales greater than the 100x Unity-Limit
public class TimeController : MonoBehaviour
{
	private static TimeController instance = null;

	[SerializeField] private float[] timeScales = { };
	[SerializeField] private float fixedUpdateInterval = 0.02f;
	private InfoController infoController = null;
	private GravityWellController gravityWellController = null;
	private int currentTimeScaleIndex = 0;
	private HashSet<IUpdateListener> updateListeners = null;
	private HashSet<IFixedUpdateListener> fixedUpdateListeners = null;
	private double gameTime = 0.0;
	private double fixedTime = 0.0;
	private float timeScale = 1.0f;

	public static TimeController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		updateListeners = new HashSet<IUpdateListener>();
		fixedUpdateListeners = new HashSet<IFixedUpdateListener>();

		instance = this;
	}

	private void Start()
	{
		infoController = InfoController.GetInstance();
		gravityWellController = GravityWellController.GetInstance();
	}

	private void Update()
	{
		gameTime += Time.deltaTime * timeScale;

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
				Physics2D.Simulate(fixedUpdateInterval);
			}
		}

		foreach(IUpdateListener listener in updateListeners)
		{
			listener.UpdateNotify();
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
			infoController.AddMessage("Game is paused and can not be sped up");
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
