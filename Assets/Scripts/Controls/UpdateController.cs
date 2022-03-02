using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Why this Class?
// 1) Update()-/FixedUpdate()-Calls have a lot of Overhead
// 2) Avoiding FixedUpdate allows to shutdown Physics completely or change fixed Timesteps without glitching Unity Physics
public class UpdateController : MonoBehaviour
{
	private static UpdateController instance = null;
	private static WaitForSecondsRealtime waitForFixedUpdate = null;

	[SerializeField] private float fixedUpdateInterval = 0.02f;
	private HashSet<IUpdateListener> updateListeners = null;
	private HashSet<IFixedUpdateListener> fixedUpdateListeners = null;
	private float fixedTime = 0.0f;
	private bool unityPhysicsEnabled = true;

	public static UpdateController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		waitForFixedUpdate = new WaitForSecondsRealtime(fixedUpdateInterval);

		updateListeners = new HashSet<IUpdateListener>();
		fixedUpdateListeners = new HashSet<IFixedUpdateListener>();

		instance = this;
	}

	private void Update()
	{
		foreach(IUpdateListener listener in updateListeners)
		{
			listener.UpdateNotify();
		}
	}

	private void FixedUpdate()
	{
		// I'm not sure, if FixedUpdate() is called when Physics2D.autoSimulation is false
		if(unityPhysicsEnabled)
		{
			fixedTime += fixedUpdateInterval;

			foreach(IFixedUpdateListener listener in fixedUpdateListeners)
			{
				listener.FixedUpdateNotify();
			}

			Physics2D.Simulate(fixedUpdateInterval);
		}
	}

	private IEnumerator FixedUpdateCoroutine()
	{
		while(true)
		{
			yield return waitForFixedUpdate;

			fixedTime += fixedUpdateInterval * Time.timeScale;

			foreach(IFixedUpdateListener listener in fixedUpdateListeners)
			{
				listener.FixedUpdateNotify();
			}
		}
	}

	public void ToggleUnityPhysics(bool unityPhysicsEnabled)
	{
		this.unityPhysicsEnabled = unityPhysicsEnabled;

		if(unityPhysicsEnabled)
		{
			StopAllCoroutines();
		}
		else
		{
			StartCoroutine(FixedUpdateCoroutine());
		}
	}

	// Only use together with GravityWellController.OnRailAll() too prevent on-railed Gravity Objects from operating with wrong startTimes
	public void ResetFixedTime()
	{
		fixedTime = 0.0f;
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

	public float GetFixedDeltaTime()
	{
		return fixedUpdateInterval * Time.timeScale;
	}

	public float GetFixedTime()
	{
		return fixedTime;
	}
}
