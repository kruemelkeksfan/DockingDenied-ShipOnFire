using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThrusterModule : Module
{
	[SerializeField] private float thrust = 1.0f;
	private Transform spacecraftTransform = null;
	private new Rigidbody2D rigidbody = null;
	private Vector2 thrustVector = Vector2.zero;
	private float throttle = 0.0f;
	private ParticleSystem thrustParticles = null;
	private ParticleSystem.MinMaxCurve thrustParticlesSizeX = new ParticleSystem.MinMaxCurve();
	private ParticleSystem.MinMaxCurve thrustParticlesSizeY = new ParticleSystem.MinMaxCurve();
	private ParticleSystem.MinMaxCurve thrustParticlesSizeZ = new ParticleSystem.MinMaxCurve();
	private Vector3 initialParticleSize = Vector3.one;

	protected override void Start()
	{
		base.Start();

		rigidbody = gameObject.GetComponentInParent<Rigidbody2D>();
		thrustParticles = gameObject.GetComponentInChildren<ParticleSystem>();
		thrustParticlesSizeX = thrustParticles.main.startSizeX;
		thrustParticlesSizeY = thrustParticles.main.startSizeY;
		thrustParticlesSizeZ = thrustParticles.main.startSizeZ;
		initialParticleSize = new Vector3(thrustParticlesSizeX.constant, thrustParticlesSizeY.constant, thrustParticlesSizeZ.constant);
	}

	public override void FixedUpdateNotify()
	{
		if(constructed && throttle > 0.0f)
		{
			rigidbody.AddForceAtPosition(spacecraftTransform.rotation * thrustVector * throttle * Time.fixedDeltaTime, transform.position, ForceMode2D.Impulse);

			/*
			// Manual Approach:

			// M = r x F
			// M - Torque
			// r - Lever
			// F - Thrust
			Vector2 lever = ((Vector2)this.Position * WorldConsts.GRID_SIZE) - rigidbody.centerOfMass;
			Vector2 thrust = spacecraftTransform.rotation * thrustVector * throttle * Time.fixedDeltaTime;
			float torque = Vector3.Cross(lever, thrust).z;

			// https://physics.stackexchange.com/questions/510025/linear-acceleration-on-a-spinning-satellite-with-an-unbalanced-force
			float force = Mathf.Sqrt(thrust.sqrMagnitude - (torque * torque));															// c^2 = a^2 + b^2

			rigidbody.velocity += -lever.normalized * (force / rigidbody.mass);
			rigidbody.angularVelocity += torque / rigidbody.inertia;
			*/
		}
	}
	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position, false, true);

		spacecraftTransform = spacecraft.transform;
		thrustVector = (transform.localRotation * Vector2.up) * thrust;
		spacecraft.AddThruster(this);
	}
	public override void Deconstruct()
	{
		spacecraft.RemoveThruster(this);
		base.Deconstruct();
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
			if(this.throttle != throttle)
			{
				thrustParticlesSizeX = initialParticleSize.x * throttle;
				thrustParticlesSizeY = initialParticleSize.y * throttle;
				thrustParticlesSizeZ = initialParticleSize.z * throttle;
			}
		}
		else
		{
			thrustParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
		}

		this.throttle = Mathf.Clamp(throttle, 0.0f, 1.0f);
	}
}
