using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeController : MonoBehaviour, IListener
{
	private static TimeController instance = null;

	[SerializeField] private float[] timeScales = { };
	private InfoController infoController = null;
	private GravityWellController gravityWellController = null;
	private float startFixedDeltaTime = 1.0f;
	private int currentTimeScaleIndex = 0;
	private Transform localPlayerMainTransform = null;
	private float unrailDistance = 0.0f;
	private int collisionLayerMask = 0;

	public static TimeController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		startFixedDeltaTime = Time.fixedDeltaTime;

		instance = this;
	}

	private void Start()
	{
		infoController = InfoController.GetInstance();
		gravityWellController = GravityWellController.GetInstance();

		unrailDistance = Mathf.Sqrt(gravityWellController.GetSqrUnrailDistance());
		collisionLayerMask = LayerMask.GetMask("Spacecraft", "Asteroids");

		SpacecraftManager.GetInstance().AddSpacecraftChangeListener(this);
		Notify();
	}

	public void Notify()
	{
		localPlayerMainTransform = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft().GetTransform();
	}

	public void SetTimeScale(int timeScaleIndex)
	{
		if(currentTimeScaleIndex == 0 && timeScaleIndex != 0)
		{
			if(!CheckCollisions() || !gravityWellController.OnRailAll())
			{
				infoController.AddMessage("Can not speed up Time");
				return;
			}
		}

		currentTimeScaleIndex = timeScaleIndex;
		Time.timeScale = timeScales[timeScaleIndex];
		Time.fixedDeltaTime = startFixedDeltaTime * timeScales[timeScaleIndex];

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

	private bool CheckCollisions()
	{
		foreach(Collider2D collider in Physics2D.OverlapCircleAll(localPlayerMainTransform.position, unrailDistance, collisionLayerMask))
		{
			if(!collider.isTrigger)
			{
				float radius = collider.gameObject.GetComponent<GravityObjectController>().GetSqrColliderRadius();
				if(Physics2D.OverlapCircleAll(collider.bounds.center, radius, collisionLayerMask).Length > 1)
				{
					infoController.AddMessage("Some nearby Objects are dangerously close to each other");
					return false;
				}
			}
		}

		return true;
	}
}
