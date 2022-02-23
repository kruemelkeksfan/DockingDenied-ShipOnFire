using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GravityWellController : MonoBehaviour, IListener
{
	public const double GRAVITY_CONSTANT = 0.000000000066743;

	private static GravityWellController instance = null;
	private static WaitForSecondsRealtime waitForPositionCheckInterval = null;
	private static WaitForFixedUpdate waitForFixedUpdate = null;

	[Tooltip("The Mass of this Gravity Well in kg")]
	[SerializeField] private double mass = 6.2e+21;
	[Tooltip("Determines how often the Heights of all Gravity Objects Orbits are checked in Ingame Seconds")]
	[SerializeField] private float positionCheckInterval = 5.0f;
	[Tooltip("Sea level Height above the Planet Center")]
	[SerializeField] private float surfaceAltitude = 500000.0f;
	[SerializeField] private float tooHighWarningAltitude = 3000000.0f;
	[SerializeField] private float tooLowWarningAltitude = 650000.0f;
	[Tooltip("Height at which Asteroids should start burning up")]
	[SerializeField] private float atmosphereEntryAltitude = 650000.0f;
	[Tooltip("Height at which Asteroids should be completely destroyed")]
	[SerializeField] private float destructionAltitude = 590000.0f;
	[Tooltip("Maximum Height for all Orbiters, regardless whether they were touched or not, unrailed Objects outside this Range will start decaying")]
	[SerializeField] private float maximumAltitude = 4000000.0f;
	[Tooltip("Atmospheric Density for Drag Calculation at Sea Level of the Planet in t/m^3")]
	[SerializeField] private float atmosphericDensity = 0.0012f;
	[Tooltip("Scale Height is the Height above Sea Level at which Air Pressure is 1/e of the Air Pressure on the Surface")]
	[SerializeField] private float scaleAltitude = 8500.0f;
	[Tooltip("A Particle System to visualize Re-Entry Heat and Plasma")]
	[SerializeField] private ParticleSystem plasmaParticleSystemPrefab = null;
	[Tooltip("Minimum Drag for Plasma Particles to be created")]
	[SerializeField] private float plasmaMinDrag = 400.0f;
	[Tooltip("Maximum Distance of the Player from the local Origin, before the Origin will be moved")]
	[SerializeField] private float maxOriginDistance = 10000.0f;
	[Tooltip("Distance to the Player at which all Objects are un-railed")]
	[SerializeField] private float unrailDistance = 10000.0f;
	[Tooltip("Distance to the Player at which un-railed Objects are on-railed again")]
	[SerializeField] private float onrailDistance = 12000.0f;
	private Transform planetTransform = null;
	private SpawnController spawnController = null;
	private InfoController infoController = null;
	private SpacecraftController localPlayerMainSpacecraft = null;
	private GameObject localPlayerMainObject = null;
	private Transform localPlayerMainTransform = null;
	private Rigidbody2D localPlayerMainRigidbody = null;
	private double gravitationalParameter = 0.0f;
	private Dictionary<Rigidbody2D, GravityObjectController> gravityObjects = null;
	private Vector2Double localOrigin = Vector2Double.zero;
	private bool originShifted = false;

	public static GravityWellController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		if(instance == null)
		{
			waitForPositionCheckInterval = new WaitForSecondsRealtime(positionCheckInterval);
			waitForFixedUpdate = new WaitForFixedUpdate();

			planetTransform = gameObject.GetComponent<Transform>();

			gravitationalParameter = GRAVITY_CONSTANT * mass;
			gravityObjects = new Dictionary<Rigidbody2D, GravityObjectController>();

			// Square Height Constraints to avoid Sqrt later on
			tooHighWarningAltitude *= tooHighWarningAltitude;
			tooLowWarningAltitude *= tooLowWarningAltitude;
			atmosphereEntryAltitude *= atmosphereEntryAltitude;
			destructionAltitude *= destructionAltitude;
			maximumAltitude *= maximumAltitude;
			unrailDistance *= unrailDistance;
			onrailDistance *= onrailDistance;

			instance = this;
		}
		else
		{
			// Safety Measure to avoid Start()- and FixedUpdate()-Calls
			gameObject.SetActive(false);
			GameObject.Destroy(gameObject);
		}
	}

	private void Start()
	{
		StartCoroutine(CheckGravityObjectPositions());

		spawnController = SpawnController.GetInstance();
		infoController = InfoController.GetInstance();

		SpacecraftManager.GetInstance().AddSpacecraftChangeListener(this);
		Notify();
	}

	private void FixedUpdate()
	{
		Vector2 playerPosition = localPlayerMainTransform.position;
		float time = Time.time;
		// Avoid Dictionary.Get() for Performance Reasons
		foreach(KeyValuePair<Rigidbody2D, GravityObjectController> gravityObject in gravityObjects)
		{
			// Buffer for Performance Reasons
			GravityObjectController gravityObjectValue = gravityObject.Value;
			Transform gravityObjectTransform = gravityObjectValue.GetTransform();
			Vector3 gravityObjectPosition = gravityObjectTransform.position;
			AsteroidController asteroid = gravityObjectValue as AsteroidController;
			SpacecraftController spacecraft = null;
			if(asteroid == null)
			{
				spacecraft = gravityObjectValue as SpacecraftController;
			}

			// Check onRails first, because we can simulate Gravity after unRailing, but we can not solve analytically after onRailing,
			// because rigidbody.position is updated after FixedUpdate() and we therefore have to use an obsolete Position for onRailing anyways
			// Also: onRailed Objects suffer less from skipped Frames anyways
			if(gravityObjectValue.IsOnRails())
			{
				// TODO: Set a max Velocity Difference as [SerializeField], calculate next Check Time from max Velocity, Distance to closest Player and unrailDistance and skip Position Calculations until next Check Time
				if((spacecraft != null && spacecraft.IsThrusting()) || ((Vector2)gravityObjectPosition - playerPosition).sqrMagnitude <= unrailDistance)
				{
					gravityObjectValue.UnRail();
					gravityObjectValue.ToggleRenderer(true);
				}
				else
				{
					// Analytical Gravity Solution
					Vector2Double position = GlobalToLocalPosition(gravityObjectValue.CalculateOnRailPosition(time));
					gravityObjectTransform.position = new Vector3((float)position.x, (float)position.y, gravityObjectPosition.z);
				}
			}

			// Check again, in case we unrailed above, to avoid skipping a Frame
			if(!gravityObjectValue.IsOnRails())
			{
				// Don't on-Rail thrusting or dying Objects to avoid interfering with Unity Physics or Destruction Coroutines
				// Use Time from last Frame, since this Frames rigidbody.velocity and Forces have not been applied by the Physics Engine yet
				if((spacecraft == null || !spacecraft.IsThrusting()) && !gravityObjectValue.IsDecaying()
					&& ((Vector2)gravityObjectPosition - playerPosition).sqrMagnitude >= onrailDistance
					&& gravityObjectValue.OnRail(LocalToGlobalPosition(gravityObjectPosition), gravityObject.Key.velocity, Time.time - Time.fixedDeltaTime))
				{
					gravityObjectValue.ToggleRenderer(false);
				}
				else
				{
					// Gravity Simulation
					// The Gravity Source is always at global (0.0|0.0)
					Vector2Double gravityDirection = -LocalToGlobalPosition(gravityObjectPosition);
					if(gravityDirection.x != 0.0f || gravityDirection.y != 0.0f)
					{
						// https://en.wikipedia.org/wiki/Gravity#Newton's_theory_of_gravitation
						// F = (G * m1 * m2) / (r^2)
						// A = F / m2
						// A = (G * m1) / (r^2)
						// A = ((G * m1) / (|r|^2)) * (r/|r|) = ((G * m1) / (|r|^3)) * r
						// gravitationalParameter = G * m1
						// Avoid Method Calls for Performance
						// UnityEngine.Mathf is Wrapper for System.Math, call directly for Optimization
						double gravityDirectionMagnitude = Math.Sqrt(gravityDirection.x * gravityDirection.x + gravityDirection.y * gravityDirection.y);

						Vector2Double gravity = gravityDirection
							* ((gravitationalParameter / (gravityDirectionMagnitude * gravityDirectionMagnitude * gravityDirectionMagnitude))
							* Time.fixedDeltaTime);
						// Fucking Box2D Physics Engine does not have a ForceMode.VelocityChange
						gravityObject.Key.velocity = ((Vector2Double)gravityObject.Key.velocity) + gravity;
					}
				}
			}

			// Reset objectRecord.thrusting after Read, because it is difficult to reset in Spacecraft
			spacecraft?.SetThrusting(false);
		}
	}

	public void Notify()
	{
		localPlayerMainSpacecraft = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft();
		localPlayerMainObject = localPlayerMainSpacecraft.gameObject;
		localPlayerMainTransform = localPlayerMainSpacecraft.GetTransform();
		localPlayerMainRigidbody = localPlayerMainSpacecraft.GetRigidbody();
	}

	private IEnumerator CheckGravityObjectPositions()
	{
		List<Rigidbody2D> deorbitObjects = new List<Rigidbody2D>();
		List<Rigidbody2D> despawnObjects = new List<Rigidbody2D>();
		while(true)
		{
			yield return waitForPositionCheckInterval;

			// Check, and if necessary move, Origin
			// Must perform Origin Ship outside FixedUpdate() to avoid Interference with Physics Engine
			Vector2 originShift = localPlayerMainTransform.position;
			if(originShift.x < -maxOriginDistance || originShift.x > maxOriginDistance
				|| originShift.y < -maxOriginDistance || originShift.y > maxOriginDistance)
			{
				localOrigin += (Vector2Double)originShift;
				planetTransform.position = -localOrigin;
				originShifted = true;
			}

			deorbitObjects.Clear();
			despawnObjects.Clear();
			foreach(Rigidbody2D gravityObject in gravityObjects.Keys)
			{
				GravityObjectController objectRecord = gravityObjects[gravityObject];

				if(objectRecord.IsOnRails())
				{
					// Objects on-Rails are not Origin-shifted and have reasonably stable Orbits
					continue;
				}

				// Origin Shift
				if(originShifted)
				{
					objectRecord.transform.position -= (Vector3)originShift;
				}

				AsteroidController asteroid = objectRecord as AsteroidController;
				double sqrOrbitalAltitude = LocalToGlobalPosition(objectRecord.transform.position).SqrMagnitude();
				if(((asteroid != null && !asteroid.IsTouched()
					&& (sqrOrbitalAltitude < asteroid.GetAltitudeConstraint().min || sqrOrbitalAltitude > asteroid.GetAltitudeConstraint().max))
					|| sqrOrbitalAltitude < atmosphereEntryAltitude || sqrOrbitalAltitude > maximumAltitude)
					&& !objectRecord.IsDecaying())
				{
					if(sqrOrbitalAltitude < atmosphereEntryAltitude)
					{
						deorbitObjects.Add(gravityObject);
					}
					else
					{
						despawnObjects.Add(gravityObject);
					}

					objectRecord.SetDecaying(true);
				}
			}

			foreach(Rigidbody2D deorbitObject in deorbitObjects)
			{
				StartCoroutine(Deorbit(deorbitObject));
			}
			foreach(Rigidbody2D despawnObject in despawnObjects)
			{
				StartCoroutine(spawnController.DespawnObject(despawnObject));
			}

			if(originShifted)
			{
				yield return waitForFixedUpdate;

				originShifted = false;
			}

			// Check Player Orbit
			if(infoController.GetMessageCount() < 1)
			{
				Vector2Double playerPosition = LocalToGlobalPosition(localPlayerMainTransform.position);
				double sqrPlayerAltitude = playerPosition.SqrMagnitude();
				localPlayerMainSpacecraft.CalculateOrbitalElements(playerPosition, localPlayerMainRigidbody.velocity, Time.time);
				double periapsis = localPlayerMainSpacecraft.CalculatePeriapsisAltitude();
				double apoapsis = localPlayerMainSpacecraft.CalculateApoapsisAltitude();
				if(periapsis * periapsis <= atmosphereEntryAltitude && sqrPlayerAltitude <= tooLowWarningAltitude && sqrPlayerAltitude > atmosphereEntryAltitude)
				{
					infoController.AddMessage("Dangerously low, increase Speed!");
				}
				if((apoapsis == 0 || apoapsis * apoapsis >= maximumAltitude) && sqrPlayerAltitude >= tooHighWarningAltitude)
				{
					infoController.AddMessage("Leaving Signal Range, get back to the Planet!");
				}
			}
		}
	}

	private IEnumerator Deorbit(Rigidbody2D gravityObject)
	{
		GravityObjectController objectRecord = gravityObjects[gravityObject];
		objectRecord.UnRail();

		MeshRenderer[] renderers = gravityObject.gameObject.GetComponentsInChildren<MeshRenderer>();
		Bounds bounds = renderers[0].bounds;
		foreach(MeshRenderer renderer in renderers)
		{
			bounds.Encapsulate(renderer.bounds);
		}
		float shipRadius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
		float sqrtShipRadius = Mathf.Sqrt(shipRadius);

		ParticleSystem plasmaParticles = GameObject.Instantiate<ParticleSystem>(
			plasmaParticleSystemPrefab, bounds.center, Quaternion.identity, objectRecord.transform);
		ParticleSystem.ShapeModule shape = plasmaParticles.shape;
		shape.radius = shipRadius;
		ParticleSystem.EmissionModule emission = plasmaParticles.emission;
		float startRateOverTimeMultiplier = emission.rateOverTimeMultiplier;
		// Set Emission to 0.0 to avoid producing Particles for 1 Frame when the Sjip is too slow
		emission.rateOverTimeMultiplier = 0.0f;
		ParticleSystem.SizeOverLifetimeModule size = plasmaParticles.sizeOverLifetime;
		size.sizeMultiplier *= sqrtShipRadius;
		ParticleSystem.VelocityOverLifetimeModule velocity = plasmaParticles.velocityOverLifetime;
		plasmaParticles.Play();

		float destructionTime = 0.0f;
		while(true)
		{
			yield return waitForFixedUpdate;

			float sqrOrbitalAltitude = (float)LocalToGlobalPosition(objectRecord.transform.position).SqrMagnitude();
			if(sqrOrbitalAltitude <= atmosphereEntryAltitude)
			{
				if(gravityObject.gameObject == localPlayerMainObject && infoController.GetMessageCount() <= 0)
				{
					if(sqrOrbitalAltitude < destructionAltitude + 2000.0f)
					{
						infoController.AddMessage("Brace Position!");
					}
					else if(sqrOrbitalAltitude < atmosphereEntryAltitude)
					{
						infoController.AddMessage("Atmosphere detected, pull up!");
					}
				}

				// TODO: Apply Heat Damage to Modules and Asteroids based on Drag
				// Drag based on an Approximation of atmospheric Density (not Pressure!)
				// https://en.wikipedia.org/wiki/Barometric_formula#Density_equations
				// https://en.wikipedia.org/wiki/Scale_height#Scale_height_used_in_a_simple_atmospheric_pressure_model
				float currentDensity = atmosphericDensity * Mathf.Exp(-(Mathf.Sqrt(sqrOrbitalAltitude) - surfaceAltitude) / scaleAltitude);
				// https://en.wikipedia.org/wiki/Drag_(physics)#The_drag_equation
				// Drag Coefficient for a Cube is roughly 1.0, if we ignore that Drag Coefficient is dependent on the Reynolds Number
				float drag = (0.5f * currentDensity * gravityObject.velocity.sqrMagnitude * shipRadius * bounds.extents.z);
				gravityObject.AddForce(-gravityObject.velocity.normalized * (drag * Time.deltaTime), ForceMode2D.Impulse);

				// Don't rotate ParticleSystem with Ship, so Particle Velocity can be given in global Coordinates
				plasmaParticles.transform.rotation = Quaternion.identity;
				// Local Space and set Velocity each Frame (instead of global Space and Velocity == 0.0) to avoid Problems with Origin Shift
				velocity.xMultiplier = -gravityObject.velocity.x;
				velocity.yMultiplier = -gravityObject.velocity.y;

				// TODO: When Heat Damage is implemented, apply maxDamage below DestructionAltitude, regardless of Velocity
				if(sqrOrbitalAltitude < destructionAltitude)
				{
					emission.rateOverTimeMultiplier = startRateOverTimeMultiplier * Mathf.Sqrt(Mathf.Max(drag, plasmaMinDrag));
					if(destructionTime > 0.0f)
					{
						if(Time.time - destructionTime > 2.0f)
						{
							// TODO: Call kill in Spacecraft.OnDestroy() and simply destroy Player Spacecraft here
							// TODO: Destroy Player Spacecraft if Altitude is too high
							if(gravityObject.gameObject == localPlayerMainObject)
							{
								gravityObject.GetComponent<SpacecraftController>().Kill();
							}
							else
							{
								GameObject.Destroy(gravityObject.gameObject);
							}

							yield break;
						}
					}
					else
					{
						destructionTime = Time.time;
					}
				}
				else if(drag >= plasmaMinDrag)
				{
					emission.rateOverTimeMultiplier = startRateOverTimeMultiplier * Mathf.Sqrt(drag - plasmaMinDrag);                           // Sqrt to limit Particle Count on large Ships
				}
				else
				{
					emission.rateOverTimeMultiplier = 0.0f;
				}
			}
			else
			{
				objectRecord.SetDecaying(false);
				GameObject.Destroy(plasmaParticles.gameObject);

				yield break;
			}
		}
	}

	// Calculates the required orbital Velocity for a circular Orbit at the current Altitude of this Orbiter.
	public Vector2Double CalculateOptimalOrbitalVelocity(Rigidbody2D orbiter)
	{
		Vector2Double gravityDirection = -LocalToGlobalPosition(gravityObjects[orbiter].transform.position);
		double altitude = gravityDirection.Magnitude();
		if(altitude <= 0.0f)
		{
			Debug.LogWarning("Call to CalculateOrbitalVelocity() in GravityController with altitude '" + altitude + "'!");
			return Vector2Double.zero;
		}

		// See: https://www.satsig.net/orbit-research/orbit-height-and-speed.htm
		// Velocity = sqrt(Gravitational Constant * Mass of Main Body / Radius)
		Vector2Double orbitalVelocity = ((Vector2Double.Perpendicular(gravityDirection) / altitude)
			* Math.Sqrt(gravitationalParameter / altitude));

		// Invert Velocity, if the Orbiter is already going into the other Direction
		if(Vector2Double.Dot(new Vector2Double(orbiter.velocity), orbitalVelocity) > 0.0f)
		{
			return orbitalVelocity;
		}
		else
		{
			return -orbitalVelocity;
		}
	}

	public void DrawOrbit(Rigidbody2D orbiter)
	{
		// TODO: Implement!
	}

	public void AddGravityObject(GravityObjectController gravityObject, MinMax? asteroidBeltHeight = null)
	{
		AsteroidController asteroid;
		if(asteroidBeltHeight != null && (asteroid = gravityObject as AsteroidController) != null)
		{
			asteroid.SetAltitudeConstraint(asteroidBeltHeight.Value);
		}

		Rigidbody2D gravityObjectRigidbody = gravityObject.GetRigidbody();
		gravityObjects.Add(gravityObjectRigidbody, gravityObject);
		gravityObjectRigidbody.velocity = CalculateOptimalOrbitalVelocity(gravityObjectRigidbody);
	}

	public void RemoveGravityObject(Rigidbody2D gravityObject)
	{
		gravityObjects.Remove(gravityObject);
	}

	public double GetGravitationalParameter()
	{
		return gravitationalParameter;
	}

	public float GetSurfaceAltitude()
	{
		return surfaceAltitude;
	}

	public Vector2Double GetLocalOrigin()
	{
		return localOrigin;
	}

	public Vector2Double LocalToGlobalPosition(Vector2Double localPosition)
	{
		// Perform Calculation with Vector2Double for Precision
		return localOrigin + localPosition;
	}

	public Vector2Double GlobalToLocalPosition(Vector2Double globalPosition)
	{
		// Perform Calculation with Vector2Double for Precision
		return globalPosition - localOrigin;
	}

	public bool IsOriginShifted()
	{
		return originShifted;
	}
}
