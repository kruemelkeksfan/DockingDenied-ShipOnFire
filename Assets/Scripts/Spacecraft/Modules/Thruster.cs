﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Thruster : Module
{
	[Tooltip("Energy Consumption at full Throttle in kW.")]
	[SerializeField] private float energyConsumption = 160.0f;
	[SerializeField] private float thrust = 1.0f;
	private Transform spacecraftTransform = null;
	private new Rigidbody2D rigidbody = null;
	private GravityWellController gravityWellController = null;
	private InventoryController inventoryController = null;
	private Capacitor capacitor = null;
	private Vector2 thrustVector = Vector2.zero;
	private float throttle = 0.0f;
	private ParticleSystem thrustParticles = null;
	private ParticleSystem.MainModule thrustParticlesMain = new ParticleSystem.MainModule();
	private Vector3 initialParticleSize = Vector3.zero;

	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position, false, true);

		rigidbody = gameObject.GetComponentInParent<Rigidbody2D>();
		spacecraftTransform = spacecraft.transform;
		thrustVector = (transform.localRotation * Vector2.up) * thrust;
		spacecraft.AddThruster(this);

		gravityWellController = GravityWellController.GetInstance();
		inventoryController = spacecraft.GetInventoryController();

		thrustParticles = gameObject.GetComponentInChildren<ParticleSystem>();
		thrustParticlesMain = thrustParticles.main;
		initialParticleSize = new Vector3(thrustParticlesMain.startSizeXMultiplier, thrustParticlesMain.startSizeYMultiplier, thrustParticlesMain.startSizeZMultiplier);

		capacitor = new Capacitor();
		componentSlots.Add(GoodManager.ComponentType.Capacitor, capacitor);
		inventoryController.AddEnergyConsumer(capacitor);

		InstallComponent("Capacitor [crude]");
	}

	public override void Deconstruct()
	{
		inventoryController.RemoveEnergyConsumer(capacitor);
		spacecraft.RemoveThruster(this);

		base.Deconstruct();
	}

	public override void FixedUpdateNotify()
	{
		// Don't apply Thrust during a Frame in which the Origin shifted,
		// because the Physics freak out when moving transform.position while Forces are being applied
		// TODO: Check for Origin Shift in Spacecraft (instead of here) to avoid unnecessary Method Calls
		if(constructed && throttle > MathUtil.EPSILON && !gravityWellController.IsOriginShifted())
		{
			float finalThrottle = throttle * capacitor.DischargePartial(energyConsumption * throttle * timeController.GetFixedDeltaTime());
			rigidbody.AddForceAtPosition(spacecraftTransform.rotation * thrustVector * finalThrottle * timeController.GetFixedDeltaTime(), transform.position, ForceMode2D.Impulse);

			thrustParticlesMain.startSizeXMultiplier = initialParticleSize.x * finalThrottle;
			thrustParticlesMain.startSizeYMultiplier = initialParticleSize.y * finalThrottle;
			thrustParticlesMain.startSizeZMultiplier = initialParticleSize.z * finalThrottle;
		}
	}

	public Vector2 GetThrustVector()
	{
		return thrustVector;
	}

	public bool SetThrottle(float throttle)
	{
		if(constructed && capacitor.GetCharge() > 0.0f && throttle > 0.0f)
		{
			if(this.throttle <= 0.0f)
			{
				thrustParticlesMain.startSizeXMultiplier = initialParticleSize.x * throttle;
				thrustParticlesMain.startSizeYMultiplier = initialParticleSize.y * throttle;
				thrustParticlesMain.startSizeZMultiplier = initialParticleSize.z * throttle;

				thrustParticles.Play();
			}

			this.throttle = Mathf.Clamp(throttle, 0.0f, 1.0f);

			return true;
		}
		else
		{
			thrustParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

			this.throttle = 0.0f;

			return false;
		}
	}
}
