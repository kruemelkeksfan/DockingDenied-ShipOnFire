using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class QuestFeedbackController : MonoBehaviour
{
	[Serializable]
	public struct QuestFeedbackData
	{
		public int gender;
		public int age;
		public int playtime;
		public QuestManager.Quest selectedQuest;
		public List<string> selectedRewards;
		public QuestManager.Quest rejectedQuest1;
		public QuestManager.Quest rejectedQuest2;
		public List<string> rejectedRewards1;
		public List<string> rejectedRewards2;
		public int backstoryFeedback;
		public int questGiverFeedback;
		public int taskDifficultyFeedback;
		public int taskFunFeedback;
		public int rewardFeedback;
		public int effectFeedback;
		public List<int> reasonFeedback;
		public int enjoymentFeedback;
		public string suggestions;
	}

	private static QuestFeedbackController instance = null;
	private static bool consentDecided = false;
	private static bool accepted = false;
	private static int gender = 0;
	private static int age = 0;
	private static int playtime = 0;

	[SerializeField] private GameObject consentPanel = null;
	[SerializeField] private Dropdown genderField = null;
	[SerializeField] private Dropdown ageField = null;
	[SerializeField] private Dropdown playtimeField = null;
	[SerializeField] private GameObject surveyPanel = null;
	[SerializeField] private Dropdown backstoryField = null;
	[SerializeField] private Dropdown questGiverField = null;
	[SerializeField] private Dropdown taskDifficultyField = null;
	[SerializeField] private Dropdown taskFunField = null;
	[SerializeField] private Dropdown rewardField = null;
	[SerializeField] private Dropdown effectField = null;
	[SerializeField] private Toggle[] reasonFields = { };
	[SerializeField] private Dropdown enjoymentField = null;
	[SerializeField] private InputField suggestionField = null;
	private Dictionary<SpaceStationController, QuestManager.Quest[]> rejectedQuests = null;
	private QuestManager.Quest quest = null;
	private List<string> selectedRewards = null;

	public static QuestFeedbackController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		rejectedQuests = new Dictionary<SpaceStationController, QuestManager.Quest[]>(4);

		instance = this;
	}

	private void Start()
	{
		if(!consentDecided)
		{
			consentPanel.SetActive(true);
		}
	}

	public void Accept()
	{
		accepted = true;
		gender = genderField.value;
		age = ageField.value;
		playtime = playtimeField.value;

		consentDecided = true;
		consentPanel.SetActive(false);
	}

	public void Reject()
	{
		accepted = false;

		consentDecided = true;
		consentPanel.SetActive(false);
	}

	public void RejectQuests(QuestManager.Quest[] rejectedQuests)
	{
		if(accepted && rejectedQuests != null && rejectedQuests.Length > 0)
		{
			this.rejectedQuests[rejectedQuests[0].destination] = rejectedQuests;
		}
	}

	public void RequestFeedback(QuestManager.Quest quest)
	{
		if(accepted)
		{
			this.quest = quest;
			selectedRewards = new List<string>(3);
			foreach(KeyValuePair<string, int> reward in quest.rewards)
			{
				selectedRewards.Add(reward.Value + " " + reward.Key);
			}

			surveyPanel.SetActive(true);
		}
	}

	public void SendFeedback()
	{
		QuestFeedbackData feedback = new QuestFeedbackData();
		feedback.gender = gender;
		feedback.age = age;
		feedback.playtime = playtime;
		feedback.selectedQuest = quest;
		feedback.selectedRewards = selectedRewards;
		feedback.rejectedQuest1 = rejectedQuests[quest.destination][0];
		feedback.rejectedQuest2 = rejectedQuests[quest.destination][1];
		List<string> rejectedRewards1 = new List<string>(3);
		foreach(KeyValuePair<string, int> reward in feedback.rejectedQuest1.rewards)
		{
			rejectedRewards1.Add(reward.Value + " " + reward.Key);
		}
		feedback.rejectedRewards1 = rejectedRewards1;
		List<string> rejectedRewards2 = new List<string>(3);
		foreach(KeyValuePair<string, int> reward in feedback.rejectedQuest2.rewards)
		{
			rejectedRewards2.Add(reward.Value + " " + reward.Key);
		}
		feedback.rejectedRewards2 = rejectedRewards2;
		feedback.backstoryFeedback = backstoryField.value;
		feedback.questGiverFeedback = questGiverField.value;
		feedback.taskDifficultyFeedback = taskDifficultyField.value;
		feedback.taskFunFeedback = taskFunField.value;
		feedback.rewardFeedback = rewardField.value;
		feedback.effectFeedback = effectField.value;
		List<int> reasons = new List<int>(reasonFields.Length);
		for(int i = 0; i < reasonFields.Length; ++i)
		{
			if(reasonFields[i].isOn)
			{
				reasons.Add(i);
			}
		}
		feedback.reasonFeedback = reasons;
		feedback.enjoymentFeedback = enjoymentField.value;
		feedback.suggestions = suggestionField.text;

		Debug.Log(JsonUtility.ToJson(feedback));

		// TODO: Send JSON Contents to Server (e.g. via HttpWebRequest)

		surveyPanel.SetActive(false);
	}

	public void CloseSurveyPanel()
	{
		surveyPanel.SetActive(false);
	}
}
