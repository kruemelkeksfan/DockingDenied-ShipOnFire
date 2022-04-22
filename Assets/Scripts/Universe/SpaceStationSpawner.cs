using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class SpaceStationSpawner : MonoBehaviour
{
	private static SpaceStationSpawner instance = null;

	[SerializeField] private List<string> namePrefixes = null;
	[SerializeField] private List<string> nameSuffixes = null;
	[SerializeField] private float numberSuffixChance = 0.1f;
	[SerializeField] private SpacecraftController spaceStationPrefab = null;
	[SerializeField] private TextAsset[] stationBlueprints = { };
	[SerializeField] private Vector2[] stationPositions = { };

	public static SpaceStationSpawner GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		instance = this;
	}

	private void Start()
	{
		GravityWellController gravityWellController = GravityWellController.GetInstance();
		foreach(Vector2 position in stationPositions)
		{
			SpacecraftController spaceStationSpacecraft = GameObject.Instantiate<SpacecraftController>(spaceStationPrefab, gravityWellController.GlobalToLocalPosition(position), Quaternion.identity);
			SpacecraftBlueprintController.InstantiateModules(SpacecraftBlueprintController.LoadBlueprintModules(stationBlueprints[Random.Range(0, stationBlueprints.Length)]), spaceStationSpacecraft.GetTransform());
			SpaceStationController spaceStation = spaceStationSpacecraft.GetComponent<SpaceStationController>();

			int prefixIndex = Random.Range(0, namePrefixes.Count);
			if(Random.value < numberSuffixChance)
			{
				StringBuilder name = new StringBuilder(namePrefixes[prefixIndex] + " ");
				int i = 0;
				do
				{
					if(i <= 0)
					{
						name.Append(Random.Range(1, 9));
					}
					else
					{
						name.Append(Random.Range(0, 9));
					}
					++i;
				}
				while(i < 4 && Random.value < 0.5f);
				spaceStation.SetStationName(name.ToString());
			}
			else
			{
				spaceStation.SetStationName(namePrefixes[prefixIndex] + " " + nameSuffixes[Random.Range(0, nameSuffixes.Count)]);
			}
			namePrefixes.RemoveAt(prefixIndex);
		}
	}
}
