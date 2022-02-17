using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AsteroidController : GravityObjectController
{
	private MinMax altitudeConstraint = new MinMax();
	private MeshRenderer meshRenderer = null;
	private bool touched = false;

	protected override void Awake()
	{
		base.Awake();

		meshRenderer = transform.gameObject.GetComponentInChildren<MeshRenderer>();
	}

	private void OnDestroy()
	{
		if(rigidbody != null)
		{
			AsteroidSpawner.GetInstance()?.RemoveAsteroid(rigidbody);
		}
	}

	private void OnCollisionEnter2D(Collision2D collision)
	{
		if(!touched)
		{
			AsteroidController otherAsteroid = collision.gameObject.GetComponent<AsteroidController>();
			// Set touched true if the other Object is either not an Asteroid or a touched Asteroid
			if(otherAsteroid == null || otherAsteroid.touched)
			{
				gameObject.name = "TOUCHED, unlike me"; // TODO: Remove after testing
				touched = true;
			}
		}
	}

	public override void ToggleRenderer(bool activateRenderer)
	{
		meshRenderer.enabled = activateRenderer;
	}

	public bool IsTouched()
	{
		return touched;
	}

	public MinMax GetAltitudeConstraint()
	{
		return altitudeConstraint;
	}

	public void SetAltitudeConstraint(MinMax altitudeConstraint)
	{
		this.altitudeConstraint = altitudeConstraint;
	}
}
