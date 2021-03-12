using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GravityController : MonoBehaviour
{
	private new Transform transform = null;
	private new Rigidbody2D rigidbody = null;
	private GravityWellController gravityWell = null;

	private void Awake()
	{
		transform = gameObject.GetComponent<Transform>();
		rigidbody = gameObject.GetComponent<Rigidbody2D>();
		gravityWell = GravityWellController.GetInstance();
	}

	private void Start()
	{
		if(gravityWell == null)										// If Gravity Well and this Gravity Controller got spawned at the same Time and therefore gravityWell could not be set in Awake()
		{
			gravityWell = GravityWellController.GetInstance();
		}
		gravityWell.AddGravityObject(rigidbody);
	}

	private void OnDestroy()
	{
		gravityWell.RemoveGravityObject(rigidbody);
	}

	// Calculates the required orbital Velocity for a circular Orbit at the current Height of this Orbiter.
	public Vector2 CalculateOptimalOrbitalVelocity()
	{
		Vector2 orbitalDirection = gravityWell.GetPosition() - (Vector2) transform.position;
		float orbitalHeight = orbitalDirection.magnitude;
		Vector2 orbitalVelocity =  gravityWell.CalculateOrbitalVelocity(orbitalHeight, orbitalDirection, orbitalHeight);

		if(Vector2.Dot(rigidbody.velocity, orbitalVelocity) > 0.0f)															// Turn the Target Velocity around, if the Orbiter is already going into the other Direction
		{
			return orbitalVelocity;
		}
		else
		{
			return -orbitalVelocity;
		}
	}

	public Vector2 CalculateApsides()
	{
		// https://space.stackexchange.com/questions/4727/how-to-calculate-apoapsis-of-sub-orbital-trajectory

		return new Vector2(0.0f, 0.0f);
	}

	public void DrawOrbit()
	{
		Vector2 apsides = CalculateApsides();

		// https://www.quora.com/How-do-I-calculate-the-semi-minor-axis-of-an-orbit?share=1
		// https://answers.unity.com/questions/631201/draw-an-ellipse-in-unity-3d.html
	}
}
