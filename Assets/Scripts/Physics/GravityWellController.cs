using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GravityWellController : MonoBehaviour
{
	public const double GRAVITY_CONSTANT = 0.000000000066743;

	private static GravityWellController instance = null;

	private Vector2 position = Vector2.zero;
	private float gravitationalParameter;
	private HashSet<Rigidbody2D> gravityObjects = null;

	public static GravityWellController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		position = transform.position;
		gravitationalParameter = (float)(gameObject.GetComponent<Rigidbody2D>().mass * GRAVITY_CONSTANT * 1000000000000000000000000.0); // Celestial Body Mass is given in 10^24 KGs, to accommodate them in a float
		gravityObjects = new HashSet<Rigidbody2D>();

		instance = this;
	}

	// TODO: Further Optimization Ideas:
	// (1) own Thread for Gravity Calculation (How to synchronize rigidbody.velocity and would it actually save performance?)
	// (2) go back to using a Coroutine with sparse Updates
	// (3) disable Renderers or even Renderers and Colliders of Asteroids far enough away from all Cameras (MMO Compatibility?)
	// (4) use simpler Mesh for Asteroids
	// (5) instead of simulating, calculate the whole Orbit on Start and after every Collision and just update the Velocity along the Path
	private void FixedUpdate()
	{
		foreach(Rigidbody2D gravityObject in gravityObjects)
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

	// Calculates the orbital Velocity to orbit at a given Height from the Vector from the GravityWell to the Orbiter.
	public Vector2 CalculateOrbitalVelocity(float targetHeight, Vector2 currentOrbitalDirection, float currentOrbitalHeight = -1.0f)
	{
		if(currentOrbitalHeight < 0.0f)
		{
			currentOrbitalHeight = currentOrbitalDirection.magnitude;
		}
		if(targetHeight <= 0.0f || currentOrbitalHeight <= 0.0f)
		{
			Debug.LogWarning("Invalid Call of CalculateOrbitalVelocity() in GravityController with either targetHeight " + targetHeight + " or " + " currentOrbitalHeight " + currentOrbitalHeight + " being 0!");
			return Vector2.zero;
		}

		// See: https://www.satsig.net/orbit-research/orbit-height-and-speed.htm
		// Velocity = square root of (Gravitational constant times Mass of main body / radius)
		return (new Vector2(-currentOrbitalDirection.y, currentOrbitalDirection.x) / currentOrbitalHeight)
			* Mathf.Sqrt(gravitationalParameter
			/ (targetHeight * 1000.0f))                                                                         // Convert from km to m
			/ 1000.0f;                                                                                          // Convert back from m/s to km/s
	}

	public void AddGravityObject(Rigidbody2D gravityObject)
	{
		gravityObjects.Add(gravityObject);
	}

	public void RemoveGravityObject(Rigidbody2D gravityObject)
	{
		gravityObjects.Remove(gravityObject);
	}

	public Vector2 GetPosition()
	{
		return position;
	}
}
