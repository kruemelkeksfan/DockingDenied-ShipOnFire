using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
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
		public string otherReason;
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
	[SerializeField] private InputField otherReasonField = null;
	[SerializeField] private Dropdown enjoymentField = null;
	[SerializeField] private InputField suggestionField = null;
	[SerializeField] private string serverUri = "http://localhost";
	private Dictionary<SpaceStationController, QuestManager.Quest[]> rejectedQuests = null;
	private QuestManager.Quest quest = null;
	private List<string> selectedRewards = null;
	//private System.Threading.Tasks.Task<HttpResponseMessage> response = null;
	//private System.Threading.Tasks.Task<string> responseMessage = null;
	private HttpClient client = null;

	public static QuestFeedbackController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		rejectedQuests = new Dictionary<SpaceStationController, QuestManager.Quest[]>(4);
		client = new HttpClient();

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

	public void RejectQuests(QuestManager.Quest[] rejectedQuests, SpaceStationController sourceStation)
	{
		if(accepted)
		{
			this.rejectedQuests[sourceStation] = rejectedQuests;
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

			backstoryField.value = 0;
			questGiverField.value = 0;
			taskDifficultyField.value = 0;
			taskFunField.value = 0;
			rewardField.value = 0;
			effectField.value = 0;
			for(int i = 0; i < reasonFields.Length; ++i)
			{
				reasonFields[i].isOn = false;
			}
			otherReasonField.text = "";
			enjoymentField.value = 0;
			suggestionField.text = "";

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
		feedback.rejectedQuest1 = rejectedQuests[quest.destination].Length > 0 ? rejectedQuests[quest.destination][0] : null;
		feedback.rejectedQuest2 = rejectedQuests[quest.destination].Length > 1 ? rejectedQuests[quest.destination][1] : null;
		List<string> rejectedRewards1 = new List<string>(3);
		if(feedback.rejectedQuest1 != null)
		{
			foreach(KeyValuePair<string, int> reward in feedback.rejectedQuest1.rewards)
			{
				rejectedRewards1.Add(reward.Value + " " + reward.Key);
			}
		}
		feedback.rejectedRewards1 = rejectedRewards1;
		List<string> rejectedRewards2 = new List<string>(3);
		if(feedback.rejectedQuest2 != null)
		{
			foreach(KeyValuePair<string, int> reward in feedback.rejectedQuest2.rewards)
			{
				rejectedRewards2.Add(reward.Value + " " + reward.Key);
			}
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
		feedback.otherReason = Regex.Replace(otherReasonField.text, "[^a-zA-Z0-9_ ]{1}", "_");
		feedback.enjoymentFeedback = enjoymentField.value;
		feedback.suggestions = Regex.Replace(suggestionField.text, "[^a-zA-Z0-9_ ]{1}", "_");

		// Debug.Log(JsonUtility.ToJson(feedback));
		/*response = */
		client.GetAsync(serverUri + "?json=" + JsonUtility.ToJson(feedback));                           // PostAsync() does not work, I've tried for hours

		surveyPanel.SetActive(false);
	}

	/*private void Update()
	{
		if(response != null && response.IsCompleted)
		{
			Debug.Log(response.Result.StatusCode);
			responseMessage = response.Result.Content.ReadAsStringAsync();
		}

		if(responseMessage != null && responseMessage.IsCompleted)
		{
			Debug.Log(responseMessage.Result);
		}
	}*/

	public void CloseSurveyPanel()
	{
		surveyPanel.SetActive(false);
	}
}
