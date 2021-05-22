using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AsteroidSpawner : MonoBehaviour
{
	public static uint AsteroidCount { get; set; } = 0;

	private static WaitForSeconds waitForSpawnInterval = null;

	[Tooltip("Prefab for new Asteroids")]
	[SerializeField] private GravityController[] asteroidPrefabs = null;
	// TODO: Inventory/Materials (read from JSON), calculate density from (Material Density + Stone Density) / 2
	// TODO: List of possible Materials for each Asteroid Size to avoid masses > 1000000 tons
	[Tooltip("Possible Densities for Asteroids depending on their Material")]
	[SerializeField] private float[] densities = { };
	[Tooltip("Maximum Number of Asteroids allowed in the Scene at once")]
	[SerializeField] private uint maxAsteroids = 200;
	[Tooltip("Determines how often new Asteroids can be spawned in Ingame Seconds")]
	[SerializeField] private float spawnInterval = 5.0f;
	[Tooltip("A Set of Spawn Areas for Asteroids defined by Minimum and Maximum Orbit Heights")]
	[SerializeField] private MinMax[] asteroidBeltHeights = null;
	[Tooltip("The clear area which is required for a new Asteroid to spawn at a specific Position")]
	[SerializeField] private float spawnAreaRadius = 10.0f;
	[Tooltip("Height above the XY-Plane at which new Asteroids spawn before descending into the XY-Plane")]
	[SerializeField] private float spawnHeight = 1000.0f;
	[Tooltip("Approach Velocity of freshly spawned Asteroids")]
	[SerializeField] private float approachSpeed = 0.2f;
	private GravityWellController gravityWellController = null;
	private float[] asteroidBeltChances = null;
	private float totalAsteroidBeltChances = 0.0f;
	private Collider2D[] overlapColliders = null;

	private void Start()
	{
		if(waitForSpawnInterval == null)
		{
			waitForSpawnInterval = new WaitForSeconds(spawnInterval);
		}

		gravityWellController = GravityWellController.GetInstance();
		gravityWellController.SetAsteroidAltitudeConstraints(asteroidBeltHeights);

		asteroidBeltChances = new float[asteroidBeltHeights.Length];
		for(int i = 0; i < asteroidBeltHeights.Length; ++i)
		{
			float asteroidBeltArea = Mathf.PI * (asteroidBeltHeights[i].Max * asteroidBeltHeights[i].Max) - (asteroidBeltHeights[i].Min * asteroidBeltHeights[i].Min);      // Ring Area = (PI * R1^2) - (PI * R2^2)
			asteroidBeltChances[i] = totalAsteroidBeltChances + asteroidBeltArea;
			totalAsteroidBeltChances += asteroidBeltArea;
		}

		overlapColliders = new Collider2D[10];

		StartCoroutine(SpawnUpdate());
	}

	private IEnumerator SpawnUpdate()
	{
		while(true)
		{
			yield return waitForSpawnInterval;

			if(AsteroidCount < maxAsteroids)
			{
				float beltRandom = Random.Range(0.0f, totalAsteroidBeltChances);
				for(int i = 0; i < asteroidBeltHeights.Length; ++i)
				{
					if(beltRandom <= asteroidBeltChances[i])
					{
						StartCoroutine(SpawnAsteroid(i));
						break;
					}
				}
			}
		}
	}

	private IEnumerator SpawnAsteroid(int asteroidBeltIndex)
	{
		float orbitalAngle = Random.Range(0.0f, Mathf.PI * 2.0f);
		float orbitalHeight = Random.Range(asteroidBeltHeights[asteroidBeltIndex].Min, asteroidBeltHeights[asteroidBeltIndex].Max);
		Vector2 position = new Vector2(Mathf.Sin(orbitalAngle), -Mathf.Cos(orbitalAngle)) * orbitalHeight;
		int overlapCollidersCount = Physics2D.OverlapCircleNonAlloc(position, spawnAreaRadius, overlapColliders);
		for(int i = 0; i < overlapCollidersCount; ++i)
		{
			if(overlapColliders[i].gameObject.layer == 10)
			{
				yield break;
			}
		}

		++AsteroidCount;
		GravityController asteroid = GameObject.Instantiate<GravityController>(asteroidPrefabs[Random.Range(0, asteroidPrefabs.Length - 1)],
			new Vector3(position.x, position.y, -spawnHeight), Quaternion.identity);
		asteroid.gameObject.layer = 8;

		Rigidbody2D rigidbody = asteroid.GetComponent<Rigidbody2D>();
		gravityWellController.AddGravityObject(rigidbody, asteroidBeltIndex);
		rigidbody.mass *= densities[Random.Range(0, densities.Length)];

		asteroid.SetOptimalOrbitalVelocity();

		Transform asteroidTransform = asteroid.GetComponent<Transform>();
		float asteroidExtents = asteroidTransform.GetComponent<Collider2D>().bounds.extents.x;
		Vector3 asteroidSize = asteroidTransform.localScale;
		float asteroidHeight = -asteroid.transform.position.z;
		while(asteroidHeight > 0.0002f && asteroid.gameObject.layer != 9)                                                                   // Check if Asteroid is still in Approach and not decaying yet
		{
			if(asteroidHeight < asteroidExtents)
			{
				asteroid.gameObject.layer = 10;
			}

			asteroidTransform.position += new Vector3(0.0f, 0.0f, asteroidHeight * approachSpeed * Time.deltaTime);
			asteroidHeight = -asteroidTransform.position.z;
			float currentSize = 1.0f - (asteroidHeight / spawnHeight);
			asteroidTransform.localScale = asteroidSize * currentSize;

			yield return null;
		}

		asteroidTransform.position = new Vector3(asteroidTransform.position.x, asteroidTransform.position.y, 0.0f);
		asteroidTransform.localScale = asteroidSize;
	}
}
