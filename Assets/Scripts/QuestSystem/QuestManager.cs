using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour, IListener
{
	public enum TaskType { Destroy, Bribe, JumpStart, Supply, Plunder, Tow, Trade };
	public enum VesselType { Unknown, Fugitive, Customs, Pirate, Combat, Helpless, Trading };

	[Serializable]
	public struct QuestData
	{
		public BackstoryData[] backstories;
		public QuestGiverData[] questGivers;
		public TaskData[] tasks;
	}

	[Serializable]
	public struct BackstoryData
	{
		public string description;
		public int[] questGivers;
		public int[] tasks;
	}

	[Serializable]
	public struct QuestGiverData
	{
		public string description;
		public int[] tasks;
	}

	[Serializable]
	public struct TaskData
	{
		public string description;
	}

	public class Quest
	{
		public int backstory;
		public int questGiver;
		public int task;
		public SpaceStationController destination;
		public KeyValuePair<string, int>[] rewards;
		public float progress;
		public TaskType taskType;
		public VesselType vesselType;
		public string infoString;
		public int infoInt;
	}

	private static QuestManager instance = null;

	[SerializeField] private TextAsset questDataFile = null;
	[Tooltip("The approximate Value of each Reward Roll for the Reward List.")]
	[SerializeField] private int rewardValue = 100;
	[Tooltip("Number of Rolls for the Reward List.")]
	[SerializeField] private int rewardCount = 3;
	[Tooltip("Chance that a Roll results in a monetary Reward instead of Goods.")]
	[SerializeField] private float monetaryRewardChance = 0.4f;
	[Tooltip("Range in which Quest Vessels will spawn around the Station.")]
	[SerializeField] private MinMax questVesselSpawnRange = new MinMax(4.0f, 12.0f);
	[SerializeField] private Spacecraft questVesselPrefab = null;
	[SerializeField] private TextAsset[] questVesselBlueprints = { };
	[Tooltip("Radius around the Vessel which must be free of any Colliders before Spawn.")]
	[SerializeField] private float questVesselSpawnClearance = 0.2f;
	private InventoryController localPlayerMainInventory = null;
	private BackstoryData[] backstories = null;
	private QuestGiverData[] questGivers = null;
	private TaskData[] tasks = null;
	private string[] goodNames = null;                                              // Dictionary does not allow random Element picking, therefore 2 Arrays
	private int[] goodRewards = null;
	private Dictionary<SpaceStationController, Quest> activeQuests = null;

	public static QuestManager GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		// TODO: Maybe separte Quest Type and Vessel Type, this also allows to make just 1 "Destroy Vessel" Task, 1 Towing Task etc., also allows for towing Combat Vessels without introducing 9000 new Tasks

		QuestData questData = JsonUtility.FromJson<QuestData>(questDataFile.text);
		backstories = questData.backstories;
		questGivers = questData.questGivers;
		tasks = questData.tasks;

		/*for(int i = 0; i < backstories.Length; ++i)
		{
			Debug.Log(i + ": " + backstories[i].description);
		}
		Debug.Log("--------------------");
		for(int i = 0; i < questGivers.Length; ++i)
		{
			Debug.Log(i + ": " + questGivers[i].description);
		}
		Debug.Log("--------------------");
		for(int i = 0; i < tasks.Length; ++i)
		{
			Debug.Log(i + ": " + tasks[i].description);
		}
		Debug.Log("--------------------");
		for(int i = 0; i < backstories.Length; ++i)
		{
			for(int j = 0; j < backstories[i].questGivers.Length; ++j)
			{
				List<int> questGiverList = new List<int>(questGivers[backstories[i].questGivers[j]].tasks);
				List<int> taskList = new List<int>();
				foreach(int task in backstories[i].tasks)
				{
					if(questGiverList.Contains(task))
					{
						taskList.Add(task);
					}
				}
				if(taskList.Count <= 0)
				{
					Debug.LogWarning("No valid Combination for Backstory " + i + " and Quest Giver " + j + "!");
				}

				for(int n = 0; n < taskList.Count; ++n)
				{
					Debug.Log(backstories[i].description);
					Debug.Log(questGivers[backstories[i].questGivers[j]].description);
					Debug.Log(tasks[taskList[n]].description);
					Debug.Log("--------------------");
				}
			}
		}*/

		activeQuests = new Dictionary<SpaceStationController, Quest>(1);

		instance = this;
	}

	private void Start()
	{
		Dictionary<string, GoodManager.Good> goods = GoodManager.GetInstance().GetGoodDictionary();
		goodNames = new string[goods.Count];
		goodRewards = new int[goods.Count];
		int i = 0;
		foreach(string goodName in goods.Keys)
		{
			goodNames[i] = goodName;
			goodRewards[i] = Mathf.RoundToInt((float)rewardValue / (float)goods[goodName].price);
			++i;
		}

		SpacecraftManager spacecraftManager = SpacecraftManager.GetInstance();
		localPlayerMainInventory = spacecraftManager.GetLocalPlayerMainSpacecraft().GetComponent<InventoryController>();
		spacecraftManager.AddSpacecraftChangeListener(this);
	}

	public void Notify()
	{
		localPlayerMainInventory = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft().GetComponent<InventoryController>();
	}

	// TODO: Maybe take into Account Player Preferences, e.g. his Questionary Answers and which Types of Quest he did most in the Past
	// (Example Algorithm for the last Part: Probability of a Quest Part = Amount of Times it was performed in the Past / All Quests performed in the Past
	// => if >20%, use this Number, else equally distribute Chances,
	// Always generate 1/3 Quests completely random to avoid Player getting locked up in Quests he does not like (any more))
	public Quest GenerateQuest(SpaceStationController questStation, int attempt = 0)
	{
		Quest quest = new Quest();

		quest.backstory = UnityEngine.Random.Range(0, backstories.Length);
		quest.questGiver = backstories[quest.backstory].questGivers[UnityEngine.Random.Range(0, backstories[quest.backstory].questGivers.Length)];

		List<int> taskIntersection = new List<int>(backstories[quest.backstory].tasks.Length);
		foreach(int backstoryTask in backstories[quest.backstory].tasks)
		{
			foreach(int questGiverTask in questGivers[quest.questGiver].tasks)
			{
				if(backstoryTask == questGiverTask)
				{
					taskIntersection.Add(backstoryTask);
				}
				else if(questGiverTask > backstoryTask)
				{
					break;
				}
			}
		}
		if(taskIntersection.Count < 1)
		{
			Debug.LogWarning("No valid Tasks for the Combination of Backstory " + quest.backstory + " and QuestGiver " + quest.questGiver + "!");
			if(attempt < 5)
			{
				return GenerateQuest(questStation, attempt + 1);
			}
			else
			{
				return null;
			}
		}
		quest.task = taskIntersection[UnityEngine.Random.Range(0, taskIntersection.Count)];

		quest.destination = questStation;

		Dictionary<string, int> rewards = new Dictionary<string, int>(rewardCount);
		for(int i = 0; i < rewardCount; ++i)
		{
			if(UnityEngine.Random.value < monetaryRewardChance)
			{
				if(!rewards.ContainsKey("$"))
				{
					rewards["$"] = rewardValue;
				}
				else
				{
					rewards["$"] += rewardValue;
				}
			}
			else
			{
				int goodIndex = UnityEngine.Random.Range(0, goodNames.Length);
				if(!rewards.ContainsKey(goodNames[goodIndex]))
				{
					rewards[goodNames[goodIndex]] = goodRewards[goodIndex];
				}
				else
				{
					rewards[goodNames[goodIndex]] += goodRewards[goodIndex];
				}
			}
		}
		quest.rewards = new KeyValuePair<string, int>[rewards.Count];
		int j = 0;
		foreach(KeyValuePair<string, int> reward in rewards)
		{
			quest.rewards[j] = reward;
			++j;
		}
		Array.Sort(quest.rewards, delegate (KeyValuePair<string, int> lho, KeyValuePair<string, int> rho)
		{
			return lho.Key.CompareTo(rho.Key);
		});

		quest.progress = 0.001f;

		if(tasks[quest.task].description.StartsWith("Destroy"))
		{
			quest.taskType = TaskType.Destroy;
			if(tasks[quest.task].description.Contains("Fugitive Vessel"))
			{
				quest.vesselType = VesselType.Fugitive;
			}
			else if(tasks[quest.task].description.Contains("Customs Agency Vessel"))
			{
				quest.vesselType = VesselType.Customs;
			}
			else if(tasks[quest.task].description.Contains("Pirate Vessel"))
			{
				quest.vesselType = VesselType.Pirate;
			}
			else if(tasks[quest.task].description.Contains("Combat Vessel"))
			{
				quest.vesselType = VesselType.Combat;
			}
			else
			{
				Debug.LogError("Invalid Quest Vessel Type in Task " + tasks[quest.task].description + "!");
			}
			quest.infoString = null;
			quest.infoInt = 0;
		}
		else if(tasks[quest.task].description.StartsWith("Bribe"))
		{
			quest.taskType = TaskType.Bribe;
			quest.vesselType = VesselType.Customs;
			quest.infoString = null;
			quest.infoInt = 0;
		}
		else if(tasks[quest.task].description.StartsWith("Jump-start"))
		{
			quest.taskType = TaskType.JumpStart;
			quest.vesselType = VesselType.Helpless;
			quest.infoString = null;
			quest.infoInt = 0;
		}
		else if(tasks[quest.task].description.StartsWith("Supply"))
		{
			quest.taskType = TaskType.Supply;
			quest.vesselType = VesselType.Helpless;
			string[] taskItems = tasks[quest.task].description.Split(' ');
			quest.infoString = taskItems[2];
			quest.infoInt = int.Parse(taskItems[1]);
		}
		else if(tasks[quest.task].description.StartsWith("Plunder"))
		{
			quest.taskType = TaskType.Plunder;
			if(tasks[quest.task].description.Contains("helpless Vessel"))
			{
				quest.vesselType = VesselType.Helpless;
			}
			else if(tasks[quest.task].description.Contains("Trading Vessel"))
			{
				quest.vesselType = VesselType.Trading;
			}
			else
			{
				Debug.LogError("Invalid Quest Vessel Type in Task " + tasks[quest.task].description + "!");
			}
			quest.infoString = null;
			quest.infoInt = 0;
		}
		else if(tasks[quest.task].description.StartsWith("Tow"))
		{
			quest.taskType = TaskType.Tow;
			quest.vesselType = VesselType.Helpless;
			quest.infoString = null;
			quest.infoInt = 0;
		}
		else if(tasks[quest.task].description.StartsWith("Sell"))
		{
			quest.taskType = TaskType.Trade;
			quest.vesselType = VesselType.Unknown;
			string[] taskItems = tasks[quest.task].description.Split(' ');
			quest.infoString = taskItems[2] == "Sanitary" ? (taskItems[2] + " " + taskItems[3]) : taskItems[2];         // Quick and dirty Solutions for Good Names with Spaces
			quest.infoInt = -int.Parse(taskItems[1]);
		}
		else if(tasks[quest.task].description.StartsWith("Buy"))
		{
			quest.taskType = TaskType.Trade;
			quest.vesselType = VesselType.Unknown;
			string[] taskItems = tasks[quest.task].description.Split(' ');
			if(taskItems[1] == "Goods")
			{
				quest.infoString = "$";
				quest.infoInt = -int.Parse(taskItems[5].Remove(taskItems[5].Length - 1, 1));
			}
			else
			{
				quest.infoString = taskItems[2] == "Sanitary" ? (taskItems[2] + " " + taskItems[3]) : taskItems[2];     // Quick and dirty Solutions for Good Names with Spaces
				quest.infoInt = int.Parse(taskItems[1]);
			}
		}

		return quest;
	}

	public void AcceptQuest(Quest quest)
	{
		activeQuests.Add(quest.destination, quest);

		if(quest.taskType == TaskType.Trade && quest.infoString != "$")
		{
			if(quest.infoInt > 0)
			{
				quest.destination.GetInventoryController().Deposit(quest.infoString, (uint)quest.infoInt);
			}
			else
			{
				InventoryController inventoryController = quest.destination.GetInventoryController();
				inventoryController.Withdraw(quest.infoString, inventoryController.GetGoodAmount(quest.infoString));
			}
		}
		else if(quest.taskType == TaskType.Destroy || quest.taskType == TaskType.Bribe || quest.taskType == TaskType.JumpStart
			|| quest.taskType == TaskType.Supply || quest.taskType == TaskType.Plunder || quest.taskType == TaskType.Tow)
		{
			Vector2 stationPosition = quest.destination.GetTransform().position;
			Vector2 spawnPosition = Vector2.zero;
			do
			{
				// sin(a) = G / H => G = sin(a) * H
				// cos(a) = A / H => A = cos(a) * H
				float spawnDistance = UnityEngine.Random.Range(questVesselSpawnRange.Min, questVesselSpawnRange.Max);
				float spawnAngle = UnityEngine.Random.Range(0.0f, 2 * Mathf.PI);
				spawnPosition = stationPosition + new Vector2(Mathf.Cos(spawnAngle) * spawnDistance, Mathf.Sin(spawnAngle) * spawnDistance);
			}
			while(Physics2D.OverlapCircle(spawnPosition, questVesselSpawnClearance));

			Spacecraft questVesselSpacecraft = GameObject.Instantiate<Spacecraft>(questVesselPrefab, spawnPosition, Quaternion.identity);
			SpacecraftBlueprintController.LoadBlueprint(questVesselBlueprints[UnityEngine.Random.Range(0, questVesselBlueprints.Length)], questVesselSpacecraft.GetTransform());

			QuestVesselController questVessel = questVesselSpacecraft.GetComponent<QuestVesselController>();
			questVessel.SetQuest(quest);
		}
	}

	public void CompleteQuest(SpaceStationController spaceStation)
	{
		Quest quest = activeQuests[spaceStation];
		for(int i = 0; i < quest.rewards.Length; ++i)
		{
			if(quest.rewards[i].Key == "$")
			{
				localPlayerMainInventory.TransferMoney(quest.rewards[i].Value);
				quest.rewards[i] = new KeyValuePair<string, int>(quest.rewards[i].Key, 0);
			}
			else
			{
				if(localPlayerMainInventory.Deposit(quest.rewards[i].Key, (uint)quest.rewards[i].Value))
				{
					quest.rewards[i] = new KeyValuePair<string, int>(quest.rewards[i].Key, 0);
				}
				else
				{
					InfoController.GetInstance().AddMessage("Not enough Storage Capacity on your Vessel, all Lavatories are full!");
					return;
				}
			}
		}

		activeQuests.Remove(spaceStation);
	}

	public void NotifyTrade(SpaceStationController questStation, string goodName, int amount, int price)
	{
		if(activeQuests.ContainsKey(questStation))
		{
			if(activeQuests[questStation].taskType == TaskType.Trade)
			{
				if(activeQuests[questStation].infoString == "$")
				{
					activeQuests[questStation].progress += (float)price / (float)activeQuests[questStation].infoInt;
				}
				else if(activeQuests[questStation].infoString == goodName)
				{
					activeQuests[questStation].progress += (float)amount / (float)activeQuests[questStation].infoInt;
				}
			}
		}
	}

	public string GetBackstoryDescription(int backstoryIndex)
	{
		return backstories[backstoryIndex].description;
	}

	public string GetQuestGiverDescription(int questGiverIndex)
	{
		return questGivers[questGiverIndex].description;
	}

	public string GetTaskDescription(int taskIndex)
	{
		return tasks[taskIndex].description;
	}

	public Quest GetActiveQuest(SpaceStationController spaceStation)
	{
		if(activeQuests.ContainsKey(spaceStation))
		{
			return activeQuests[spaceStation];
		}
		else
		{
			return null;
		}
	}
}
