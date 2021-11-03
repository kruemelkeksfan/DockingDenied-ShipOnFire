using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Thruster : Module
{
	[SerializeField] private Capacitor capacitor = null;
	[Tooltip("Energy Consumption at full Throttle in kW.")]
	[SerializeField] private float energyConsumption = 160.0f;
	[SerializeField] private float thrust = 1.0f;
	private Transform spacecraftTransform = null;
	private new Rigidbody2D rigidbody = null;
	private InventoryController inventoryController = null;
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

		inventoryController = spacecraft.GetInventoryController();
		inventoryController.AddEnergyConsumer(capacitor);

		thrustParticles = gameObject.GetComponentInChildren<ParticleSystem>();
		thrustParticlesMain = thrustParticles.main;
		initialParticleSize = new Vector3(thrustParticlesMain.startSizeXMultiplier, thrustParticlesMain.startSizeYMultiplier, thrustParticlesMain.startSizeZMultiplier);
	}

	public override void Deconstruct()
	{
		inventoryController.RemoveEnergyConsumer(capacitor);
		spacecraft.RemoveThruster(this);

		base.Deconstruct();
	}

	public override void FixedUpdateNotify()
	{
		if(constructed && throttle > 0.0f)
		{
			float finalThrottle = throttle * capacitor.DischargePartial(energyConsumption * throttle * Time.fixedDeltaTime);
			rigidbody.AddForceAtPosition(spacecraftTransform.rotation * thrustVector * finalThrottle * Time.fixedDeltaTime, transform.position, ForceMode2D.Impulse);

			thrustParticlesMain.startSizeXMultiplier = initialParticleSize.x * finalThrottle;
			thrustParticlesMain.startSizeYMultiplier = initialParticleSize.y * finalThrottle;
			thrustParticlesMain.startSizeZMultiplier = initialParticleSize.z * finalThrottle;
		}
	}

	public Vector2 GetThrustVector()
	{
		return thrustVector;
	}

	public void SetThrottle(float throttle)
	{
		if(constructed && throttle > 0.0f)
		{
			if(this.throttle <= 0.0f)
			{
				thrustParticles.Play();
			}
		}
		else
		{
			thrustParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
		}

		this.throttle = Mathf.Clamp(throttle, 0.0f, 1.0f);
	}
}
