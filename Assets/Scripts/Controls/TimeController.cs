using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeController : MonoBehaviour
{
	private static TimeController instance = null;

	[SerializeField] private float[] timeScales = { };
	private UpdateController updateController = null;
	private InfoController infoController = null;
	private GravityWellController gravityWellController = null;
	private int currentTimeScaleIndex = 0;

	public static TimeController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		instance = this;
	}

	private void Start()
	{
		updateController = UpdateController.GetInstance();
		infoController = InfoController.GetInstance();
		gravityWellController = GravityWellController.GetInstance();
	}

	public void SetTimeScale(int timeScaleIndex)
	{
		if(currentTimeScaleIndex == 0 && timeScaleIndex != 0)
		{
			if(gravityWellController.AreCollisionsNearby() || !gravityWellController.OnRailAll())
			{
				infoController.AddMessage("Can not speed up Time");
				return;
			}

			updateController.ToggleUnityPhysics(false);
		}
		else if(timeScaleIndex == 0)
		{
			updateController.ToggleUnityPhysics(true);
		}

		currentTimeScaleIndex = timeScaleIndex;
		Time.timeScale = timeScales[timeScaleIndex];

		infoController.AddMessage("Set Time Speedup to " + timeScales[timeScaleIndex].ToString("F0") + "x");
	}

	public void TogglePause(bool pause)
	{
		if(pause)
		{
			Time.timeScale = 0.0f;
		}
		else
		{
			Time.timeScale = timeScales[currentTimeScaleIndex];
		}
	}

	public bool IsScaled()
	{
		return currentTimeScaleIndex != 0;
	}
}
