using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AsteroidSpawner : MonoBehaviour
{
	public static uint AsteroidCount { get; set; } = 0;

	private static WaitForSeconds waitForSpawnInterval = null;

	[Tooltip("Prefab for new Asteroids")]
	[SerializeField] private GravityController[] asteroidPrefabs = null;
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
	private float[] asteroidBeltChances = null;
	private float totalAsteroidBeltChances = 0.0f;
	private Collider2D[] overlapColliders = null;

	private void Start()
	{
		if(waitForSpawnInterval == null)
		{
			waitForSpawnInterval = new WaitForSeconds(spawnInterval);
		}

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

			if(Time.timeScale > 0.0f && AsteroidCount < maxAsteroids)
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
			if(overlapColliders[i].tag == "Asteroid")
			{
				yield break;
			}
		}

		++AsteroidCount;
		GravityController asteroid = GameObject.Instantiate<GravityController>(asteroidPrefabs[Random.Range(0, asteroidPrefabs.Length - 1)],
			new Vector3(position.x, position.y, -spawnHeight), Quaternion.identity);
		asteroid.GetComponent<AsteroidController>().HeightConstraints = asteroidBeltHeights[asteroidBeltIndex];
		asteroid.GetComponent<Rigidbody2D>().velocity = asteroid.CalculateOptimalOrbitalVelocity();											// GetComponent(), because Start() has not yet been called on Asteroid

		Transform asteroidTransform = asteroid.transform;
		float asteroidExtents = asteroidTransform.GetComponent<Collider2D>().bounds.extents.x;
		Vector3 asteroidSize = asteroidTransform.localScale;
		float asteroidHeight = -asteroid.transform.position.z;
		while(asteroidHeight > 0.0002f && asteroid.gameObject.layer != 10)																	// Check if Asteroid is still in Approach and not decaying yet
		{
			if(Time.timeScale > 0.0f)
			{
				if(asteroidHeight < asteroidExtents)
				{
					asteroid.gameObject.layer = 9;
				}

				asteroidTransform.position += new Vector3(0.0f, 0.0f, asteroidHeight * approachSpeed * Time.deltaTime);
				asteroidHeight = -asteroid.transform.position.z;
				float currentSize = 1.0f - (asteroidHeight / spawnHeight);
				asteroidTransform.localScale = asteroidSize * currentSize;
			}

			yield return null;
		}

		asteroidTransform.position = new Vector3(asteroidTransform.position.x, asteroidTransform.position.y, 0.0f);
		asteroidTransform.localScale = asteroidSize;
	}
}
