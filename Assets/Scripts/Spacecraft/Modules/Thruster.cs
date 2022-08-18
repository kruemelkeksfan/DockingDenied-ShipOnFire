using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Thruster : Module
{
	[SerializeField] private string fuelName = "Xenon";
	private Transform spacecraftTransform = null;
	private new Rigidbody2D rigidbody = null;
	private GravityWellController gravityWellController = null;
	private EnergyStorage capacitor = null;
	private Engine engine = null;
	private Vector2 thrustDirection = Vector2.zero;
	private float throttle = 0.0f;
	private float fuelSupply = 1.0f;									// TODO: Start with 0.0f, once Remote Trading is implemented
	private ParticleSystem thrustParticles = null;
	private ParticleSystem.MainModule thrustParticlesMain = new ParticleSystem.MainModule();
	private Vector3 initialParticleSize = Vector3.zero;

	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position, false, true);

		rigidbody = gameObject.GetComponentInParent<Rigidbody2D>();
		spacecraftTransform = spacecraft.transform;
		thrustDirection = transform.localRotation * Vector2.up;
		spacecraft.AddThruster(this);

		gravityWellController = GravityWellController.GetInstance();

		thrustParticles = gameObject.GetComponentInChildren<ParticleSystem>();
		thrustParticlesMain = thrustParticles.main;
		initialParticleSize = new Vector3(thrustParticlesMain.startSizeXMultiplier, thrustParticlesMain.startSizeYMultiplier, thrustParticlesMain.startSizeZMultiplier);

		capacitor = new EnergyStorage();
		AddComponentSlot(GoodManager.ComponentType.Capacitor, capacitor);
		inventoryController.AddEnergyConsumer(capacitor);

		engine = new Engine();
		AddComponentSlot(GoodManager.ComponentType.IonEngine, engine);
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
			if((engine.GetSecondaryFuelConsumption() * throttle) > fuelSupply)
			{
				if(inventoryController.Withdraw(fuelName, 1))
				{
					fuelSupply += 1.0f;
				}
				else
				{
					thrustParticlesMain.startSizeXMultiplier = 0.0f;
					thrustParticlesMain.startSizeYMultiplier = 0.0f;
					thrustParticlesMain.startSizeZMultiplier = 0.0f;

					InfoController.GetInstance().AddMessage("Out of " + fuelName + " for Propulsion!", true);

					return;
				}
			}
			
			float finalThrottle = throttle * capacitor.DischargePartial(engine.GetPrimaryFuelConsumption() * throttle * timeController.GetFixedDeltaTime());
			
			fuelSupply -= engine.GetSecondaryFuelConsumption() * finalThrottle;
			
			thrustParticlesMain.startSizeXMultiplier = initialParticleSize.x * finalThrottle;
			thrustParticlesMain.startSizeYMultiplier = initialParticleSize.y * finalThrottle;
			thrustParticlesMain.startSizeZMultiplier = initialParticleSize.z * finalThrottle;

			rigidbody.AddForceAtPosition(spacecraftTransform.rotation * thrustDirection * engine.GetThrust() * finalThrottle * timeController.GetFixedDeltaTime(),
				transform.position, ForceMode2D.Impulse);
		}
	}

	public Vector2 GetThrustDirection()
	{
		return thrustDirection;
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
