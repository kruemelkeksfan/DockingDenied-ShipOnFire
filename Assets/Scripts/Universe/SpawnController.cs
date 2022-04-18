using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnController : MonoBehaviour
{
	private static SpawnController instance = null;

	[Tooltip("The clear Area which is required for a new Object to spawn at a specific Position")]
	[SerializeField] private float spawnAreaRadius = 200.0f;
	[Tooltip("The maximum of Spawn Tries, before increasing the Spawn Radius")]
	[SerializeField] private int maxSpawnTries = 20;
	[Tooltip("The Factor by which the Spawn Radius is increased, if the maximum Amount of Spawn Tries is reached")]
	[SerializeField] private float spawnRadiusFactor = 2.0f;
	[Tooltip("Height above the XY-Plane at which new Objects spawn before descending into the XY-Plane")]
	[SerializeField] private float spawnHeight = 10000.0f;
	[Tooltip("Approach Velocity of freshly spawned Objects")]
	[SerializeField] private float approachSpeed = 200.0f;
	[Tooltip("Acceleration of freshly despawned Objects")]
	[SerializeField] private float disappearingAcceleration = 20.0f;
	private TimeController timeController = null;
	private AsteroidSpawner asteroidSpawner = null;
	private GravityWellController gravityWellController = null;
	private int collisionLayers = Physics2D.DefaultRaycastLayers;

	public static SpawnController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		collisionLayers = LayerMask.GetMask(new string[] { "Approaching", "Asteroids", "Spacecraft" });

		instance = this;
	}

	private void Start()
	{
		timeController = TimeController.GetInstance();
		asteroidSpawner = AsteroidSpawner.GetInstance();
		gravityWellController = GravityWellController.GetInstance();
	}

	public IEnumerator<float> SpawnObject(Rigidbody2D spawnObjectPrefab, Vector2 globalSpawnCenter, MinMax spawnRange, int layer, QuestManager.Quest quest = null)
	{
		float alpha = Random.Range(0.0f, Mathf.PI * 2.0f);
		float radius = Random.Range(spawnRange.min, spawnRange.max);
		Vector2 position;
		int i = 0;
		do
		{
			if(i >= maxSpawnTries)
			{
				Debug.LogWarning("Spawn of " + spawnObjectPrefab + " unsuccessful at Position " + globalSpawnCenter + " with Radius " + radius + "!");

				radius *= spawnRadiusFactor;
				i = 0;
			}

			position = gravityWellController.GlobalToLocalPosition(globalSpawnCenter
				+ (new Vector2(Mathf.Cos(alpha), Mathf.Sin(alpha)) * radius));
		}
		while(Physics2D.OverlapCircle(position, spawnAreaRadius, collisionLayers) != null);

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

		Transform spawnObjectTransform = spawnObject.GetComponent<Transform>();
		float spawnObjectExtents = Mathf.Max(spawnObjectTransform.GetComponent<Collider2D>().bounds.extents.x, 1.0f);
		Vector3 spawnObjectSize = spawnObjectTransform.localScale;
		while(spawnObject != null && spawnObject.transform.position.z > 0.1f && spawnObject.gameObject.layer != 9)                                                                   // Check if Asteroid is still in Approach and not decaying yet
		{
			if(spawnObject.transform.position.z < spawnObjectExtents)
			{
				spawnObject.gameObject.layer = layer;
			}

			spawnObjectTransform.position -= new Vector3(0.0f, 0.0f, spawnObject.transform.position.z * approachSpeed * timeController.GetDeltaTime());
			float currentSize = 1.0f - (spawnObject.transform.position.z / spawnHeight);
			spawnObjectTransform.localScale = spawnObjectSize * currentSize;

			yield return -1.0f;
		}

		if(spawnObject != null && spawnObject.gameObject.layer != 9)
		{
			spawnObjectTransform.position = new Vector3(spawnObjectTransform.position.x, spawnObjectTransform.position.y, 0.0f);
			spawnObjectTransform.localScale = spawnObjectSize;
		}
	}

	public IEnumerator<float> DespawnObject(Rigidbody2D despawnObject)
	{
		UndockAll(despawnObject);

		despawnObject.gameObject.layer = 9;

		Transform spawnObjectTransform = despawnObject.GetComponent<Transform>();
		Vector3 spawnObjectSize = spawnObjectTransform.localScale;
		float speed = 0.0f;
		while(despawnObject.transform.position.z < spawnHeight)
		{
			// Add Sqrt(height) to Acceleration to accelerate Disappearance slightly
			float deltaTime = timeController.GetDeltaTime();
			speed += (disappearingAcceleration + Mathf.Sqrt(despawnObject.transform.position.z)) * deltaTime;
			spawnObjectTransform.position += new Vector3(0.0f, 0.0f, speed * deltaTime);
			float currentSize = 1.0f - (despawnObject.transform.position.z / spawnHeight);
			spawnObjectTransform.localScale = spawnObjectSize * currentSize;

			yield return -1.0f;
		}

		DestroyObject(despawnObject);
	}

	private void UndockAll(Rigidbody2D spacecraft)
	{
		DockingPort[] ports = spacecraft.GetComponentsInChildren<DockingPort>();
		foreach(DockingPort port in ports)
		{
			if(!port.IsFree())
			{
				port.HotkeyDown();
			}
		}
	}

	public void DestroyObject(Rigidbody2D despawnObject)
	{
		UndockAll(despawnObject);
		GameObject.Destroy(despawnObject.gameObject);
	}
}
