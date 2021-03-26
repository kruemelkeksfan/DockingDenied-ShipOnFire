using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpaceStationSpawner : MonoBehaviour
{
	[SerializeField] private Spacecraft spacecraftPrefab = null;
	[SerializeField] private TextAsset[] stationBlueprints = { };

	private void Start()
	{
		Spacecraft spaceStation = GameObject.Instantiate<Spacecraft>(spacecraftPrefab, new Vector3(500.0f, 0.2f, 0.0f), Quaternion.identity);
		SpacecraftBlueprintController.LoadBlueprint(stationBlueprints[Random.Range(0, stationBlueprints.Length - 1)], spaceStation.GetTransform());
	}
}
