using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SpacecraftBlueprintController
{
	[Serializable]
	private struct SpacecraftData
	{
		public List<ModuleData> moduleData;

		public SpacecraftData(List<ModuleData> moduleData)
		{
			this.moduleData = moduleData;
		}
	}

	[Serializable]
	public struct ModuleData
	{
		public string moduleName;
		public Vector2Int position;
		public float rotation;

		public ModuleData(string moduleName, Vector2Int position, float rotation)
		{
			this.moduleName = moduleName;
			this.position = position;
			this.rotation = rotation;
		}
	}

	public static string[] GetBlueprintPaths(string blueprintFolderName)
	{
		string path = Application.persistentDataPath + Path.DirectorySeparatorChar + blueprintFolderName;
		if(Directory.Exists(path))
		{
			return Directory.GetFiles(path);
		}
		else
		{
			Directory.CreateDirectory(path);
			return new string[0];
		}
	}

	public static void SaveBlueprint(string blueprintFolderName, string blueprintName, Dictionary<Vector2Int, Module> modules)
	{
		string path = Application.persistentDataPath + Path.DirectorySeparatorChar + blueprintFolderName + Path.DirectorySeparatorChar + blueprintName + ".json";

		List<ModuleData> moduleData = new List<ModuleData>();
		foreach(Vector2Int position in modules.Keys)
		{
			if(modules[position].GetPosition() == position)
			{
				moduleData.Add(new ModuleData(modules[position].GetModuleName(), modules[position].GetPosition(), modules[position].GetTransform().localRotation.eulerAngles.z));
			}
		}
		SpacecraftData spacecraftData = new SpacecraftData(moduleData);

		using(StreamWriter writer = new StreamWriter(path, false))
		{
			writer.WriteLine(JsonUtility.ToJson(spacecraftData, true));
		}
	}

	public static void LoadBlueprint(string blueprintPath, Transform spacecraftTransform)
	{
		using(StreamReader reader = new StreamReader(blueprintPath))
		{
			InstantiateModules(JsonUtility.FromJson<SpacecraftData>(reader.ReadToEnd()), spacecraftTransform);
		}
	}

	public static void LoadBlueprint(TextAsset blueprint, Transform spacecraftTransform)
	{
		InstantiateModules(JsonUtility.FromJson<SpacecraftData>(blueprint.text), spacecraftTransform);
	}

	private static void InstantiateModules(SpacecraftData spacecraftData, Transform spacecraftTransform)
	{
		Dictionary<string, Module> modulePrefabDictionary = BuildingMenu.GetInstance().GetModulePrefabDictionary();
		foreach(ModuleData moduleData in spacecraftData.moduleData)
		{
			Module module = GameObject.Instantiate<Module>(modulePrefabDictionary[moduleData.moduleName], spacecraftTransform);
			module.Rotate(moduleData.rotation);
			module.Build(moduleData.position);
		}
	}
}
