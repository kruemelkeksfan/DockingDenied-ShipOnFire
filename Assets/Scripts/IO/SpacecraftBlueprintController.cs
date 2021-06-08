using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SpacecraftBlueprintController
{
	[Serializable]
	public struct SpacecraftData
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
		public string type;
		public Vector2Int position;
		public float rotation;
		public string actionName;
		public int hotkey;

		public ModuleData(string type, Vector2Int position, float rotation, string actionName = "", int hotkey = -1)
		{
			this.type = type;
			this.position = position;
			this.rotation = rotation;
			this.actionName = actionName;
			this.hotkey = hotkey;
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
				if(modules[position] is HotkeyModule)
				{
					HotkeyModule hotkeyModule = (HotkeyModule)modules[position];
					moduleData.Add(new ModuleData(modules[position].GetModuleName(), hotkeyModule.GetPosition(), hotkeyModule.GetTransform().localRotation.eulerAngles.z, hotkeyModule.GetActionName(), hotkeyModule.GetHotkey()));
				}
				else
				{
					moduleData.Add(new ModuleData(modules[position].GetModuleName(), modules[position].GetPosition(), modules[position].GetTransform().localRotation.eulerAngles.z));
				}
			}
		}
		SpacecraftData spacecraftData = new SpacecraftData(moduleData);

		using(StreamWriter writer = new StreamWriter(path, false))
		{
			writer.WriteLine(JsonUtility.ToJson(spacecraftData, true));
		}
	}

	public static GoodManager.Load[] CalculateBlueprintCosts(SpacecraftData spacecraftData)
	{
		Dictionary<string, Module> modulePrefabDictionary = BuildingMenu.GetInstance().GetModulePrefabDictionary();
		Dictionary<string, uint> costDictionary = new Dictionary<string, uint>();
		foreach(ModuleData moduleData in spacecraftData.moduleData)
		{
			if(moduleData.type != "Command Module")
			{
				foreach(GoodManager.Load cost in modulePrefabDictionary[moduleData.type].GetBuildingCosts())
				{
					if(cost.amount > 0)
					{
						if(!costDictionary.ContainsKey(cost.goodName))
						{
							costDictionary[cost.goodName] = cost.amount;
						}
						else
						{
							costDictionary[cost.goodName] += cost.amount;
						}
					}
				}
			}
		}
		GoodManager.Load[] costs = new GoodManager.Load[costDictionary.Count];
		int i = 0;
		foreach(string costName in costDictionary.Keys)
		{
			costs[i] = new GoodManager.Load();
			costs[i].goodName = costName;
			costs[i].amount = costDictionary[costName];
			++i;
		}

		return costs;
	}

	public static SpacecraftData LoadBlueprintModules(string blueprintPath)
	{
		using(StreamReader reader = new StreamReader(blueprintPath))
		{
			return JsonUtility.FromJson<SpacecraftData>(reader.ReadToEnd());
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

	public static void InstantiateModules(SpacecraftData spacecraftData, Transform spacecraftTransform)
	{
		spacecraftTransform.gameObject.GetComponent<Spacecraft>().DeconstructModules();

		Dictionary<string, Module> modulePrefabDictionary = BuildingMenu.GetInstance().GetModulePrefabDictionary();
		foreach(ModuleData moduleData in spacecraftData.moduleData)
		{
			Module module = GameObject.Instantiate<Module>(modulePrefabDictionary[moduleData.type], spacecraftTransform);
			module.Rotate(moduleData.rotation);
			module.Build(moduleData.position);

			if(moduleData.hotkey >= 0 || !string.IsNullOrEmpty(moduleData.actionName))
			{
				HotkeyModule hotkeyModule = module.GetComponent<HotkeyModule>();
				if(!string.IsNullOrEmpty(moduleData.actionName))
				{
					hotkeyModule.SetActionName(moduleData.actionName);
				}
				if(moduleData.hotkey >= 0)
				{
					hotkeyModule.SetHotkey(moduleData.hotkey);
				}
			}
		}
	}
}
