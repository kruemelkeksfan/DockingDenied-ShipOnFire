using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GravityWellController : MonoBehaviour, IListener
{
	private struct AsteroidRecord
	{
		public MinMax altitudeConstraint;
		public bool touched;

		public AsteroidRecord(MinMax altitudeConstraint)
		{
			this.altitudeConstraint = new MinMax(altitudeConstraint.min * altitudeConstraint.min, altitudeConstraint.max * altitudeConstraint.max);
			touched = false;
		}

		public void MarkTouched()
		{
			touched = true;
		}
	}

	public const double GRAVITY_CONSTANT = 0.000000000066743;

	private static GravityWellController instance = null;
	private static WaitForSeconds waitForAltitudeCheckInterval = null;
	private static WaitForFixedUpdate waitForFixedUpdate = null;

	[Tooltip("Determines how often the Heights of all Gravity Objects Orbits are checked in Ingame Seconds")]
	[SerializeField] private float altitudeCheckInterval = 5.0f;
	[Tooltip("Sea level Height above the Planet Center")]
	[SerializeField] private float surfaceAltitude = 250.0f;
	[SerializeField] private float pullUpWarningAltitude = 370.0f;
	[Tooltip("Height at which Asteroids should start burning up")]
	[SerializeField] private float atmossphereEntryAltitude = 320.0f;
	[Tooltip("Height at which Asteroids should be completely destroyed")]
	[SerializeField] private float destructionAltitude = 280.0f;
	[Tooltip("Valid Height Constraint for all Asteroids, regardless whether they were touched or not, Asteroids outside this Range will start decaying")]
	[SerializeField] private MinMax globalAltitudeConstraint = new MinMax(320.0f, 10000.0f);
	[Tooltip("Atmosspheric Density for Drag Calculation at Sea Level of the Planet")]
	[SerializeField] private float atmossphericDensity = 0.2f;
	[Tooltip("A Particle System to visualize Re-Entry Heat and Plasma")]
	[SerializeField] private ParticleSystem plasmaParticleSystemPrefab = null;
	private SpawnController spawnController = null;
	private InfoController infoController = null;
	private GameObject localPlayerMainSpacecraftObject = null;
	private float gravitationalParameter;
	private Dictionary<Rigidbody2D, AsteroidRecord?> gravityObjects = null;
	HashSet<Rigidbody2D> deadGravityObjects = null;
	private float scaleAltitude = 0.0f;
	private float halfMaxAltitude = 0.0f;

	public static GravityWellController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		if(waitForAltitudeCheckInterval == null || waitForFixedUpdate == null)
		{
			waitForAltitudeCheckInterval = new WaitForSeconds(altitudeCheckInterval);
			waitForFixedUpdate = new WaitForFixedUpdate();
		}

		gravitationalParameter = (float)(gameObject.GetComponent<Rigidbody2D>().mass * GRAVITY_CONSTANT * 1000000000000000000000000.0);                                     // Celestial Body Mass is given in 10^24 KGs, to accommodate them in a float
		gravityObjects = new Dictionary<Rigidbody2D, AsteroidRecord?>();
		deadGravityObjects = new HashSet<Rigidbody2D>();

		pullUpWarningAltitude *= pullUpWarningAltitude;                                                                                                                     // Square to avoid Mathf.sqrt() later on
		halfMaxAltitude = (globalAltitudeConstraint.max * 0.5f) * (globalAltitudeConstraint.max * 0.5f);
		globalAltitudeConstraint = new MinMax(globalAltitudeConstraint.min * globalAltitudeConstraint.min, globalAltitudeConstraint.max * globalAltitudeConstraint.max);    // Square to avoid Mathf.sqrt() later on

		scaleAltitude = atmossphereEntryAltitude - surfaceAltitude;                                                                                                         // According to Formula Scale Height is the Height at which
																																											// 1/e of the Air Density of the Surface is present,
																																											// so treating it as the edge of the Atmossphere is a bit overdrawn

		instance = this;
	}

	private void Start()
	{
		StartCoroutine(CheckHeight());

		spawnController = SpawnController.GetInstance();
		infoController = InfoController.GetInstance();
		SpacecraftManager spacecraftManager = SpacecraftManager.GetInstance();
		localPlayerMainSpacecraftObject = spacecraftManager.GetLocalPlayerMainSpacecraft().gameObject;
		spacecraftManager.AddSpacecraftChangeListener(this);
	}

	// TODO: Further Optimization Ideas:
	// (1) own Thread for Gravity Calculation (How to synchronize rigidbody.velocity and would it actually save performance?)
	// (2) go back to using a Coroutine with sparse Updates
	// (3) disable Renderers or even Renderers and Colliders of Asteroids far enough away from all Cameras (MMO Compatibility?)
	// (4) use simpler Mesh for Asteroids
	// (5) instead of simulating, calculate the whole Orbit on Start and after every Collision and just update the Velocity along the Path
	private void FixedUpdate()
	{
		foreach(Rigidbody2D gravityObject in gravityObjects.Keys)
		{
			Vector2 gravityDirection = -gravityObject.position;
			if(gravityDirection.x != 0.0f || gravityDirection.y != 0.0f)
			{
				float sqrGravityDirectionMagnitude = gravityDirection.x * gravityDirection.x + gravityDirection.y * gravityDirection.y;
				Vector2 gravity = (((gravitationalParameter
					// TODO: Cancel out 1000.0f from next 2 Lines
					/ (sqrGravityDirectionMagnitude * (1000.0f * 1000.0f)))                     // Convert from km to m for Calculation since 1 Unit is 1 km and use (a*b)^2 = a^2 * b^2 to avoid a Sqrt
					/ 1000.0f)                                                                  // Convert Result from m/s back to km/s
					* Time.fixedDeltaTime)
					* (gravityDirection / Mathf.Sqrt(sqrGravityDirectionMagnitude));
				gravityObject.velocity += gravity;                                              // Fucking Box2D Physics Engine does not have a ForceMode.VelocityChange
			}
		}
	}

	public void Notify()
	{
		localPlayerMainSpacecraftObject = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft().gameObject;
	}

	private IEnumerator CheckHeight()
	{
		while(true)
		{
			yield return altitudeCheckInterval;

			List<Rigidbody2D> deorbitObjects = new List<Rigidbody2D>();
			List<Rigidbody2D> despawnObjects = new List<Rigidbody2D>();
			foreach(Rigidbody2D gravityObject in gravityObjects.Keys)
			{
				float sqrOrbitalHeight = ((Vector2)gravityObject.position).sqrMagnitude;
				if(((gravityObjects[gravityObject].HasValue && !gravityObjects[gravityObject].Value.touched
					&& (sqrOrbitalHeight < gravityObjects[gravityObject].Value.altitudeConstraint.min || sqrOrbitalHeight > gravityObjects[gravityObject].Value.altitudeConstraint.max))
					|| sqrOrbitalHeight < globalAltitudeConstraint.min || sqrOrbitalHeight > globalAltitudeConstraint.max || (gravityObject.gameObject == localPlayerMainSpacecraftObject && (sqrOrbitalHeight < pullUpWarningAltitude || sqrOrbitalHeight > halfMaxAltitude)))
					&& !deadGravityObjects.Contains(gravityObject))
				{
					if(gravityObject.gameObject == localPlayerMainSpacecraftObject)
					{
						if(sqrOrbitalHeight < pullUpWarningAltitude && sqrOrbitalHeight > globalAltitudeConstraint.min)
						{
							if(infoController.GetMessageCount() <= 0)
							{
								infoController.AddMessage("Altitude critical, pull up!");
							}
							continue;
						}
						else if(sqrOrbitalHeight > globalAltitudeConstraint.max)
						{
							if(infoController.GetMessageCount() <= 0)
							{
								infoController.AddMessage("Leaving Signal Range, get back to the Planet!");
							}
							continue;
						}
						else if(sqrOrbitalHeight > halfMaxAltitude)
						{
							if(infoController.GetMessageCount() <= 0)
							{
								infoController.AddMessage("Signal is getting weaker...");
							}
							continue;
						}
					}

					if(sqrOrbitalHeight < globalAltitudeConstraint.min)
					{
						deorbitObjects.Add(gravityObject);
					}
					else
					{
						despawnObjects.Add(gravityObject);
					}
					deadGravityObjects.Add(gravityObject);
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
		}
	}

	private IEnumerator Deorbit(Rigidbody2D gravityObject)
	{
		gravityObject.drag = 0.02f;

		Transform gravityObjectTransform = gravityObject.GetComponent<Transform>();
		float orbitalAltitude;
		ParticleSystem plasmaParticles = null;
		while((orbitalAltitude = ((Vector2)gravityObjectTransform.position).magnitude) > destructionAltitude)
		{
			if(orbitalAltitude < atmossphereEntryAltitude)
			{
				if(plasmaParticles == null)
				{
					gravityObject.drag = 0.0f;

					MeshRenderer[] renderers = gravityObject.gameObject.GetComponentsInChildren<MeshRenderer>();
					Bounds bounds = renderers[0].bounds;
					foreach(MeshRenderer renderer in renderers)
					{
						bounds.Encapsulate(renderer.bounds);
					}
					plasmaParticles = GameObject.Instantiate<ParticleSystem>(plasmaParticleSystemPrefab, bounds.center, Quaternion.identity, gravityObjectTransform);
					ParticleSystem.ShapeModule shape = plasmaParticles.shape;
					shape.radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) * 1.2f;
					ParticleSystem.EmissionModule emission = plasmaParticles.emission;
					emission.rateOverTimeMultiplier = shape.radius * shape.radius;
					ParticleSystem.SizeOverLifetimeModule size = plasmaParticles.sizeOverLifetime;
					size.sizeMultiplier *= Mathf.Sqrt(shape.radius);                                                                                                    // Sqrt to get Values nearer to 1 and dampen Size
				}
				float drag = atmossphericDensity * Mathf.Exp(-(orbitalAltitude - surfaceAltitude) / scaleAltitude);                                                     // Drag based on an Approximation of atmosspheric Density
				gravityObject.velocity *= 1.0f - Mathf.Min((gravityObject.velocity.sqrMagnitude * drag * Time.deltaTime), 1.0f);
			}
			else if(plasmaParticles != null && orbitalAltitude * orbitalAltitude > globalAltitudeConstraint.min)
			{
				deadGravityObjects.Remove(gravityObject);
				GameObject.Destroy(plasmaParticles.gameObject);

				yield break;
			}

			yield return waitForFixedUpdate;
		}

		if(gravityObject.gameObject == localPlayerMainSpacecraftObject)
		{
			gravityObject.GetComponent<Spacecraft>().Kill();
		}
		else
		{
			GameObject.Destroy(gravityObject.gameObject);
		}
	}

	// Calculates the required orbital Velocity for a circular Orbit at the current Height of this Orbiter.
	public Vector2 CalculateOptimalOrbitalVelocity(Rigidbody2D orbiter)
	{
		Vector2 orbitalVelocity = CalculateOptimalOrbitalVelocity(orbiter.position);

		if(Vector2.Dot(orbiter.velocity, orbitalVelocity) > 0.0f)                                                         // Turn the Target Velocity around, if the Orbiter is already going into the other Direction
		{
			return orbitalVelocity;
		}
		else
		{
			return -orbitalVelocity;
		}
	}

	// Calculates the orbital Velocity to orbit at a given Height from the Vector from the GravityWell to the Orbiter.
	public Vector2 CalculateOptimalOrbitalVelocity(Vector2 orbitalDirection, float altitude = -1.0f)
	{
		if(altitude < 0.0f)
		{
			altitude = orbitalDirection.magnitude;
		}
		if(altitude <= 0.0f)
		{
			Debug.LogWarning("Invalid Call of CalculateOrbitalVelocity() in GravityController with altitude " + altitude + " being 0!");
			return Vector2.zero;
		}

		// See: https://www.satsig.net/orbit-research/orbit-height-and-speed.htm
		// Velocity = sqrt(Gravitational Constant * Mass of Main Body / Radius)
		return (new Vector2(orbitalDirection.y, -orbitalDirection.x) / altitude)
			* Mathf.Sqrt(gravitationalParameter
			/ (altitude * 1000.0f))                                                                                                         // Convert from km to m
			/ 1000.0f;                                                                                                                      // Convert back from m/s to km/s
	}

	public void AddGravityObject(Rigidbody2D gravityObject, MinMax asteroidBeltHeight = new MinMax())
	{
		gravityObjects.Add(gravityObject, ((asteroidBeltHeight.min != 0.0f || asteroidBeltHeight.max != 0.0f) ? ((AsteroidRecord?)new AsteroidRecord(asteroidBeltHeight)) : null));
	}

	public void RemoveGravityObject(Rigidbody2D gravityObject)
	{
		deadGravityObjects.Remove(gravityObject);
		gravityObjects.Remove(gravityObject);
	}

	public bool MarkAsteroidTouched(Rigidbody2D asteroid, Rigidbody2D otherAsteroid)
	{
		if(gravityObjects[asteroid].HasValue)
		{
			if(otherAsteroid.gameObject.layer == 10 || (gravityObjects[otherAsteroid].HasValue && gravityObjects[otherAsteroid].Value.touched))
			{
				gravityObjects[asteroid].Value.MarkTouched();

				// TODO: Remove when verified
				asteroid.name = "TOUCHY";

				return true;
			}
			else
			{
				return false;
			}
		}
		else
		{
			// TODO: Remove when verified
			asteroid.name = "TOUCHY";

			return true;
		}
	}

	public float GetSurfaceAltitude()
	{
		return surfaceAltitude;
	}
}
