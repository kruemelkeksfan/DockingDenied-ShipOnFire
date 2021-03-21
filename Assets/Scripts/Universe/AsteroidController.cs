using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AsteroidController : MonoBehaviour
{
	// TODO: Inventory/Materials (read from JSON), calculate density from (Material Density + Stone Density) / 2
	// TODO: List of possible Materials for each Asteroid Size to avoid masses > 1000000 tons

	private static WaitForSeconds waitForHeightCheckInterval = null;

	[Tooltip("Determines how often the Height of the Asteroid Orbit is checked and corrected in Ingame Seconds")]
	[SerializeField] private float heightCheckInterval = 50.0f;
	[Tooltip("Height below the XY-Plane at which old Asteroids will enter the Atmossphere and decay")]
	[SerializeField] private float decayHeight = 100.0f;
	[Tooltip("Velocity with which Asteroids leave the orbital Plane when despawning")]
	[SerializeField] private float clearSpeed = 0.002f;
	[Tooltip("Height at which Asteroids should start burning up")]
	[SerializeField] private float atmossphereEntryHeight = 650.0f;
	[Tooltip("Height at which Asteroids should be completely destroyed")]
	[SerializeField] private float destructionHeight = 600.0f;
	[Tooltip("Valid Height Constraint for all Asteroids, regardless whether they were touched or not, Asteroids outside this Range will start decaying")]
	[SerializeField] private MinMax globalHeightConstraint = new MinMax(0.0f, 10000.0f);
	[Tooltip("Possible Densities for the Asteroid depending on its Material")]
	[SerializeField] private float[] densities = null;
	private new Transform transform = null;
	private new Rigidbody2D rigidbody = null;
	private Vector2 gravityWellPosition = Vector2.zero;
	private float burnUpDistance = 0.0f;

	private MinMax heightConstraints = new MinMax(0.0f, 10000.0f);
	public MinMax HeightConstraints
	{
		get
		{
			return HeightConstraints;
		}
		set
		{
			heightConstraints = new MinMax(value.Min * value.Min, value.Max * value.Max);
		}
	}
	public bool Touched { get; private set; } = false;

	private void Start()
	{
		if(waitForHeightCheckInterval == null)
		{
			waitForHeightCheckInterval = new WaitForSeconds(heightCheckInterval);
		}

		transform = gameObject.GetComponent<Transform>();
		rigidbody = gameObject.GetComponent<Rigidbody2D>();

		rigidbody.mass *= densities[Random.Range(0, densities.Length - 1)];

		atmossphereEntryHeight *= atmossphereEntryHeight;																											// Square to avoid Mathf.sqrt() later on
		destructionHeight *= destructionHeight;																														// Square to avoid Mathf.sqrt() later on
		globalHeightConstraint = new MinMax(globalHeightConstraint.Min * globalHeightConstraint.Min, globalHeightConstraint.Max * globalHeightConstraint.Max);		// Square to avoid Mathf.sqrt() later on

		burnUpDistance = atmossphereEntryHeight - destructionHeight;

		gravityWellPosition = GravityWellController.GetInstance().GetPosition();
		StartCoroutine(CheckHeight());
	}

	private void OnCollisionEnter2D(Collision2D collision)
	{
		AsteroidController otherAsteroidController = null;
		if(collision.gameObject.tag != "Asteroid"
			|| ((otherAsteroidController = collision.gameObject.GetComponent<AsteroidController>()) != null && otherAsteroidController.Touched))
		{
			Touched = true;
		}
	}

	private IEnumerator CheckHeight()
	{
		while(true)
		{
			yield return heightCheckInterval;

			float sqrOrbitalHeight = (gravityWellPosition - (Vector2)transform.position).sqrMagnitude;
			if(Time.timeScale > 0.0f
				&& ((!Touched && (sqrOrbitalHeight < heightConstraints.Min || sqrOrbitalHeight > heightConstraints.Max))
				|| sqrOrbitalHeight < globalHeightConstraint.Min || sqrOrbitalHeight > globalHeightConstraint.Max))
			{
				StartCoroutine(Despawn());
				yield break;
			}
		}
	}

	private IEnumerator Despawn()
	{
		gameObject.layer = 10;
		rigidbody.drag = 0.02f;

		Vector3 initialSize = transform.localScale;
		float sqrOrbitalHeight = 0.0f;
		while((sqrOrbitalHeight = (gravityWellPosition - (Vector2)transform.position).sqrMagnitude) > destructionHeight)
		{
			if(transform.position.z < decayHeight)
			{
				transform.position += new Vector3(0.0f, 0.0f, clearSpeed * Time.deltaTime);
			}

			if(sqrOrbitalHeight < atmossphereEntryHeight)
			{
				float currentSize = (sqrOrbitalHeight - destructionHeight) / burnUpDistance;
				transform.localScale = initialSize * currentSize;
			}

			yield return null;
		}

		--AsteroidSpawner.AsteroidCount;
		GameObject.Destroy(gameObject);
	}
}
