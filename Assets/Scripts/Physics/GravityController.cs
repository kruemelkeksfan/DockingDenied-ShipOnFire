using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// TODO: Integrate all this Functionality in GravityWellController (additional Rigidbody2D Parameter for the Methods as Object ID), make GravityWellController singleton, delete this class and rename GravityWellController to GravityController
public class GravityController : MonoBehaviour
{
	private new Transform transform = null;
	private new Rigidbody2D rigidbody = null;
	private GravityWellController gravityWell = null;

	private void Awake()
	{
		transform = gameObject.GetComponent<Transform>();
		rigidbody = gameObject.GetComponent<Rigidbody2D>();
	}

	public void DrawOrbit()
	{
		Vector2 apsides = CalculateApsides();

		// https://www.quora.com/How-do-I-calculate-the-semi-minor-axis-of-an-orbit?share=1
		// https://answers.unity.com/questions/631201/draw-an-ellipse-in-unity-3d.html
	}

	// TODO: Remove
	// Calculates the required orbital Velocity for a circular Orbit at the current Height of this Orbiter.
	public Vector2 CalculateOptimalOrbitalVelocity()
	{
		if(gravityWell == null)
		{
			gravityWell = GravityWellController.GetInstance();
		}

		return gravityWell.CalculateOptimalOrbitalVelocity(rigidbody);
	}

	public Vector2 CalculateApsides()
	{
		// https://space.stackexchange.com/questions/4727/how-to-calculate-apoapsis-of-sub-orbital-trajectory

		return new Vector2(0.0f, 0.0f);
	}

	public void SetOptimalOrbitalVelocity()
	{
		rigidbody.velocity = CalculateOptimalOrbitalVelocity();
	}
}
