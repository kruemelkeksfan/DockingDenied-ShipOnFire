using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class MenuController : MonoBehaviour, IListener
{
	private static MenuController instance = null;
	private static WaitForSecondsRealtime waitASecond = null;

	[SerializeField] private RectTransform uiTransform = null;
	[SerializeField] private RectTransform moduleMenu = null;
	[SerializeField] private InputField moduleNameField = null;
	[SerializeField] private Dropdown hotkeySelectionField = null;
	[SerializeField] private GameObject stationMainMenu = null;
	[SerializeField] private Text stationNameField = null;
	[SerializeField] private GameObject stationQuestMenu = null;
	[SerializeField] private RectTransform[] stationQuestEntries = { };
	[SerializeField] private GameObject stationTradingMenu = null;
	[SerializeField] private Text playerMoneyField = null;
	[SerializeField] private Text stationMoneyField = null;
	[SerializeField] private Text nextUpdateField = null;
	[SerializeField] private RectTransform tradingContentPane = null;
	[SerializeField] private GameObject emptyListIndicator = null;
	[SerializeField] private RectTransform questVesselMenu = null;
	[SerializeField] private Text questVesselNameField = null;
	[SerializeField] private Text progressField = null;
	[SerializeField] private Text hintField = null;
	[SerializeField] private Button interactButton = null;
	[SerializeField] private GameObject mainMenu = null;
	[SerializeField] private BuildingMenu buildingMenu = null;
	[SerializeField] private InventoryScreenController inventoryMenu = null;
	// TODO: Somehow control Flight Control bool with all of this
	private QuestManager questManager = null;
	private HotkeyModule activeModule = null;
	private SpaceStationController activeStation = null;
	private QuestVesselController activeQuestVessel = null;
	private Dictionary<string, string> amountSettings = null;
	private Spacecraft localPlayerMainSpacecraft = null;
	private InventoryController localPlayerMainInventory = null;
	private InputController localPlayerMainInputController = null;

	public static MenuController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		amountSettings = new Dictionary<string, string>();

		instance = this;
	}

	private void Start()
	{
		if(waitASecond == null)
		{
			waitASecond = new WaitForSecondsRealtime(1.0f);
		}

		SpacecraftManager.GetInstance().AddSpacecraftChangeListener(this);
		Notify();

		questManager = QuestManager.GetInstance();
	}

	public void Notify()
	{
		localPlayerMainSpacecraft = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft();
		localPlayerMainInventory = localPlayerMainSpacecraft.GetComponent<InventoryController>();
		localPlayerMainInputController = localPlayerMainSpacecraft.GetComponent<InputController>();
	}

	public void ToggleMainMenu(bool forceOpen = false)
	{
		if(!forceOpen)
		{
			if(activeModule != null)
			{
				CloseModuleMenu();
			}
			else if(activeStation != null)
			{
				if(stationQuestMenu.activeSelf)
				{
					ToggleQuests();
				}
				else if(stationTradingMenu.activeSelf)
				{
					ToggleTrading();
				}
				else
				{
					CloseStationMenu();
				}
			}
			else if(activeQuestVessel != null)
			{
				CloseQuestVesselMenu();
			}
			else if(inventoryMenu.gameObject.activeSelf)
			{
				inventoryMenu.ToggleInventoryMenu();
			}
			else if(buildingMenu.gameObject.activeSelf)
			{
				if(!buildingMenu.DeselectModule())
				{
					buildingMenu.ToggleBuildingMenu();
				}
			}
			else
			{
				mainMenu.SetActive(!mainMenu.activeSelf);
			}
		}
		else
		{
			mainMenu.SetActive(!mainMenu.activeSelf);
		}

		UpdateFlightControls();
	}

	public void ToggleModuleMenu(HotkeyModule requester)
	{
		CloseStationMenu();
		CloseQuestVesselMenu();

		if(requester != activeModule)
		{
			moduleMenu.gameObject.SetActive(true);

			activeModule = requester;

			moduleNameField.text = requester.GetActionName();
			hotkeySelectionField.value = requester.GetHotkey();
		}
		else
		{
			CloseModuleMenu();
		}

		UpdateFlightControls();
	}

	public void ModuleNameChanged()
	{
		activeModule.SetActionName(moduleNameField.text);
	}

	public void HotkeySelectionChanged()
	{
		activeModule.SetHotkey(hotkeySelectionField.value);
	}

	public void CloseModuleMenu()
	{
		moduleMenu.gameObject.SetActive(false);
		activeModule = null;
		UpdateFlightControls();
	}

	public void ToggleStationMenu(SpaceStationController requester, string name)
	{
		CloseModuleMenu();
		CloseQuestVesselMenu();

		if(requester != activeStation)
		{
			CloseStationMenu();                                                     // Do this inside if/else because we need the old activeStation to evaluate the Condition before setting it to null in CloseStationMenu()
			stationMainMenu.SetActive(true);
			activeStation = requester;
			stationNameField.text = name;
		}
		else
		{
			CloseStationMenu();
		}

		UpdateFlightControls();
	}

	public void RequestDocking()
	{
		activeStation.RequestDocking(localPlayerMainSpacecraft);
	}

	public void ToggleQuests()
	{
		stationQuestMenu.SetActive(!stationQuestMenu.activeSelf);
		stationMainMenu.SetActive(!stationMainMenu.activeSelf);

		if(stationQuestMenu.activeSelf)
		{
			activeStation.UpdateQuests();
		}
	}

	public void UpdateQuest(SpaceStationController requester, QuestManager.Quest quest, int position, bool active, bool playerDocked = false)
	{
		if(requester == activeStation)
		{
			stationQuestEntries[position].GetChild(1).GetComponent<Text>().text = questManager.GetBackstoryDescription(quest.backstory);
			stationQuestEntries[position].GetChild(3).GetComponent<Text>().text = questManager.GetQuestGiverDescription(quest.questGiver);
			stationQuestEntries[position].GetChild(5).GetComponent<Text>().text = questManager.GetTaskDescription(quest.task);

			StringBuilder rewardString = new StringBuilder();
			foreach(KeyValuePair<string, int> reward in quest.rewards)
			{
				rewardString.AppendLine(reward.Value + (reward.Key != "$" ? " " : "") + reward.Key);
			}
			stationQuestEntries[position].GetChild(7).GetComponent<Text>().text = rewardString.ToString();

			Button button = stationQuestEntries[position].GetChild(8).GetComponentInChildren<Button>();
			if(active)
			{
				stationQuestEntries[0].gameObject.SetActive(false);
				stationQuestEntries[2].gameObject.SetActive(false);

				if(quest.progress < 1.0f)
				{
					button.interactable = false;
					button.GetComponentInChildren<Text>().text = Mathf.FloorToInt(quest.progress * 100.0f) + "%";
				}
				else
				{
					if(playerDocked)
					{
						button.interactable = true;
						button.GetComponentInChildren<Text>().text = "Complete";
						SpaceStationController localActiveStation = activeStation;
						button.onClick.AddListener(delegate
						{
							questManager.CompleteQuest(localActiveStation);
							localActiveStation.UpdateQuests();
						});
					}
					else
					{
						button.interactable = false;
						button.GetComponentInChildren<Text>().text = "Get back here!";
					}
				}
			}
			else
			{
				stationQuestEntries[position].gameObject.SetActive(true);

				button.interactable = true;
				button.GetComponentInChildren<Text>().text = "Accept";
				QuestManager.Quest localQuest = quest;
				SpaceStationController localActiveStation = activeStation;
				button.onClick.AddListener(delegate
				{
					questManager.AcceptQuest(localQuest);
					localActiveStation.UpdateQuests();
				});
			}
		}
		else
		{
			Debug.LogWarning("Call to SubmitQuest() in MenuController by unauthorized Station!");
		}
	}

	public void ToggleTrading()
	{
		stationTradingMenu.SetActive(!stationTradingMenu.activeSelf);
		stationMainMenu.SetActive(!stationMainMenu.activeSelf);

		if(stationTradingMenu.activeSelf)
		{
			activeStation.UpdateTrading();
		}
	}

	public void UpdateTrading(SpaceStationController requester, RectTransform[] tradingEntries, int stationMoney, float lastStationUpdate, float stationUpdateInterval)
	{
		if(requester == activeStation)
		{
			StopAllCoroutines();
			StartCoroutine(UpdateNextUpdateField(lastStationUpdate, stationUpdateInterval));

			playerMoneyField.text = localPlayerMainInventory.GetMoney() + "$";
			stationMoneyField.text = stationMoney + "$";

			amountSettings.Clear();
			for(int i = 2; i < tradingContentPane.childCount; ++i)
			{
				Transform child = tradingContentPane.GetChild(i);
				amountSettings[child.GetChild(0).GetComponent<Text>().text] = child.GetChild(5).GetComponent<InputField>().text;
				GameObject.Destroy(child.gameObject);
			}

			foreach(RectTransform tradingEntry in tradingEntries)
			{
				tradingEntry.SetParent(tradingContentPane, false);

				string goodName = tradingEntry.GetChild(0).GetComponent<Text>().text;
				if(amountSettings.ContainsKey(goodName))
				{
					tradingEntry.GetChild(5).GetComponent<InputField>().text = amountSettings[goodName];
				}
			}

			if(tradingEntries.Length <= 0)
			{
				emptyListIndicator.SetActive(true);
			}
			else
			{
				emptyListIndicator.SetActive(false);
			}
		}
		else
		{
			Debug.LogWarning("Call to UpdateTrading() in MenuController by unauthorized Station!");
		}
	}

	public void CloseStationMenu()
	{
		stationMainMenu.SetActive(false);
		stationQuestMenu.SetActive(false);
		stationTradingMenu.SetActive(false);
		activeStation = null;
		UpdateFlightControls();
	}

	public void CloseStationMenu(SpaceStationController requester)
	{
		if(requester == null || requester == activeStation)
		{
			CloseStationMenu();
		}
	}

	public void ToggleQuestVesselMenu(QuestVesselController requester, string name, string progress, string hint, string interactionLabel, UnityAction interaction)
	{
		CloseModuleMenu();
		CloseStationMenu();

		if(requester != activeQuestVessel)
		{
			questVesselMenu.gameObject.SetActive(true);

			activeQuestVessel = requester;
			questVesselNameField.text = name;
			progressField.text = progress;
			hintField.text = hint;
			if(interaction != null)
			{
				interactButton.gameObject.SetActive(true);
				interactButton.onClick.RemoveAllListeners();
				interactButton.onClick.AddListener(interaction);
				interactButton.GetComponentInChildren<Text>().text = interactionLabel;
			}
			else
			{
				interactButton.gameObject.SetActive(false);
			}
		}
		else
		{
			CloseQuestVesselMenu();
		}

		UpdateFlightControls();
	}

	public void UpdateQuestVesselMenu(QuestVesselController requester, string name, string progress, string hint, string interactionLabel, UnityAction interaction)
	{
		if(requester == activeQuestVessel)
		{
			questVesselNameField.text = name;
			progressField.text = progress;
			hintField.text = hint;
			if(interaction != null)
			{
				interactButton.gameObject.SetActive(true);
				interactButton.onClick.RemoveAllListeners();
				interactButton.onClick.AddListener(interaction);
				interactButton.GetComponentInChildren<Text>().text = interactionLabel;
			}
			else
			{
				interactButton.gameObject.SetActive(false);
			}
		}
	}

	public void CloseQuestVesselMenu()
	{
		questVesselMenu.gameObject.SetActive(false);
		activeQuestVessel = null;
		UpdateFlightControls();
	}

	public void UpdateFlightControls()
	{
		localPlayerMainInputController.SetFlightControls(activeModule == null && activeStation == null && activeQuestVessel == null && !buildingMenu.gameObject.activeSelf && !mainMenu.activeSelf);
	}

	private IEnumerator UpdateNextUpdateField(float lastStationUpdate, float stationUpdateInterval)
	{
		int remainingTime = 0;
		while(stationTradingMenu.activeSelf && remainingTime >= 0)
		{
			remainingTime = Mathf.FloorToInt((lastStationUpdate + (stationUpdateInterval / Time.timeScale)) - Time.realtimeSinceStartup);
			nextUpdateField.text = remainingTime + " Seconds";
			yield return waitASecond;
		}
	}

	public RectTransform GetUITransform()
	{
		return uiTransform;
	}

	public bool StationIsQuesting(SpaceStationController requester)
	{
		return requester == activeStation && stationQuestMenu.activeSelf;
	}

	public bool StationIsTrading(SpaceStationController requester)
	{
		return requester == activeStation && stationTradingMenu.activeSelf;
	}
}
