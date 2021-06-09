using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnController : MonoBehaviour
{
	private static SpawnController instance = null;

	[Tooltip("The clear area which is required for a new Object to spawn at a specific Position")]
	[SerializeField] private float spawnAreaRadius = 2.0f;
	[Tooltip("Height above the XY-Plane at which new Objects spawn before descending into the XY-Plane")]
	[SerializeField] private float spawnHeight = -10.0f;
	[Tooltip("Approach Velocity of freshly spawned Objects")]
	[SerializeField] private float approachSpeed = 0.02f;
	private AsteroidSpawner asteroidSpawner = null;
	private GravityWellController gravityWellController = null;
	private int layerMask = Physics2D.DefaultRaycastLayers;

	public static SpawnController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		layerMask = LayerMask.GetMask(new string[] { "Approaching", "Asteroids", "Spacecraft" });

		instance = this;
	}

	private void Start()
	{
		asteroidSpawner = AsteroidSpawner.GetInstance();
		gravityWellController = GravityWellController.GetInstance();
	}

	public IEnumerator SpawnObject(Rigidbody2D spawnObjectPrefab, Vector2 spawnCenter, MinMax spawnRange, int layer, QuestManager.Quest quest = null)
	{
		float orbitalAngle = Random.Range(0.0f, Mathf.PI * 2.0f);
		float orbitalAltitude = Random.Range(spawnRange.min, spawnRange.max);
		Vector2 position = spawnCenter + (new Vector2(Mathf.Sin(orbitalAngle), -Mathf.Cos(orbitalAngle)) * orbitalAltitude);
		if(Physics2D.OverlapCircle(position, spawnAreaRadius, layerMask) != null)
		{
			yield break;
		}

		Rigidbody2D spawnObject = GameObject.Instantiate<Rigidbody2D>(spawnObjectPrefab, new Vector3(position.x, position.y, spawnHeight), Quaternion.identity);
		spawnObject.gameObject.layer = 8;
		if(layer == 10)
		{
			asteroidSpawner.AddAsteroid(spawnObject, spawnRange);
		}
		if(quest != null)
		{
			spawnObject.GetComponent<QuestVesselController>().SetQuest(quest);
		}
		spawnObject.velocity = gravityWellController.CalculateOptimalOrbitalVelocity(spawnObject);

		Transform spawnObjectTransform = spawnObject.GetComponent<Transform>();
		float spawnObjectExtents = Mathf.Max(spawnObjectTransform.GetComponent<Collider2D>().bounds.extents.x, 0.01f);
		Vector3 spawnObjectSize = spawnObjectTransform.localScale;
		while(spawnObject.transform.position.z < -0.0002f && spawnObject.gameObject.layer != 9)                                                                   // Check if Asteroid is still in Approach and not decaying yet
		{
			if(-spawnObject.transform.position.z < spawnObjectExtents)
			{
				spawnObject.gameObject.layer = layer;
			}

			spawnObjectTransform.position -= new Vector3(0.0f, 0.0f, spawnObject.transform.position.z * approachSpeed * Time.deltaTime);
			float currentSize = 1.0f - (spawnObject.transform.position.z / spawnHeight);
			spawnObjectTransform.localScale = spawnObjectSize * currentSize;

			yield return null;
		}

		if(spawnObject.gameObject.layer != 9)
		{
			spawnObjectTransform.position = new Vector3(spawnObjectTransform.position.x, spawnObjectTransform.position.y, 0.0f);
			spawnObjectTransform.localScale = spawnObjectSize;
		}
	}

	public IEnumerator DespawnObject(Rigidbody2D despawnObject)
	{
		DockingPort[] ports = despawnObject.GetComponentsInChildren<DockingPort>();
		foreach(DockingPort port in ports)
		{
			if(!port.IsFree())
			{
				port.HotkeyDown();
			}
		}

		despawnObject.gameObject.layer = 9;

		Transform spawnObjectTransform = despawnObject.GetComponent<Transform>();
		Vector3 spawnObjectSize = spawnObjectTransform.localScale;
		float speed = 0.0f;
		while(despawnObject.transform.position.z > spawnHeight)
		{
			speed -= approachSpeed * 0.2f * Time.deltaTime;
			spawnObjectTransform.position += new Vector3(0.0f, 0.0f, speed * Time.deltaTime);
			float currentSize = 1.0f - (despawnObject.transform.position.z / spawnHeight);
			spawnObjectTransform.localScale = spawnObjectSize * currentSize;

			yield return null;
		}

		GameObject.Destroy(despawnObject.gameObject);
	}
}
