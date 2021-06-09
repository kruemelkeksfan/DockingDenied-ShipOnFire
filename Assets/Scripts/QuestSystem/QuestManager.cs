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
	[SerializeField] private Rigidbody2D questVesselPrefab = null;
	[Tooltip("Radius around the Vessel which must be free of any Colliders before Spawn.")]
	[SerializeField] private float questVesselSpawnClearance = 0.2f;
	private SpawnController spawnController = null;
	private InventoryController localPlayerMainInventory = null;
	private BackstoryData[] backstories = null;
	private QuestGiverData[] questGivers = null;
	private TaskData[] tasks = null;
	private string[] goodNames = null;                                              // Dictionary does not allow random Element picking, therefore 2 Arrays
	private int[] goodRewards = null;
	private List<int>[] taskBackstories = null;
	private List<int>[] taskQuestGivers = null;
	private List<int> questGiverIntersection = null;
	private Dictionary<TaskType, List<int>> taskTypes = null;
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

		taskBackstories = new List<int>[tasks.Length];
		taskQuestGivers = new List<int>[tasks.Length];
		for(int i = 0; i < tasks.Length; ++i)
		{
			taskBackstories[i] = new List<int>();
			for(int j = 0; j < backstories.Length; ++j)
			{
				foreach(int task in backstories[j].tasks)
				{
					if(task == i)
					{
						taskBackstories[i].Add(j);
						break;
					}
				}
			}

			taskQuestGivers[i] = new List<int>();
			for(int j = 0; j < questGivers.Length; ++j)
			{
				foreach(int task in questGivers[j].tasks)
				{
					if(task == i)
					{
						taskQuestGivers[i].Add(j);
						break;
					}
				}
			}
		}

		questGiverIntersection = new List<int>();

		Array taskTypeValues = Enum.GetValues(typeof(TaskType));
		taskTypes = new Dictionary<TaskType, List<int>>(taskTypeValues.Length);
		foreach(TaskType taskType in taskTypeValues)
		{
			taskTypes.Add(taskType, new List<int>());
		}
		for(int i = 0; i < tasks.Length; ++i)
		{
			if(tasks[i].description.StartsWith("Destroy"))
			{
				taskTypes[TaskType.Destroy].Add(i);
			}
			else if(tasks[i].description.StartsWith("Bribe"))
			{
				taskTypes[TaskType.Bribe].Add(i);
			}
			else if(tasks[i].description.StartsWith("Jump-start"))
			{
				taskTypes[TaskType.JumpStart].Add(i);
			}
			else if(tasks[i].description.StartsWith("Supply"))
			{
				taskTypes[TaskType.Supply].Add(i);
			}
			else if(tasks[i].description.StartsWith("Plunder"))
			{
				taskTypes[TaskType.Plunder].Add(i);
			}
			else if(tasks[i].description.StartsWith("Tow"))
			{
				taskTypes[TaskType.Tow].Add(i);
			}
			else if(tasks[i].description.StartsWith("Sell"))
			{
				taskTypes[TaskType.Trade].Add(i);
			}
			else if(tasks[i].description.StartsWith("Buy"))
			{
				taskTypes[TaskType.Trade].Add(i);
			}
		}

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
		spawnController = SpawnController.GetInstance();
		Dictionary<string, GoodManager.Good> goods = GoodManager.GetInstance().GetGoodDictionary();
		goodNames = new string[goods.Count];
		goodRewards = new int[goods.Count];
		int i = 0;
		foreach(string goodName in goods.Keys)
		{
			goodNames[i] = goodName;
			goodRewards[i] = Mathf.RoundToInt(rewardValue / (goods[goodName].price * 0.5f));								// * 0.5 bc Prices at Stations are Shit and Goods therefore have less Utility than Money
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
	public Quest GenerateQuest(SpaceStationController questStation, TaskType[] taskTypes = null, int attempt = 0)
	{
		Quest quest = new Quest();

		if(taskTypes == null || taskTypes.Length <= 0)
		{
			quest.task = UnityEngine.Random.Range(0, tasks.Length);
		}
		else
		{
			TaskType taskType = taskTypes[UnityEngine.Random.Range(0, taskTypes.Length)];
			quest.task = this.taskTypes[taskType][UnityEngine.Random.Range(0, this.taskTypes[taskType].Count)];
		}

		quest.backstory = taskBackstories[quest.task][UnityEngine.Random.Range(0, taskBackstories[quest.task].Count)];

		questGiverIntersection.Clear();
		foreach(int questGiver in backstories[quest.backstory].questGivers)
		{
			foreach(int task in questGivers[questGiver].tasks)
			{
				if(task == quest.task)
				{
					questGiverIntersection.Add(questGiver);
					break;
				}
			}
		}
		if(questGiverIntersection.Count <= 0)
		{
			Debug.LogWarning("No valid Quest Givers for the Combination of Task " + quest.task + " and Backstory " + quest.backstory + "!");
			if(attempt < 5)
			{
				return GenerateQuest(questStation, taskTypes, attempt + 1);
			}
			else
			{
				return null;
			}
		}
		quest.questGiver = questGiverIntersection[UnityEngine.Random.Range(0, questGiverIntersection.Count)];

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

		// TODO: Use Reverse from taskTypes-Dictionary to save Performance (String Comparisons)
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

			InventoryController inventoryController = quest.destination.GetInventoryController();
			inventoryController.Withdraw(quest.infoString, inventoryController.GetGoodAmount(quest.infoString));
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

		if(quest.taskType == TaskType.Destroy || quest.taskType == TaskType.Bribe || quest.taskType == TaskType.JumpStart
			|| quest.taskType == TaskType.Supply || quest.taskType == TaskType.Plunder || quest.taskType == TaskType.Tow)
		{
			StartCoroutine(spawnController.SpawnObject(questVesselPrefab, quest.destination.GetTransform().position, questVesselSpawnRange, 11, quest));
		}
	}

	public bool CompleteQuest(SpaceStationController spaceStation)
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
					return false;
				}
			}
		}

		activeQuests.Remove(spaceStation);
		return true;
	}

	public void NotifyTrade(SpaceStationController questStation, string goodName, int amount, int price)
	{
		if(activeQuests.ContainsKey(questStation) && activeQuests[questStation].taskType == TaskType.Trade && activeQuests[questStation].infoString != "$" && activeQuests[questStation].infoInt < 0)
		{
			if(activeQuests[questStation].infoString == goodName)
			{
				activeQuests[questStation].progress += (float)amount / (float)activeQuests[questStation].infoInt;
			}
		}

		foreach(SpaceStationController station in activeQuests.Keys)
		{
			if(station != questStation && activeQuests.ContainsKey(station) && activeQuests[station].taskType == TaskType.Trade && (activeQuests[station].infoString == "$" || activeQuests[station].infoInt > 0))
			{
				if(activeQuests[station].infoString == "$")
				{
					activeQuests[station].progress += (float)price / (float)activeQuests[station].infoInt;
				}
				else if(activeQuests[station].infoString == goodName)
				{
					activeQuests[station].progress += (float)amount / (float)activeQuests[station].infoInt;
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

	public int GetActiveQuestCount()
	{
		return activeQuests.Count;
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
