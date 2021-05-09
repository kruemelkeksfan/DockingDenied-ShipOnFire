using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
	public enum EventType { Trade };

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

	public struct Quest
	{
		public int backstory;
		public int questGiver;
		public int task;
		public SpaceStationController destination;
		public KeyValuePair<string, int>[] rewards;
		public float progress;
	}

	private static QuestManager instance = null;

	[SerializeField] private TextAsset questDataFile = null;
	[Tooltip("The approximate Value of each Reward Roll for the Reward List.")]
	[SerializeField] private int rewardValue = 100;
	[Tooltip("Number of Rolls for the Reward List.")]
	[SerializeField] private int rewardCount = 3;
	[Tooltip("Chance that a Roll results in a monetary Reward instead of Goods.")]
	[SerializeField] private float monetaryRewardChance = 0.4f;
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
		// TODO: Give Vessels IDs (e.g. T3 for the third spawned Trading Ship) and use this ID in task descriptions instead of "a Trading Vesssel", this also allows to make just 1 "Destroy Vessel" Task, 1 Towing Task etc., also allows for towing Combat Vessels without introducing 9000 new Tasks

		QuestData questData = JsonUtility.FromJson<QuestData>(questDataFile.text);
		backstories = questData.backstories;
		questGivers = questData.questGivers;
		tasks = questData.tasks;

		/*for(int i = 0; i < backstories.Length; ++i)
		{
			Debug.Log(i + ": " + backstories[i].description);
		}
		for(int i = 0; i < questGivers.Length; ++i)
		{
			Debug.Log(i + ": " + questGivers[i].description);
		}
		for(int i = 0; i < tasks.Length; ++i)
		{
			Debug.Log(i + ": " + tasks[i].description);
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
	}

	public Quest GenerateQuest(SpaceStationController questStation)
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
			Debug.LogError("No valid Tasks for the Combination of Backstory " + quest.backstory + " and QuestGiver " + quest.questGiver + "!");
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

		return quest;
	}

	public void AcceptQuest(Quest quest)
	{
		activeQuests.Add(quest.destination, quest);

		// TODO: Parse Task and prepare Stuff (e.g. spawn Target Vessels)
	}

	public void CompleteQuest(SpaceStationController spaceStation)
	{
		// TODO: Award Rewards, Reformat Rewards from string[] to sorted KeyValuePair[]

		activeQuests.Remove(spaceStation);
	}

	public void Notify(EventType eventType)
	{
		foreach(Quest quest in activeQuests.Values)
		{
			// TODO: Parse Task and look if it is completed, if so change Quest Status
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

	public Quest? GetActiveQuest(SpaceStationController spaceStation)
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
