using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AsteroidSpawner : MonoBehaviour
{
	private static AsteroidSpawner instance = null;
	private static WaitForSeconds waitForSpawnInterval = null;

	[Tooltip("Prefab for new Asteroids")]
	[SerializeField] private Rigidbody2D[] asteroidPrefabs = null;
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
	private GravityWellController gravityWellController = null;
	private SpawnController spawnController = null;
	private float[] asteroidBeltChances = null;
	private float totalAsteroidBeltChances = 0.0f;
	private uint asteroidCount = 0;

	public static AsteroidSpawner GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		instance = this;
	}

	private void Start()
	{
		if(waitForSpawnInterval == null)
		{
			waitForSpawnInterval = new WaitForSeconds(spawnInterval);
		}

		gravityWellController = GravityWellController.GetInstance();
		spawnController = SpawnController.GetInstance();

		asteroidBeltChances = new float[asteroidBeltHeights.Length];
		for(int i = 0; i < asteroidBeltHeights.Length; ++i)
		{
			float asteroidBeltArea = Mathf.PI * (asteroidBeltHeights[i].max * asteroidBeltHeights[i].max) - (asteroidBeltHeights[i].min * asteroidBeltHeights[i].min);      // Ring Area = (PI * R1^2) - (PI * R2^2)
			asteroidBeltChances[i] = totalAsteroidBeltChances + asteroidBeltArea;
			totalAsteroidBeltChances += asteroidBeltArea;
		}

		StartCoroutine(SpawnUpdate());
	}

	private IEnumerator SpawnUpdate()
	{
		while(true)
		{
			yield return waitForSpawnInterval;

			if(asteroidCount < maxAsteroids)
			{
				float beltRandom = Random.Range(0.0f, totalAsteroidBeltChances);
				for(int i = 0; i < asteroidBeltHeights.Length; ++i)
				{
					if(beltRandom <= asteroidBeltChances[i])
					{
						StartCoroutine(spawnController.SpawnObject(asteroidPrefabs[Random.Range(0, asteroidPrefabs.Length - 1)], Vector2.zero, asteroidBeltHeights[i], 10));
						break;
					}
				}
			}
		}
	}

	public void AddAsteroid(Rigidbody2D asteroid, MinMax spawnRange)
	{
		asteroid.mass *= densities[Random.Range(0, densities.Length)];
		gravityWellController.AddGravityObject(asteroid, spawnRange);

		++asteroidCount;
	}

	public void RemoveAsteroid(Rigidbody2D asteroid)
	{
		gravityWellController.RemoveGravityObject(asteroid);

		--asteroidCount;
	}
}
