using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GravityWellController : MonoBehaviour, IListener
{
	private struct AsteroidRecord
	{
		public int beltIndex;
		public bool touched;

		public AsteroidRecord(int beltIndex)
		{
			this.beltIndex = beltIndex;
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
	[Tooltip("Height below the XY-Plane at which old Asteroids will enter the Atmossphere and decay")]
	[SerializeField] private float decayHeight = 100.0f;
	[Tooltip("Velocity with which Asteroids leave the orbital Plane when despawning")]
	[SerializeField] private float clearSpeed = 0.002f;
	[Tooltip("Sea level Height above the Planet Center")]
	[SerializeField] private float surfaceAltitude = 250.0f;
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
	private GameObject localPlayerMainSpacecraftObject = null;
	private Vector2 position = Vector2.zero;
	private float gravitationalParameter;
	private Dictionary<Rigidbody2D, AsteroidRecord?> gravityObjects = null;
	HashSet<Rigidbody2D> deadGravityObjects = null;
	private MinMax[] asteroidAltitudeConstraints = { };
	private float scaleAltitude = 0.0f;

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

		position = transform.position;
		gravitationalParameter = (float)(gameObject.GetComponent<Rigidbody2D>().mass * GRAVITY_CONSTANT * 1000000000000000000000000.0);										// Celestial Body Mass is given in 10^24 KGs, to accommodate them in a float
		gravityObjects = new Dictionary<Rigidbody2D, AsteroidRecord?>();
		deadGravityObjects = new HashSet<Rigidbody2D>();

		globalAltitudeConstraint = new MinMax(globalAltitudeConstraint.Min * globalAltitudeConstraint.Min, globalAltitudeConstraint.Max * globalAltitudeConstraint.Max);	// Square to avoid Mathf.sqrt() later on

		scaleAltitude = atmossphereEntryAltitude - surfaceAltitude;																											// According to Formula Scale Height is the Height at which
																																											// 1/e of the Air Density of the Surface is present,
																																											// so treating it as the edge of the Atmossphere is a bit overdrawn

		instance = this;
	}

	private void Start()
	{
		StartCoroutine(CheckHeight());

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
			Vector2 gravityDirection = position - gravityObject.position;
			if(gravityDirection.x != 0.0f || gravityDirection.y != 0.0f)
			{
				float sqrGravityDirectionMagnitude = gravityDirection.x * gravityDirection.x + gravityDirection.y * gravityDirection.y;
				Vector2 gravity = (((gravitationalParameter
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

			foreach(Rigidbody2D gravityObject in gravityObjects.Keys)
			{
				float sqrOrbitalHeight = (position - (Vector2)gravityObject.position).sqrMagnitude;
				if(((gravityObjects[gravityObject].HasValue && !gravityObjects[gravityObject].Value.touched
					&& (sqrOrbitalHeight < asteroidAltitudeConstraints[gravityObjects[gravityObject].Value.beltIndex].Min || sqrOrbitalHeight > asteroidAltitudeConstraints[gravityObjects[gravityObject].Value.beltIndex].Max))
					|| sqrOrbitalHeight < globalAltitudeConstraint.Min || sqrOrbitalHeight > globalAltitudeConstraint.Max)
					&& !deadGravityObjects.Contains(gravityObject))
				{
					if(gravityObject.gameObject == localPlayerMainSpacecraftObject)
					{
						if(sqrOrbitalHeight > globalAltitudeConstraint.Max)
						{
							InfoController.GetInstance().AddMessage("Leaving Signal Range, get back to the Planet!");
							continue;
						}
						else if(sqrOrbitalHeight < globalAltitudeConstraint.Min)
						{
							InfoController.GetInstance().AddMessage("Altitude critical, pull up!");
						}
					}

					// TODO: Different Mechanism for Objects which leave the Orbit (despawn when they reach a ridiculous Distance from the Planet and are not in Sight)
					StartCoroutine(Despawn(gravityObject, gravityObjects[gravityObject]));
					deadGravityObjects.Add(gravityObject);
				}
			}
		}
	}

	private IEnumerator Despawn(Rigidbody2D gravityObject, AsteroidRecord? asteroidRecord)
	{
		int previousLayer = gravityObject.gameObject.layer;
		gravityObject.gameObject.layer = 9;
		gravityObject.drag = 0.02f;

		Transform gravityObjectTransform = gravityObject.GetComponent<Transform>();
		float orbitalAltitude = 0.0f;
		ParticleSystem plasmaParticles = null;
		while((orbitalAltitude = ((Vector2)gravityObjectTransform.position - position).magnitude) > destructionAltitude)
		{
			if(asteroidRecord.HasValue && gravityObjectTransform.position.z < decayHeight)
			{
				gravityObjectTransform.position += new Vector3(0.0f, 0.0f, clearSpeed * Time.fixedDeltaTime);
			}

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
					size.sizeMultiplier *= Mathf.Sqrt(shape.radius);																									// Sqrt to get Values nearer to 1 and dampen Size
				}
				float drag = atmossphericDensity * Mathf.Exp(-(orbitalAltitude - surfaceAltitude) / scaleAltitude);														// Drag based on an Approximation of atmosspheric Density
				gravityObject.velocity *= 1.0f - Mathf.Min((gravityObject.velocity.sqrMagnitude * drag * Time.deltaTime), 1.0f);
			}
			else if(plasmaParticles != null && orbitalAltitude * orbitalAltitude > globalAltitudeConstraint.Min)
			{
				while(asteroidRecord.HasValue && gravityObjectTransform.position.z > 0.0f)
				{
					gravityObjectTransform.position -= new Vector3(0.0f, 0.0f, clearSpeed * Time.fixedDeltaTime);
					yield return waitForFixedUpdate;
				}

				gravityObject.drag = 0.0f;
				gravityObject.gameObject.layer = previousLayer;
				deadGravityObjects.Remove(gravityObject);
				GameObject.Destroy(plasmaParticles);

				yield break;
			}

			yield return waitForFixedUpdate;
		}

		if(asteroidRecord.HasValue)
		{
			--AsteroidSpawner.AsteroidCount;
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
		Vector2 orbitalVelocity = CalculateOptimalOrbitalVelocity(position - orbiter.position);

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
		return (new Vector2(-orbitalDirection.y, orbitalDirection.x) / altitude)
			* Mathf.Sqrt(gravitationalParameter
			/ (altitude * 1000.0f))																											// Convert from km to m
			/ 1000.0f;																														// Convert back from m/s to km/s
	}

	public void AddGravityObject(Rigidbody2D gravityObject, int asteroidBeltIndex = -1)
	{
		gravityObjects.Add(gravityObject, (asteroidBeltIndex >= 0 ? ((AsteroidRecord?)new AsteroidRecord(asteroidBeltIndex)) : null));
	}

	public void RemoveGravityObject(Rigidbody2D gravityObject)
	{
		deadGravityObjects.Remove(gravityObject);
		gravityObjects.Remove(gravityObject);
	}

	public Vector2 GetPosition()
	{
		return position;
	}

	public void SetAsteroidAltitudeConstraints(MinMax[] asteroidAltitudeConstraints)
	{
		this.asteroidAltitudeConstraints = new MinMax[asteroidAltitudeConstraints.Length];
		for(int i = 0; i < asteroidAltitudeConstraints.Length; ++i)
		{
			this.asteroidAltitudeConstraints[i] = new MinMax(asteroidAltitudeConstraints[i].Min * asteroidAltitudeConstraints[i].Min, asteroidAltitudeConstraints[i].Max * asteroidAltitudeConstraints[i].Max);
		}
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
}
