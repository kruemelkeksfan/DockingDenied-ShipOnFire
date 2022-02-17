using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GravityWellController : MonoBehaviour, IListener
{
	private class GravityObjectRecord
	{
		public GravityWellController gravityWellController = null;
		public Transform transform = null;
		public Rigidbody2D rigidbody = null;
		public double gravitationalParameter = 0.0;
		public double specificOrbitalEnergy = 0.0;
		public double eccentricityMagnitude = 0.0;
		public double semiMajorAxis = 0.0;
		public double semiMinorAxis = 0.0;
		public double phi = 0.0;
		public double phiSin = 0.0;
		public double phiCos = 1.0;
		public double startMeanAnomaly = 0.0;
		public Vector2Double orbitCenter = Vector2Double.zero;
		public double orbitalPeriod = 0.0;
		public bool clockwise = false;
		public float startTime = 0.0f;
		public double eccentricAnomalySin = 0.0;
		public double eccentricAnomalyCos = 1.0;
		public bool onRails = false;
		public bool isAsteroid = false;
		public MeshRenderer asteroidMeshRenderer = null;
		public MinMax altitudeConstraint = new MinMax();
		public bool touched = false;
		public GameObject spacecraftGameObject = null;
		public bool thrusting = false;
		public bool decaying = false;

		public GravityObjectRecord(GravityWellController gravityWellController, Transform transform, Rigidbody2D rigidbody, double gravitationalParameter)
		{
			this.gravityWellController = gravityWellController;
			this.transform = transform;
			this.rigidbody = rigidbody;
			this.gravitationalParameter = gravitationalParameter;

			spacecraftGameObject = transform.gameObject;
		}

		// TODO: Use Inheritance for Asteroid and Spacecraft GravityObjects
		public GravityObjectRecord(GravityWellController gravityWellController, Transform transform, Rigidbody2D rigidbody, double gravitationalParameter, MinMax altitudeConstraint)
		{
			this.gravityWellController = gravityWellController;
			this.transform = transform;
			this.rigidbody = rigidbody;
			this.gravitationalParameter = gravitationalParameter;

			isAsteroid = true;
			asteroidMeshRenderer = transform.gameObject.GetComponentInChildren<MeshRenderer>();
			this.altitudeConstraint = new MinMax(altitudeConstraint.min * altitudeConstraint.min, altitudeConstraint.max * altitudeConstraint.max);
		}

		public bool OnRail(Vector2Double globalPosition, Vector2Double startVelocity, float startTime)
		{
			// Don't on-Rail thrusting or dying Objects to avoid interfering with Unity Physics or Destruction Coroutines
			if(thrusting || decaying)
			{
				if(onRails)
				{
					UnRail();
				}

				return false;
			}

			double startPositionMagnitude = globalPosition.Magnitude();
			Vector2Double perpendicularPosition = Vector2Double.Perpendicular(globalPosition);
			double startVelocitySqrMagnitude = startVelocity.SqrMagnitude();
			this.startTime = startTime;

			// Determine orbital Direction
			clockwise = Vector2Double.Dot(startVelocity, perpendicularPosition) < 0.0;
			double gravitationalParameter = gravityWellController.GetGravitationalParameter();
			// https://en.wikipedia.org/wiki/Orbital_elements
			specificOrbitalEnergy = (startVelocitySqrMagnitude * 0.5) - (gravitationalParameter / startPositionMagnitude);
			Vector2Double eccentricity = ((globalPosition * ((startVelocitySqrMagnitude / gravitationalParameter) - (1.0 / startPositionMagnitude)))
				- (startVelocity * (Vector2Double.Dot(globalPosition, startVelocity) / gravitationalParameter)));
			eccentricityMagnitude = eccentricity.Magnitude();
			// https://en.wikipedia.org/wiki/Semi-major_and_semi-minor_axes#Energy;_calculation_of_semi-major_axis_from_state_vectors
			semiMajorAxis = -gravitationalParameter / (2.0 * specificOrbitalEnergy);
			// https://en.wikipedia.org/wiki/Orbital_period
			orbitalPeriod = 2.0 * Math.PI * Math.Sqrt((semiMajorAxis * semiMajorAxis * semiMajorAxis) / gravitationalParameter);
			// Check for valid Eccentricity
			if(eccentricityMagnitude > 0.02 && eccentricityMagnitude < (1.0 - 0.02))
			{
				semiMinorAxis = Math.Sqrt(1.0 - eccentricityMagnitude * eccentricityMagnitude) * semiMajorAxis;
				// phi is the Angle by which the Orbit is rotated around the Origin of the Coordinate System
				// Vector2.SignedAngle() returns the Result in Degrees, must convert to Radians!
				phi = (Math.PI * 2.0) - Vector2.SignedAngle(eccentricity, Vector2.right) * Mathf.Deg2Rad;
				phiSin = Math.Sin(phi);
				phiCos = Math.Cos(phi);

				// https://en.wikipedia.org/wiki/True_anomaly#From_state_vectors
				double trueAnomalyCos = Vector2Double.Dot(eccentricity, globalPosition) / (eccentricityMagnitude * startPositionMagnitude);
				// https://en.wikipedia.org/wiki/Eccentric_anomaly#From_the_true_anomaly
				double eccentricAnomaly = Math.Acos((eccentricityMagnitude + trueAnomalyCos) / (1.0 + eccentricityMagnitude * trueAnomalyCos));
				if(Vector2Double.Dot(eccentricity, perpendicularPosition) >= 0.0)
				{
					eccentricAnomaly = 2.0 * Math.PI - eccentricAnomaly;
				}
				eccentricAnomalySin = Math.Sin(eccentricAnomaly);
				eccentricAnomalyCos = Math.Cos(eccentricAnomaly);
				// https://en.wikipedia.org/wiki/Mean_anomaly#Formulae
				startMeanAnomaly = eccentricAnomaly - eccentricityMagnitude * eccentricAnomalySin;

				// Calculate Center Point of the Ellipse
				orbitCenter = -(new Vector2Double(
					(semiMajorAxis * phiCos * eccentricAnomalyCos - semiMinorAxis * phiSin * eccentricAnomalySin),
					(semiMajorAxis * phiSin * eccentricAnomalyCos + semiMinorAxis * phiCos * eccentricAnomalySin))
					- globalPosition);
			}
			// Circular Orbit
			else if(eccentricityMagnitude < (1.0 - 0.02 - double.Epsilon))
			{
				semiMinorAxis = semiMajorAxis;
				// https://en.wikipedia.org/wiki/Orbital_period
				// https://en.wikipedia.org/wiki/True_anomaly#From_state_vectors
				// Use Vector2.right as makeshift Eccentricity
				Vector2Double makeshiftEccentricity = new Vector2Double(1.0, 0.0);
				double trueAnomalyCos = Vector2Double.Dot(makeshiftEccentricity, globalPosition) / startPositionMagnitude;
				double eccentricAnomaly = Math.Acos(trueAnomalyCos);
				if(Vector2Double.Dot(makeshiftEccentricity, perpendicularPosition) >= 0.0)
				{
					eccentricAnomaly = 2.0 * Math.PI - eccentricAnomaly;
				}
				eccentricAnomalySin = Math.Sin(eccentricAnomaly);
				eccentricAnomalyCos = Math.Cos(eccentricAnomaly);
				startMeanAnomaly = eccentricAnomaly;
			}
			// Parabolic or hyperbolic Trajectory
			// https://en.wikipedia.org/wiki/Parabolic_trajectory
			// https://en.wikipedia.org/wiki/Hyperbolic_trajectory
			else
			{
				Debug.LogWarning("Parabolic or Hyperbolic Trajectory detected!");
				Debug.Log("Position: " + globalPosition);
				Debug.Log("Velocity: " + startVelocity);
				Debug.Log("Eccentricity: " + eccentricity);
				LogOrbitalParameters();

				UnRail();
				return false;
			}

			if(orbitCenter.x < double.MinValue || orbitCenter.x > double.MaxValue
					|| orbitCenter.y < double.MinValue || orbitCenter.y > double.MaxValue)
			{
				Debug.LogError("Faulty Orbital Parameters calculated!");
				Debug.Log("Position: " + globalPosition);
				Debug.Log("Velocity: " + startVelocity);
				Debug.Log("Eccentricity: " + eccentricity);
				LogOrbitalParameters();

				UnRail();
				return false;
			}

			rigidbody.simulated = false;
			onRails = true;

			return true;
		}

		public void UnRail()
		{
			if(onRails)
			{
				rigidbody.velocity = CalculateVelocity();
			}

			rigidbody.simulated = true;
			onRails = false;
		}

		public Vector2Double CalculateOnRailPosition(int maxIterations, double minPrecision)
		{
			// Calculate Eccentric- and Mean Anomaly from current Time
			// Calculate Mean Anomaly
			double meanAnomaly = ((((clockwise ? -(Time.time - startTime) : (Time.time - startTime)) * 2.0 * Math.PI) / orbitalPeriod) + startMeanAnomaly) % (2.0 * Math.PI);

			// Solve Kepler Equation and convert Mean Anomaly to Eccentric Anomaly
			// https://en.wikipedia.org/wiki/Kepler%27s_equation#Numerical_approximation_of_inverse_problem
			double eccentricAnomaly = meanAnomaly;
			// Skip Calculation if Orbit is (almost) circular
			if(eccentricityMagnitude > 0.02)
			{
				// Use different initial Guess for very elliptic Orbits
				if(eccentricityMagnitude > 0.8)
				{
					eccentricAnomaly = Math.PI;
				}

				// Newton-Raphson
				int i = 0;
				double lastEccentricAnomaly = 0.0;
				do
				{
					lastEccentricAnomaly = eccentricAnomaly;
					eccentricAnomaly = eccentricAnomaly
						- ((eccentricAnomaly - eccentricityMagnitude * eccentricAnomalySin - meanAnomaly)
						/ (1.0 - eccentricityMagnitude * eccentricAnomalyCos));

					eccentricAnomalySin = Math.Sin(eccentricAnomaly);
					eccentricAnomalyCos = Math.Cos(eccentricAnomaly);
				}
				while(i++ < maxIterations && !((eccentricAnomaly - lastEccentricAnomaly) < minPrecision && (eccentricAnomaly - lastEccentricAnomaly) > -minPrecision));

				if(i >= maxIterations)
				{
					Debug.LogWarning("Exceeded Max Iterations during Newton-Raphson!");
					Debug.Log("i: " + i);
					Debug.Log("Last: " + lastEccentricAnomaly);
					Debug.Log("Current: " + eccentricAnomaly);
					LogOrbitalParameters();

					UnRail();
				}
			}
			else
			{
				eccentricAnomalySin = Math.Sin(eccentricAnomaly);
				eccentricAnomalyCos = Math.Cos(eccentricAnomaly);
			}

			// Position Calculation
			// Using transform.position instead of rigidbody.centerOfMass, because the Difference is negligible
			// Plug in all Parameters in Parameter-Form of Ellipse-Equation
			// https://de.wikipedia.org/wiki/Ellipse#Ellipsengleichung_(Parameterform)
			return orbitCenter + new Vector2Double(
				(semiMajorAxis * phiCos * eccentricAnomalyCos - semiMinorAxis * phiSin * eccentricAnomalySin),
				(semiMajorAxis * phiSin * eccentricAnomalyCos + semiMinorAxis * phiCos * eccentricAnomalySin));
		}

		public Vector2Double CalculateVelocity()
		{
			// M = ((2 * pi) / T) * (t - t0)
			// M' = (2 * pi) / T
			// M = E - e * sin(E)
			// M' = (E - e * sin(E))'
			// M' = E' - e * cos(E) * E'
			// M' = E'(1 - e * cos(E))
			// E' = M' / (1 - e * cos(E))
			// E' = (2 * pi) / (T - T * e * cos(E))
			double derivedEccentricAnomaly = (2.0 * Math.PI) / (orbitalPeriod - orbitalPeriod * eccentricityMagnitude * eccentricAnomalyCos);
			return new Vector2Double(
				(-semiMajorAxis * phiCos * eccentricAnomalySin * derivedEccentricAnomaly - semiMinorAxis * phiSin * eccentricAnomalyCos * derivedEccentricAnomaly),
				(-semiMajorAxis * phiSin * eccentricAnomalySin * derivedEccentricAnomaly + semiMinorAxis * phiCos * eccentricAnomalyCos * derivedEccentricAnomaly));
		}

		private void LogOrbitalParameters()
		{
			Debug.Log("Gravitational Parameter: " + gravitationalParameter);
			Debug.Log("Specific Orbital Energy: " + specificOrbitalEnergy);
			Debug.Log("|Eccentricity|: " + eccentricityMagnitude);
			Debug.Log("Semi-Major Axis: " + semiMajorAxis);
			Debug.Log("Semi-Minor Axis: " + semiMinorAxis);
			Debug.Log("Phi: " + phi);
			Debug.Log("Orbit Center: " + orbitCenter);
			Debug.Log("Orbital Period: " + orbitalPeriod);
			Debug.Log("Clockwise: " + clockwise);
			Debug.Log("sin(phi): " + phiSin);
			Debug.Log("sin(eccenctricAnomaly): " + eccentricAnomalySin);
			Debug.Log("cos(phi): " + phiCos);
			Debug.Log("cos(eccenctricAnomaly): " + eccentricAnomalyCos);
		}
	}

	public const double GRAVITY_CONSTANT = 0.000000000066743;

	private static GravityWellController instance = null;
	private static WaitForSecondsRealtime waitForPositionCheckInterval = null;
	private static WaitForFixedUpdate waitForFixedUpdate = null;

	[Tooltip("The Mass of this Gravity Well in kg")]
	[SerializeField] private double mass = 6.2e+21;
	[Tooltip("Determines how often the Heights of all Gravity Objects Orbits are checked in Ingame Seconds")]
	[SerializeField] private float positionCheckInterval = 5.0f;
	[Tooltip("Sea level Height above the Planet Center")]
	[SerializeField] private float surfaceAltitude = 250000.0f;
	[SerializeField] private float pullUpWarningAltitude = 370000.0f;
	[Tooltip("Height at which Asteroids should start burning up")]
	[SerializeField] private float atmosphereEntryAltitude = 320000.0f;
	[Tooltip("Height at which Asteroids should be completely destroyed")]
	[SerializeField] private float destructionAltitude = 280000.0f;
	[Tooltip("Maximum Height for all Orbiters, regardless whether they were touched or not, unrailed Objects outside this Range will start decaying")]
	[SerializeField] private float maximumAltitude = 5000000.0f;
	[Tooltip("Atmospheric Density for Drag Calculation at Sea Level of the Planet")]
	[SerializeField] private float atmosphericDensity = 0.2f;
	[Tooltip("Scale Height is the Height above Sea Level at which Air Pressure is 1/e of the Air Pressure on the Surface")]
	[SerializeField] private float scaleAltitude = 10000.0f;
	[Tooltip("A Particle System to visualize Re-Entry Heat and Plasma")]
	[SerializeField] private ParticleSystem plasmaParticleSystemPrefab = null;
	[Tooltip("Minimum Drag for Plasma Particles to be created")]
	[SerializeField] private float plasmaMinDrag = 400.0f;
	[Tooltip("Maximum Distance of the Player from the local Origin, before the Origin will be moved")]
	[SerializeField] private float maxOriginDistance = 10000.0f;
	[Tooltip("Maximum Amount of Iterations for eccentricAnomaly Calculation")]
	[SerializeField] private int maxIterations = 200;
	[Tooltip("Target Precision for eccentricAnomaly Calculation")]
	[SerializeField] private double minPrecision = 0.0001;
	[Tooltip("Distance to the Player at which all Objects are un-railed")]
	[SerializeField] private float unrailDistance = 10000.0f;
	[Tooltip("Distance to the Player at which un-railed Objects are on-railed again")]
	[SerializeField] private float onrailDistance = 12000.0f;
	private Transform planetTransform = null;
	private SpawnController spawnController = null;
	private InfoController infoController = null;
	private GameObject localPlayerMainObject = null;
	private Transform localPlayerMainTransform = null;
	private double gravitationalParameter = 0.0f;
	private Dictionary<Rigidbody2D, GravityObjectRecord> gravityObjects = null;
	private float halfMaxAltitude = 0.0f;
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
			gravityObjects = new Dictionary<Rigidbody2D, GravityObjectRecord>();

			// Square Height Constraints to avoid Sqrt later on
			halfMaxAltitude = (maximumAltitude * 0.5f) * (maximumAltitude * 0.5f);
			pullUpWarningAltitude *= pullUpWarningAltitude;
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
		// Avoid Dictionary.Get() for Performance Reasons
		foreach(KeyValuePair<Rigidbody2D, GravityObjectRecord> gravityObject in gravityObjects)
		{
			// Buffer for Performance Reasons
			GravityObjectRecord gravityObjectValue = gravityObject.Value;
			Vector3 gravityObjectPosition = gravityObjectValue.transform.position;

			// Check onRails first, because we can simulate Gravity after unRailing, but we can not solve analytically after onRailing,
			// because rigidbody.position is updated after FixedUpdate() and we therefore have to use an obsolete Position for onRailing anyways
			// Also: onRailed Objects suffer less from skipped Frames anyways
			if(gravityObjectValue.onRails)
			{
				// TODO: Set a max Velocity Difference as [SerializeField], calculate next Check Time from max Velocity, Distance to closest Player and unrailDistance and skip Position Calculations until next Check Time
				if(gravityObjectValue.thrusting || ((Vector2)gravityObjectPosition - playerPosition).sqrMagnitude <= unrailDistance)
				{
					gravityObjectValue.UnRail();

					if(gravityObjectValue.isAsteroid)
					{
						gravityObjectValue.asteroidMeshRenderer.enabled = true;
					}
					else
					{
						foreach(MeshRenderer renderer in gravityObjectValue.spacecraftGameObject.GetComponentsInChildren<MeshRenderer>())
						{
							renderer.enabled = true;
						}
					}
				}
				else
				{
					// Analytical Gravity Solution
					Vector2Double position = GlobalToLocalPosition(gravityObjectValue.CalculateOnRailPosition(maxIterations, minPrecision));
					gravityObjectValue.transform.position = new Vector3((float)position.x, (float)position.y, gravityObjectPosition.z);
				}
			}

			// Check again, in case we unrailed above, to avoid skipping a Frame
			if(!gravityObjectValue.onRails)
			{
				// Use Time from last Frame, since this Frames rigidbody.velocity and Forces have not been applied by the Physics Engine yet
				if(((Vector2)gravityObjectPosition - playerPosition).sqrMagnitude >= onrailDistance
					&& gravityObjectValue.OnRail(LocalToGlobalPosition(gravityObjectPosition), gravityObject.Key.velocity, Time.time - Time.fixedDeltaTime))
				{
					if(gravityObjectValue.isAsteroid)
					{
						gravityObjectValue.asteroidMeshRenderer.enabled = false;
					}
					else
					{
						foreach(MeshRenderer renderer in gravityObjectValue.spacecraftGameObject.GetComponentsInChildren<MeshRenderer>())
						{
							renderer.enabled = false;
						}
					}
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
			gravityObjectValue.thrusting = false;
		}
	}

	public void Notify()
	{
		Spacecraft localPlayerMainSpacecraft = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft();
		localPlayerMainObject = localPlayerMainSpacecraft.gameObject;
		localPlayerMainTransform = localPlayerMainSpacecraft.GetTransform();
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
				GravityObjectRecord objectRecord = gravityObjects[gravityObject];

				if(objectRecord.onRails)
				{
					// Objects on-Rails are not Origin-shifted and have reasonably stable Orbits
					continue;
				}

				// Origin Shift
				if(originShifted)
				{
					objectRecord.transform.position -= (Vector3)originShift;
				}

				double sqrOrbitalAltitude = LocalToGlobalPosition(objectRecord.transform.position).SqrMagnitude();
				if(((objectRecord.isAsteroid && !objectRecord.touched
					&& (sqrOrbitalAltitude < objectRecord.altitudeConstraint.min || sqrOrbitalAltitude > objectRecord.altitudeConstraint.max))
					|| sqrOrbitalAltitude < atmosphereEntryAltitude || sqrOrbitalAltitude > maximumAltitude
					|| (gravityObject.gameObject == localPlayerMainObject
					&& (sqrOrbitalAltitude < pullUpWarningAltitude || sqrOrbitalAltitude > halfMaxAltitude)))
					&& !objectRecord.decaying)
				{
					if(gravityObject.gameObject == localPlayerMainObject)
					{
						// TODO: Check Periapsis instead of current Height
						if(sqrOrbitalAltitude < pullUpWarningAltitude && sqrOrbitalAltitude >= atmosphereEntryAltitude)
						{
							if(infoController.GetMessageCount() <= 0)
							{
								infoController.AddMessage("Altitude critical, pull up!");
							}
							continue;
						}
						else if(sqrOrbitalAltitude > maximumAltitude)
						{
							if(infoController.GetMessageCount() <= 0)
							{
								infoController.AddMessage("Leaving Signal Range, get back to the Planet!");
							}
							continue;
						}
						else if(sqrOrbitalAltitude > halfMaxAltitude)
						{
							if(infoController.GetMessageCount() <= 0)
							{
								infoController.AddMessage("Signal is getting weaker...");
							}
							continue;
						}
					}

					if(sqrOrbitalAltitude < atmosphereEntryAltitude)
					{
						deorbitObjects.Add(gravityObject);
					}
					else
					{
						despawnObjects.Add(gravityObject);
					}

					objectRecord.decaying = true;
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
		}
	}

	private IEnumerator Deorbit(Rigidbody2D gravityObject)
	{
		GravityObjectRecord objectRecord = gravityObjects[gravityObject];
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
				// Drag based on an Approximation of atmospheric Density
				// https://en.wikipedia.org/wiki/Scale_height#Scale_height_used_in_a_simple_atmospheric_pressure_model
				float airPressure = atmosphericDensity * Mathf.Exp(-(Mathf.Sqrt(sqrOrbitalAltitude) - surfaceAltitude) / scaleAltitude);
				// https://en.wikipedia.org/wiki/Drag_(physics)#The_drag_equation
				// Drag Coefficient for a Cube is roughly 1.0, if we ignore that Drag Coefficient is dependent on the Reynolds Number
				float drag = (0.5f * airPressure * gravityObject.velocity.sqrMagnitude * shipRadius * shipRadius) / 1000.0f;                    // Convert kg*m/s^2 to t*m/s^2
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
								gravityObject.GetComponent<Spacecraft>().Kill();
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
				gravityObjects[gravityObject].decaying = false;
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

	public void AddGravityObject(Rigidbody2D gravityObject, MinMax asteroidBeltHeight = new MinMax())
	{
		Transform gravityObjectTransform = gravityObject.GetComponent<Transform>();
		gravityObjects.Add(gravityObject, ((asteroidBeltHeight.min != 0.0f || asteroidBeltHeight.max != 0.0f) ?
			new GravityObjectRecord(this, gravityObjectTransform, gravityObject.GetComponent<Rigidbody2D>(), gravitationalParameter, asteroidBeltHeight)
			: new GravityObjectRecord(this, gravityObjectTransform, gravityObject.GetComponent<Rigidbody2D>(), gravitationalParameter)));
		gravityObject.velocity = CalculateOptimalOrbitalVelocity(gravityObject);
	}

	public void RemoveGravityObject(Rigidbody2D gravityObject)
	{
		gravityObjects.Remove(gravityObject);
	}

	public void MarkSpacecraftThrusting(Rigidbody2D spacecraft, bool thrusting)
	{
		if(!gravityObjects[spacecraft].isAsteroid)
		{
			gravityObjects[spacecraft].thrusting = thrusting;
		}
	}

	public bool MarkAsteroidTouched(Rigidbody2D asteroid, Rigidbody2D otherObject)
	{
		if(gravityObjects[asteroid].isAsteroid)
		{
			if(!gravityObjects[otherObject].isAsteroid || gravityObjects[otherObject].touched)
			{
				gravityObjects[asteroid].touched = true;

				return true;
			}
			else
			{
				return false;
			}
		}
		else
		{
			return false;
		}
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
