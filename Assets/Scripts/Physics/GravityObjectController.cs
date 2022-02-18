﻿using System;
using UnityEngine;

public class GravityObjectController : MonoBehaviour
{
	[Tooltip("Maximum Amount of Iterations for eccentricAnomaly Calculation")]
	[SerializeField] private int maxIterations = 200;
	[Tooltip("Target Precision for eccentricAnomaly Calculation")]
	[SerializeField] private double minPrecision = 0.0001;
	protected GravityWellController gravityWellController = null;
	protected new Transform transform = null;
	protected new Rigidbody2D rigidbody = null;
	private double specificOrbitalEnergy = 0.0;
	private double eccentricityMagnitude = 0.0;
	private double semiMajorAxis = 0.0;
	private double semiMinorAxis = 0.0;
	private double phi = 0.0;
	private double phiSin = 0.0;
	private double phiCos = 1.0;
	private double startMeanAnomaly = 0.0;
	private Vector2Double orbitCenter = Vector2Double.zero;
	private double orbitalPeriod = 0.0;
	private bool clockwise = false;
	private float startTime = 0.0f;
	private double eccentricAnomalySin = 0.0;
	private double eccentricAnomalyCos = 1.0;
	private bool onRails = false;
	private bool decaying = false;

	protected virtual void Awake()
	{
		transform = gameObject.GetComponent<Transform>();
		rigidbody = gameObject.GetComponent<Rigidbody2D>();
	}

	protected virtual void Start()
	{
		gravityWellController = GravityWellController.GetInstance();
	}

	public bool OnRail(Vector2Double globalPosition, Vector2Double startVelocity, float startTime)
	{
		if(CalculateOrbitalElements(globalPosition, startVelocity, startTime))
		{
			rigidbody.simulated = false;
			onRails = true;

			return true;
		}
		else
		{
			// Don't give time, because it would not be used and startTime is obsolete
			UnRail();
		}

		return false;
	}

	public void UnRail(float time = -1.0f)
	{
		if(onRails)
		{
			if(time < 0.0f)
			{
				time = Time.time;
			}

			rigidbody.velocity = CalculateVelocity(time);
		}

		rigidbody.simulated = true;
		onRails = false;
	}

	public bool CalculateOrbitalElements(Vector2Double globalPosition, Vector2Double startVelocity, float startTime)
	{
		double globalPositionMagnitude = globalPosition.Magnitude();
		Vector2Double perpendicularPosition = Vector2Double.Perpendicular(globalPosition);
		double startVelocitySqrMagnitude = startVelocity.SqrMagnitude();
		this.startTime = startTime;

		// Determine orbital Direction
		clockwise = Vector2Double.Dot(startVelocity, perpendicularPosition) < 0.0;
		double gravitationalParameter = gravityWellController.GetGravitationalParameter();
		// https://en.wikipedia.org/wiki/Orbital_elements
		specificOrbitalEnergy = (startVelocitySqrMagnitude * 0.5) - (gravitationalParameter / globalPositionMagnitude);
		Vector2Double eccentricity = ((globalPosition * ((startVelocitySqrMagnitude / gravitationalParameter) - (1.0 / globalPositionMagnitude)))
			- (startVelocity * (Vector2Double.Dot(globalPosition, startVelocity) / gravitationalParameter)));
		eccentricityMagnitude = eccentricity.Magnitude();
		// Check for valid Eccentricity
		if(eccentricityMagnitude > 0.01 && eccentricityMagnitude < (1.0 - 0.01))
		{
			// https://en.wikipedia.org/wiki/Semi-major_and_semi-minor_axes#Energy;_calculation_of_semi-major_axis_from_state_vectors
			semiMajorAxis = -gravitationalParameter / (2.0 * specificOrbitalEnergy);
			semiMinorAxis = Math.Sqrt(1.0 - eccentricityMagnitude * eccentricityMagnitude) * semiMajorAxis;
			// phi is the Angle by which the Orbit is rotated around the Origin of the Coordinate System
			// Vector2.SignedAngle() returns the Result in Degrees, must convert to Radians!
			phi = (Math.PI * 2.0) - Vector2.SignedAngle(eccentricity, Vector2.right) * Mathf.Deg2Rad;
			phiSin = Math.Sin(phi);
			phiCos = Math.Cos(phi);

			// https://en.wikipedia.org/wiki/True_anomaly#From_state_vectors
			double trueAnomalyCos = Vector2Double.Dot(eccentricity, globalPosition) / (eccentricityMagnitude * globalPositionMagnitude);
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
		else if(eccentricityMagnitude < (1.0 - 0.01 - double.Epsilon))
		{
			semiMajorAxis = globalPositionMagnitude;
			semiMinorAxis = globalPositionMagnitude;
			// https://en.wikipedia.org/wiki/True_anomaly#From_state_vectors
			// Use Vector2.right as makeshift Eccentricity
			Vector2Double makeshiftEccentricity = new Vector2Double(1.0, 0.0);
			double trueAnomalyCos = Vector2Double.Dot(makeshiftEccentricity, globalPosition) / globalPositionMagnitude;
			double eccentricAnomaly = Math.Acos(trueAnomalyCos);
			if(Vector2Double.Dot(makeshiftEccentricity, perpendicularPosition) >= 0.0)
			{
				eccentricAnomaly = 2.0 * Math.PI - eccentricAnomaly;
			}
			eccentricAnomalySin = Math.Sin(eccentricAnomaly);
			eccentricAnomalyCos = Math.Cos(eccentricAnomaly);
			startMeanAnomaly = eccentricAnomaly;

			phi = 0.0f;
			phiSin = 0.0f;
			phiCos = 1.0f;

			orbitCenter = Vector2Double.zero;
		}
		// Parabolic or hyperbolic Trajectory
		// https://en.wikipedia.org/wiki/Parabolic_trajectory
		// https://en.wikipedia.org/wiki/Hyperbolic_trajectory
		else
		{
			/*
			Debug.LogWarning("Parabolic or Hyperbolic Trajectory detected!");
			Debug.Log("Position: " + globalPosition);
			Debug.Log("Velocity: " + startVelocity);
			Debug.Log("Eccentricity: " + eccentricity);
			LogOrbitalParameters();
			*/

			return false;
		}

		// https://en.wikipedia.org/wiki/Orbital_period
		orbitalPeriod = 2.0 * Math.PI * Math.Sqrt((semiMajorAxis * semiMajorAxis * semiMajorAxis) / gravitationalParameter);

		if(orbitCenter.x < double.MinValue || orbitCenter.x > double.MaxValue
				|| orbitCenter.y < double.MinValue || orbitCenter.y > double.MaxValue)
		{
			Debug.LogError("Faulty Orbital Parameters calculated!");
			Debug.Log("Position: " + globalPosition);
			Debug.Log("Velocity: " + startVelocity);
			Debug.Log("Eccentricity: " + eccentricity);
			LogOrbitalParameters();

			return false;
		}

		return true;
	}

	public Vector2Double CalculateOnRailPosition(float time)
	{
		CalculateEccentricAnomaly(time);

		// Position Calculation
		// Using transform.position instead of rigidbody.centerOfMass, because the Difference is negligible
		// Plug in all Parameters in Parameter-Form of Ellipse-Equation
		// https://de.wikipedia.org/wiki/Ellipse#Ellipsengleichung_(Parameterform)
		return orbitCenter + new Vector2Double(
			(semiMajorAxis * phiCos * eccentricAnomalyCos - semiMinorAxis * phiSin * eccentricAnomalySin),
			(semiMajorAxis * phiSin * eccentricAnomalyCos + semiMinorAxis * phiCos * eccentricAnomalySin));
	}

	public Vector2Double CalculateVelocity(float time)
	{
		CalculateEccentricAnomaly(time);

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

	private void CalculateEccentricAnomaly(float time)
	{
		// Calculate Eccentric- and Mean Anomaly from current Time
		// Calculate Mean Anomaly
		double meanAnomaly = ((((clockwise ? -(time - startTime) : (time - startTime)) * 2.0 * Math.PI) / orbitalPeriod) + startMeanAnomaly) % (2.0 * Math.PI);

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
	}

	public virtual void ToggleRenderer(bool activateRenderer)
	{

	}

	public bool IsOnRails()
	{
		return onRails;
	}

	public bool IsDecaying()
	{
		return decaying;
	}

	public Transform GetTransform()
	{
		return transform;
	}

	public Rigidbody2D GetRigidbody()
	{
		return rigidbody;
	}

	public double GetOrbitalPeriod()
	{
		return orbitalPeriod;
	}

	public void SetDecaying(bool decaying)
	{
		this.decaying = decaying;
	}
}
