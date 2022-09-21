using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class MenuController : MonoBehaviour, IListener
{
	private static MenuController instance = null;

	[SerializeField] private RectTransform uiTransform = null;
	[SerializeField] private RectTransform moduleComponentSelectionPanel = null;
	[SerializeField] private GameObject stationMainMenu = null;
	[SerializeField] private Text stationNameField = null;
	[SerializeField] private GameObject stationQuestMenu = null;
	[SerializeField] private RectTransform[] stationQuestEntries = { };
	[SerializeField] private GameObject stationTradingMenu = null;
	[SerializeField] private Text playerMoneyField = null;
	[SerializeField] private Text nextUpdateField = null;
	[SerializeField] private Button[] amountButtons = { };
	[SerializeField] private Color amountHighlightColor = Color.blue;
	[SerializeField] private InputField customAmountField = null;
	[SerializeField] private float expensiveGoodFactor = 2.0f;
	[SerializeField] private Color goodPriceColor = Color.green;
	[SerializeField] private Color normalPriceColor = Color.white;
	[SerializeField] private Color badPriceColor = Color.red;
	[SerializeField] private float remoteTradeFeeFactor = 5.0f;
	[SerializeField] private float remoteTradeFeeRecalculationDistance = 500.0f;
	[SerializeField] private GameObject remoteTradeHint = null;
	[SerializeField] private Text stationMoneyField = null;
	[SerializeField] private RectTransform tradingContentPane = null;
	[SerializeField] private GameObject emptyListIndicator = null;
	[SerializeField] private RectTransform questVesselMenu = null;
	[SerializeField] private Text questVesselNameField = null;
	[SerializeField] private Text progressField = null;
	[SerializeField] private Text hintField = null;
	[SerializeField] private Button interactButton = null;
	[SerializeField] private GameObject mainMenu = null;
	[SerializeField] private BuildingMenu buildingMenu = null;
	[SerializeField] private Transform mapMarkerParent = null;
	[SerializeField] private Transform orbitMarkerParent = null;
	// Keep these Module Variables here instead of in Module so that we don't have to set them manually for each Module Prefab
	[SerializeField] private Button moduleMenuButtonPrefab = null;
	[SerializeField] private GameObject moduleMenuPrefab = null;
	[SerializeField] private RectTransform moduleComponentEntryPrefab = null;
	[SerializeField] private RectTransform moduleMenuButtonParent = null;
	[SerializeField] private RectTransform moduleMenuParent = null;
	private TimeController timeController = null;
	private GoodManager goodManager = null;
	private QuestManager questManager = null;
	private InfoController infoController = null;
	private SpaceStationController activeStation = null;
	private QuestVesselController activeQuestVessel = null;
	private SpacecraftController localPlayerMainSpacecraft = null;
	private Transform localPlayerMainTransform = null;
	private InventoryController localPlayerMainInventory = null;
	private InputController localPlayerMainInputController = null;
	private TimeController.Coroutine nextUpdateFieldCoroutine = null;
	private int amount = 1;
	private ColorBlock amountButtonColors = ColorBlock.defaultColorBlock;
	private ColorBlock amountButtonHighlightedColors = ColorBlock.defaultColorBlock;

	public static MenuController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		instance = this;
	}

	private void Start()
	{
		SpacecraftManager.GetInstance().AddSpacecraftChangeListener(this);
		Notify();

		timeController = TimeController.GetInstance();
		goodManager = GoodManager.GetInstance();
		questManager = QuestManager.GetInstance();
		infoController = InfoController.GetInstance();

		amountButtonColors = amountButtons[0].colors;
		amountButtonHighlightedColors.normalColor = amountHighlightColor;
		amountButtonHighlightedColors.highlightedColor = amountHighlightColor;
		amountButtonHighlightedColors.pressedColor = amountHighlightColor;
		amountButtonHighlightedColors.selectedColor = amountHighlightColor;

		SetHighlightedAmountButton(0);
	}

	public void Notify()
	{
		localPlayerMainSpacecraft = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft();
		localPlayerMainTransform = localPlayerMainSpacecraft.GetTransform();
		localPlayerMainInventory = localPlayerMainSpacecraft.GetComponent<InventoryController>();
		localPlayerMainInputController = localPlayerMainSpacecraft.GetComponent<InputController>();
	}

	public void ToggleMainMenu(bool forceOpen = false)
	{
		if(!forceOpen)
		{
			Button closeButton = FindCloseButton();
			if(closeButton != null)
			{
				closeButton.onClick.Invoke();
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
				timeController.TogglePause(mainMenu.activeSelf);
			}
		}
		else
		{
			mainMenu.SetActive(!mainMenu.activeSelf);
			timeController.TogglePause(mainMenu.activeSelf);
		}

		UpdateFlightControls();
	}

	public void CloseModuleComponentSelection()
	{
		moduleComponentSelectionPanel.gameObject.SetActive(false);
	}

	public void ToggleStationMenu(SpaceStationController requester, string name)
	{
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

	public void UpdateQuest(SpaceStationController requester, QuestManager.Quest quest, int position, bool active)
	{
		if(requester == activeStation)
		{
			if(quest != null)
			{
				stationQuestEntries[position].gameObject.SetActive(true);

				stationQuestEntries[position].GetChild(1).GetComponent<Text>().text = questManager.GetBackstoryDescription(quest.backstory);
				stationQuestEntries[position].GetChild(3).GetComponent<Text>().text = questManager.GetQuestGiverDescription(quest.questGiver);
				stationQuestEntries[position].GetChild(5).GetComponent<Text>().text = questManager.GetTaskDescription(quest.task);

				StringBuilder rewardString = new StringBuilder();
				foreach(KeyValuePair<string, int> reward in quest.rewards)
				{
					rewardString.AppendLine(reward.Value + (reward.Key != "$" ? " " : "") + reward.Key);
				}
				stationQuestEntries[position].GetChild(7).GetComponent<Text>().text = rewardString.ToString();

				Button negativeButton = stationQuestEntries[position].GetChild(8).GetComponentInChildren<Button>();
				Button positiveButton = stationQuestEntries[position].GetChild(9).GetComponentInChildren<Button>();
				if(active)
				{
					stationQuestEntries[0].gameObject.SetActive(false);
					stationQuestEntries[2].gameObject.SetActive(false);

					negativeButton.gameObject.SetActive(true);
					SpaceStationController localActiveStation = activeStation;
					negativeButton.onClick.RemoveAllListeners();
					negativeButton.onClick.AddListener(delegate
					{
						questManager.AbandonQuest(localActiveStation);
						localActiveStation.UpdateQuests();
					});

					positiveButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(45.0f, -115.0f);
					if(quest.progress < 1.0f)
					{
						positiveButton.interactable = false;
						positiveButton.GetComponentInChildren<Text>().text = Mathf.FloorToInt(quest.progress * 100.0f) + "%";
					}
					else
					{
						if(localPlayerMainSpacecraft.IsDockedToStation())
						{
							positiveButton.interactable = true;
							positiveButton.GetComponentInChildren<Text>().text = "Complete";
							positiveButton.onClick.RemoveAllListeners();
							positiveButton.onClick.AddListener(delegate
							{
								questManager.CompleteQuest(localActiveStation);
								localActiveStation.UpdateQuests();
							});
						}
						else
						{
							positiveButton.interactable = false;
							positiveButton.GetComponentInChildren<Text>().text = "Get back here!";
						}
					}
				}
				else
				{
					stationQuestEntries[position].gameObject.SetActive(true);

					negativeButton.gameObject.SetActive(false);

					positiveButton.interactable = true;
					positiveButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0.0f, -115.0f);
					positiveButton.GetComponentInChildren<Text>().text = "Accept";
					QuestManager.Quest localQuest = quest;
					SpaceStationController localActiveStation = activeStation;
					positiveButton.onClick.RemoveAllListeners();
					positiveButton.onClick.AddListener(delegate
					{
						questManager.AcceptQuest(localQuest);
						localActiveStation.UpdateQuests();
					});
				}
			}
			else
			{
				stationQuestEntries[position].gameObject.SetActive(false);
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

	public void UpdateTrading(SpaceStationController requester, RectTransform[] tradingEntries, int stationMoney, double lastStationUpdate, float stationUpdateInterval, bool remoteTrade)
	{
		if(requester == activeStation)
		{
			if(nextUpdateFieldCoroutine != null)
			{
				timeController.StopCoroutine(nextUpdateFieldCoroutine);
				nextUpdateFieldCoroutine = null;
			}
			nextUpdateFieldCoroutine = timeController.StartCoroutine(UpdateNextUpdateField(lastStationUpdate, stationUpdateInterval), false);

			int playerMoney = localPlayerMainInventory.GetMoney();
			playerMoneyField.text = playerMoney + "$";
			stationMoneyField.text = stationMoney + "$";

			remoteTradeHint.SetActive(remoteTrade);

			for(int i = 1; i < tradingContentPane.childCount; ++i)
			{
				GameObject.Destroy(tradingContentPane.GetChild(i).gameObject);
			}

			foreach(RectTransform tradingEntry in tradingEntries)
			{
				// Place Entry in List
				tradingEntry.SetParent(tradingContentPane, false);

				// Get Good Data and Stocks
				InventoryController playerInventory = localPlayerMainInventory;
				InventoryController stationInventory = requester.GetInventoryController();
				string goodName = tradingEntry.GetChild(0).GetComponentInChildren<Text>().text;
				GoodManager.Good good = goodManager.GetGood(goodName);
				uint playerAmount = playerInventory.GetGoodAmount(goodName);
				uint stationAmount = stationInventory.GetGoodAmount(goodName);

				// Calculate Amount
				int amount = this.amount;
				// Sell MAX
				if(amount == -1)
				{
					amount = Mathf.Min((int)playerAmount, (int)stationInventory.GetFreeCapacity(good));
					int maxPrice = requester.CalculateGoodPrice(goodName, stationAmount, amount)
						- (remoteTrade ? Mathf.CeilToInt(requester.GetTeleporter().CalculateTeleportationEnergyCost(
						requester.GetTransform().position, localPlayerMainTransform.position, (good.mass * amount)) * remoteTradeFeeFactor) : 0);
					while(maxPrice > stationMoney)
					{
						--amount;
						maxPrice = requester.CalculateGoodPrice(goodName, stationAmount, amount)
							- (remoteTrade ? Mathf.CeilToInt(requester.GetTeleporter().CalculateTeleportationEnergyCost(
							requester.GetTransform().position, localPlayerMainTransform.position, (good.mass * amount)) * remoteTradeFeeFactor) : 0);
					}
				}
				// Buy MAX
				else if(amount == -2)
				{
					amount = Mathf.Min((int)stationAmount, (int)playerInventory.GetFreeCapacity(good));
					int maxPrice = requester.CalculateGoodPrice(goodName, stationAmount, -amount)
						+ (remoteTrade ? Mathf.CeilToInt(requester.GetTeleporter().CalculateTeleportationEnergyCost(
						requester.GetTransform().position, localPlayerMainTransform.position, (good.mass * amount)) * remoteTradeFeeFactor) : 0);
					while(maxPrice > playerMoney)
					{
						--amount;
						maxPrice = requester.CalculateGoodPrice(goodName, stationAmount, -amount)
							+ (remoteTrade ? Mathf.CeilToInt(requester.GetTeleporter().CalculateTeleportationEnergyCost(
							requester.GetTransform().position, localPlayerMainTransform.position, (good.mass * amount)) * remoteTradeFeeFactor) : 0);
					}
				}

				// Set Entry Fields
				InfoController localInfoController = infoController;
				string localDescription = good.description;
				tradingEntry.GetChild(0).GetComponent<Button>().onClick.AddListener(delegate
				{
					localInfoController.AddMessage(localDescription, false);
				});
				tradingEntry.GetChild(1).GetComponent<Text>().text = good.state.ToString();
				tradingEntry.GetChild(2).GetComponent<Text>().text = (good.volume * amount) + " m3";
				tradingEntry.GetChild(3).GetComponent<Text>().text = (good.mass * amount).ToString("F2") + " t";
				int localAmount = amount;
				int buyPrice = requester.CalculateGoodPrice(goodName, stationAmount, -amount);
				Text buyPriceLabel = tradingEntry.GetChild(5).GetComponentInChildren<Text>();
				Button buyButton = tradingEntry.GetChild(5).GetComponent<Button>();
				if(stationAmount > 0)
				{
					buyPrice += remoteTrade ? Mathf.CeilToInt(requester.GetTeleporter().CalculateTeleportationEnergyCost(
						requester.GetTransform().position, localPlayerMainTransform.position, (good.mass * amount)) * remoteTradeFeeFactor) : 0;
					buyPriceLabel.text = buyPrice + "$";

					buyButton.gameObject.SetActive(true);
					buyButton.onClick.AddListener(delegate
					{
						requester.Trade(goodName, (uint)localAmount, playerInventory, stationInventory, stationAmount, false, false, buyPrice);
					});
				}
				else
				{
					buyPriceLabel.text = string.Empty;

					buyButton.gameObject.SetActive(false);
				}
				int sellPrice = requester.CalculateGoodPrice(goodName, stationAmount, amount);
				Text sellPriceLabel = tradingEntry.GetChild(6).GetComponentInChildren<Text>();
				Button sellButton = tradingEntry.GetChild(6).GetComponent<Button>();
				if(playerAmount > 0)
				{
					sellPrice -= remoteTrade ? Mathf.CeilToInt(requester.GetTeleporter().CalculateTeleportationEnergyCost(
						requester.GetTransform().position, localPlayerMainTransform.position, (good.mass * amount)) * remoteTradeFeeFactor) : 0;
					sellPrice = Mathf.Max(sellPrice, 0);
					sellPriceLabel.text = sellPrice + "$";

					sellButton.gameObject.SetActive(true);
					sellButton.onClick.AddListener(delegate
					{
						requester.Trade(goodName, (uint)localAmount, stationInventory, playerInventory, stationAmount, false, false, sellPrice);
					});
				}
				else
				{
					sellPriceLabel.text = string.Empty;

					sellButton.gameObject.SetActive(false);
				}

				// Mark cheap and expensive Prices
				amount = Mathf.Max(amount, 1);
				if((buyPrice / (float)amount) < good.price)
				{
					buyPriceLabel.color = goodPriceColor;
					if(infoController.IsColorblindModeActivated())
					{
						buyPriceLabel.text += " +";
					}
				}
				else if((buyPrice / (float)amount) > good.price * expensiveGoodFactor)
				{
					buyPriceLabel.color = badPriceColor;
					if(infoController.IsColorblindModeActivated())
					{
						buyPriceLabel.text += " -";
					}
				}
				else
				{
					buyPriceLabel.color = normalPriceColor;
				}
				if((sellPrice / (float)amount) < good.price)
				{
					sellPriceLabel.color = badPriceColor;
					if(infoController.IsColorblindModeActivated())
					{
						sellPriceLabel.text += " -";
					}
				}
				else if((sellPrice / (float)amount) > good.price * expensiveGoodFactor)
				{
					sellPriceLabel.color = goodPriceColor;
					if(infoController.IsColorblindModeActivated())
					{
						sellPriceLabel.text += " +";
					}
				}
				else
				{
					sellPriceLabel.color = normalPriceColor;
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
		stationMainMenu?.SetActive(false);
		stationQuestMenu?.SetActive(false);
		stationTradingMenu?.SetActive(false);
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
		bool flightControls = !buildingMenu.gameObject.activeSelf && (FindCloseButton() == null);

		localPlayerMainInputController.SetFlightControls(flightControls);
		infoController.SetFlightControls(flightControls);
	}

	public void ResetTarget()
	{
		localPlayerMainSpacecraft.GetComponent<PlayerSpacecraftUIController>().SetTarget(null, null, null);
	}

	private Button FindCloseButton()
	{
		Button[] buttons = uiTransform.GetComponentsInChildren<Button>();
		for(int i = buttons.Length - 1; i > 0; --i)
		{
			Text buttonText = buttons[i].GetComponentInChildren<Text>();
			if(buttonText != null && buttonText.text == "Close")
			{
				return buttons[i];
			}
		}

		return null;
	}

	private IEnumerator<float> UpdateNextUpdateField(double lastStationUpdate, float stationUpdateInterval)
	{
		int remainingTime = 0;
		float lastDistance = (activeStation.GetTransform().position - localPlayerMainTransform.position).magnitude;
		while(stationTradingMenu.activeSelf && remainingTime >= 0)
		{
			float currentDistance = (activeStation.GetTransform().position - localPlayerMainTransform.position).magnitude;
			if(remoteTradeHint.activeSelf && Math.Abs(lastDistance - currentDistance) >= remoteTradeFeeRecalculationDistance)
			{
				activeStation.UpdateTrading();
				lastDistance = currentDistance;
			}

			remainingTime = Math.Max((int)((lastStationUpdate + stationUpdateInterval) - timeController.GetTime()), 0);
			nextUpdateField.text = remainingTime + " s";
			yield return 1.0f;
		}
	}

	public RectTransform GetUITransform()
	{
		return uiTransform;
	}

	public Transform GetMapMarkerParent()
	{
		return mapMarkerParent;
	}

	public Transform GetOrbitMarkerParent()
	{
		return orbitMarkerParent;
	}

	public Button GetModuleMenuButtonPrefab()
	{
		return moduleMenuButtonPrefab;
	}

	public GameObject GetModuleMenuPrefab()
	{
		return moduleMenuPrefab;
	}

	public RectTransform GetModuleComponentEntryPrefab()
	{
		return moduleComponentEntryPrefab;
	}

	public RectTransform GetModuleMenuButtonParent()
	{
		return moduleMenuButtonParent;
	}

	public RectTransform GetModuleMenuParent()
	{
		return moduleMenuParent;
	}

	public RectTransform GetModuleComponentSelectionPanel()
	{
		return moduleComponentSelectionPanel;
	}

	public ColorBlock GetAmountButtonColors()
	{
		return amountButtonColors;
	}

	public ColorBlock GetAmountButtonHighlightedColors()
	{
		return amountButtonHighlightedColors;
	}

	public bool StationIsQuesting(SpaceStationController requester)
	{
		return requester == activeStation && stationQuestMenu.activeSelf;
	}

	public bool StationIsTrading(SpaceStationController requester)
	{
		return requester == activeStation && stationTradingMenu.activeSelf;
	}

	public void SetTradeAmount(int amount)
	{
		this.amount = amount;
		activeStation.UpdateTrading();
	}

	public void SetCustomTradeAmount()
	{
		if(customAmountField.text.StartsWith("-"))
		{
			customAmountField.text = customAmountField.text.Remove(0, 1);
		}
		amount = int.Parse(customAmountField.text);
		activeStation.UpdateTrading();

		SetHighlightedAmountButton(-1);
		customAmountField.colors = amountButtonHighlightedColors;
	}

	public void SetHighlightedAmountButton(int id)
	{
		for(int i = 0; i < amountButtons.Length; ++i)
		{
			if(i == id)
			{
				amountButtons[i].colors = amountButtonHighlightedColors;
			}
			else
			{
				amountButtons[i].colors = amountButtonColors;
			}
		}

		customAmountField.colors = amountButtonColors;
	}
}
