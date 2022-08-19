using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class SpaceStationController : MonoBehaviour, IUpdateListener, IDockingListener, IListener
{
	private struct GoodTradingInfo
	{
		public uint playerAmount;
		public uint stationAmount;

		public GoodTradingInfo(uint playerAmount, uint stationAmount)
		{
			this.playerAmount = playerAmount;
			this.stationAmount = stationAmount;
		}
	}

	[SerializeField] private RectTransform mapMarkerPrefab = null;
	[Tooltip("Distance up from which no more digital Digits will be displayed")]
	[SerializeField] private float decimalDigitThreshold = 100.0f;
	[Tooltip("Maximum Distance from which a Docking Permission can be granted")]
	[SerializeField] private float maxApproachDistance = 1.0f;
	[Tooltip("Maximum Time in Seconds before Docking Permission expires")]
	[SerializeField] private float dockingTimeout = 600.0f;
	[Tooltip("Delay before an unauthorized Docking Port is disconnected after Docking")]
	[SerializeField] private float cancelDockingDelay = 0.5f;
	[SerializeField] private float stationUpdateInterval = 600.0f;
	[Tooltip("Determines the maximum Amount of this Good this Station will stockpile by itself, depending on the Consumption.")]
	[SerializeField] private float maxGoodStockFactor = 12.0f;
	[Tooltip("When the Stocks are precisely at this Factor times the Consumption, Good Prices equal Base Good Prices.")]
	[SerializeField] private float targetGoodStockFactor = 4.0f;
	[Tooltip("Minimum Money Change of the Station per Economy Update.")]
	[SerializeField] private int minProfit = -100;
	[Tooltip("Maximum Money Change of the Station per Economy Update.")]
	[SerializeField] private int maxProfit = 200;
	[SerializeField] private RectTransform tradingEntryPrefab = null;
	[SerializeField] private ColorBlock questStationMarkerColors = new ColorBlock();
	[SerializeField] private Color questStationTextColor = Color.red;
	[SerializeField] private AudioClip tradeAudio = null;
	private TimeController timeController = null;
	private AudioController audioController = null;
	private GoodManager goodManager = null;
	private QuestManager questManager = null;
	private MenuController menuController = null;
	private InfoController infoController = null;
	private SpacecraftController spacecraft = null;
	private new Transform transform = null;
	private new Rigidbody2D rigidbody = null;
	private InventoryController inventoryController = null;
	private RectTransform uiTransform = null;
	private SpacecraftController localPlayerMainSpacecraft = null;
	private Transform localPlayerMainTransform = null;
	private InventoryController localPlayerMainInventory = null;
	private PlayerSpacecraftUIController playerSpacecraftController = null;
	private new Camera camera = null;
	private Transform cameraTransform = null;
	private RectTransform mapMarker = null;
	private Text mapMarkerName = null;
	private Text mapMarkerDistance = null;
	private Button mapMarkerButton = null;
	private string stationName = null;
	private DockingPort[] dockingPorts = null;
	private TimeController.Coroutine dockingTimeoutCoroutine = null;
	private Dictionary<DockingPort, SpacecraftController> expectedDockings = null;
	private HashSet<SpacecraftController> dockedSpacecraft = null;
	private QuestManager.TaskType[] firstTasks = null;
	private QuestManager.TaskType[] secondaryTasks = null;
	private QuestManager.TaskType[] allTasks = null;
	private QuestManager.Quest[] questSelection = null;
	private bool updateQuestSelection = true;
	private double lastStationUpdate = 0.0;
	private Dictionary<string, GoodTradingInfo> tradingInventory;
	private List<string> goodNames = null;
	private bool spawnProtection = true;
	private bool newActiveQuest = true;
	private ColorBlock originalMarkerColors = new ColorBlock();
	private Color originalTextColor = Color.yellow;

	private void Start()
	{
		maxApproachDistance *= maxApproachDistance;
		spacecraft = GetComponent<SpacecraftController>();
		transform = spacecraft.GetTransform();
		rigidbody = GetComponent<Rigidbody2D>();
		inventoryController = GetComponent<InventoryController>();
		camera = Camera.main;
		cameraTransform = camera.GetComponent<Transform>();
		dockingPorts = GetComponentsInChildren<DockingPort>();
		foreach(DockingPort port in dockingPorts)
		{
			port.AddDockingListener(this);
		}

		expectedDockings = new Dictionary<DockingPort, SpacecraftController>();
		dockedSpacecraft = new HashSet<SpacecraftController>();
		tradingInventory = new Dictionary<string, GoodTradingInfo>();
		goodNames = new List<string>();

		audioController = AudioController.GetInstance();
		goodManager = GoodManager.GetInstance();
		questManager = QuestManager.GetInstance();
		infoController = InfoController.GetInstance();
		SpacecraftManager.GetInstance().AddSpacecraftChangeListener(this);
		Notify();

		firstTasks = new QuestManager.TaskType[] { QuestManager.TaskType.Bribe, QuestManager.TaskType.JumpStart, QuestManager.TaskType.Tow };
		secondaryTasks = new QuestManager.TaskType[] { QuestManager.TaskType.Trade };
		allTasks = (QuestManager.TaskType[])Enum.GetValues(typeof(QuestManager.TaskType));

		Dictionary<string, GoodManager.Good> goods = goodManager.GetGoodDictionary();
		foreach(string goodName in goods.Keys)
		{
			inventoryController.Deposit(goodName, (uint)Mathf.CeilToInt(goods[goodName].consumption * maxGoodStockFactor));
		}

		timeController = TimeController.GetInstance();
		timeController.AddUpdateListener(this);

		timeController.StartCoroutine(UpdateStation(), false);
	}

	private void OnDestroy()
	{
		if(mapMarker != null)
		{
			GameObject.Destroy(mapMarker.gameObject);
		}

		timeController?.RemoveUpdateListener(this);
	}

	public void UpdateNotify()
	{
		Vector2? uiPoint = ScreenUtility.WorldToUIPoint(transform.position, camera, cameraTransform, uiTransform);
		if(uiPoint.HasValue)
		{
			mapMarker.localScale = Vector3.one;
			mapMarker.anchoredPosition = uiPoint.Value;

			float distance = (transform.position - localPlayerMainTransform.position).magnitude;
			if(distance > decimalDigitThreshold)
			{
				mapMarkerDistance.text = (distance / 1000.0f).ToString("F0") + "km";
			}
			else
			{
				mapMarkerDistance.text = (distance / 1000.0f).ToString("F2") + "km";
			}
		}
		else
		{
			mapMarker.localScale = Vector3.zero;
		}
	}

	public void Docked(DockingPort port, DockingPort otherPort)
	{
		if(expectedDockings.ContainsKey(port))
		{
			SpacecraftController otherSpacecraft = otherPort.GetComponentInParent<SpacecraftController>();
			if(expectedDockings[port] == otherSpacecraft)
			{
				expectedDockings.Remove(port);
				dockedSpacecraft.Add(otherSpacecraft);

				if(otherSpacecraft == localPlayerMainSpacecraft)
				{
					timeController.StopCoroutine(dockingTimeoutCoroutine);
					dockingTimeoutCoroutine = null;
					infoController.SetDockingExpiryTime(-1.0f);
				}
			}
			else
			{
				if(otherSpacecraft == localPlayerMainSpacecraft)
				{
					infoController.AddMessage("You have no Docking Permission for this Docking Port!", true);
				}
				
				timeController.StartCoroutine(CancelDocking(otherPort), false);
			}
		}

		// StartCoroutine(SpawnController.GetInstance().DespawnObject(rigidbody));								// Used for Despawn Testing
	}

	private IEnumerator<float> CancelDocking(DockingPort otherPort)
	{
		// Disconnect with Delay, so that the Joints have some time to adjust themselves, else the Physics freak out
		yield return cancelDockingDelay;

		otherPort.HotkeyDown();
	}

	public void Undocked(DockingPort port, DockingPort otherPort)
	{
		dockedSpacecraft.Remove(otherPort.GetComponentInParent<SpacecraftController>());

		if(port.IsActive() && !expectedDockings.ContainsKey(port))
		{
			port.HotkeyDown();
		}
		if(otherPort.GetComponentInParent<SpacecraftController>() == localPlayerMainSpacecraft)
		{
			menuController.CloseStationMenu(this);
			infoController.AddMessage("Undocking successful, good Flight!", false);
		}
	}

	public void Notify()
	{
		localPlayerMainSpacecraft = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft();
		localPlayerMainTransform = localPlayerMainSpacecraft.GetTransform();
		localPlayerMainInventory = localPlayerMainSpacecraft.GetComponent<InventoryController>();
		playerSpacecraftController = localPlayerMainSpacecraft.GetComponent<PlayerSpacecraftUIController>();
	}

	public void ToggleStationMenu()
	{
		playerSpacecraftController.SetTarget(spacecraft, transform, rigidbody);
		menuController.ToggleStationMenu(this, stationName);
	}

	public void RequestDocking(SpacecraftController requester)
	{
		// TODO: Check if Ship is on Fire etc.
		if(!dockedSpacecraft.Contains(requester))
		{
			if(!expectedDockings.ContainsValue(requester))
			{
				if(requester != localPlayerMainSpacecraft || (requester.GetTransform().position - transform.position).sqrMagnitude <= maxApproachDistance)
				{
					float maxAngle = float.MinValue;
					DockingPort alignedPort = null;
					foreach(DockingPort port in dockingPorts)
					{
						if(!port.IsActive() && port.IsFree())
						{
							Vector2 approachVector = (requester.GetTransform().position - port.GetTransform().position).normalized;
							float dot = Vector2.Dot(port.GetTransform().up, approachVector);
							if(dot > maxAngle)
							{
								maxAngle = dot;
								alignedPort = port;
							}
						}
					}

					if(alignedPort != null)
					{
						expectedDockings.Add(alignedPort, requester);
						alignedPort.HotkeyDown();

						if(requester == localPlayerMainSpacecraft)
						{
							dockingTimeoutCoroutine = timeController.StartCoroutine(DockingTimeout(alignedPort, requester), false);

							infoController.AddMessage("Docking Permission granted for Docking Port " + alignedPort.GetActionName() + "!", false);
						}
					}
					else if(requester == localPlayerMainSpacecraft)
					{
						infoController.AddMessage("No free Docking Ports available!", true);
					}
				}
				else if(requester == localPlayerMainSpacecraft)
				{
					infoController.AddMessage("You are too far away to request Docking Permission!", true);
				}
			}
			else if(requester == localPlayerMainSpacecraft)
			{
				string portName = "Unknown Port";
				foreach(DockingPort port in expectedDockings.Keys)
				{
					if(expectedDockings[port] == localPlayerMainSpacecraft)
					{
						portName = port.GetActionName();
						break;
					}
				}
				infoController.AddMessage("You already have an active Docking Permission for Port " + portName + "!", true);
			}
		}
		else if(requester == localPlayerMainSpacecraft)
		{
			infoController.AddMessage("You are already docked at this Station!", true);
		}
	}

	public void AbortDocking(SpacecraftController requester)
	{
		foreach(DockingPort port in expectedDockings.Keys)
		{
			if(expectedDockings[port] == requester)
			{
				port.HotkeyDown();
				expectedDockings.Remove(port);
				break;
			}
		}
	}

	public void UpdateQuests()
	{
		if(menuController.StationIsQuesting(this))
		{
			QuestManager.Quest activeQuest = questManager.GetActiveQuest(this);
			if(activeQuest != null)
			{
				if(newActiveQuest)
				{
					for(int j = 0; j < questSelection.Length; ++j)
					{
						if(questSelection[j] == activeQuest)
						{
							questSelection[j] = null;
							break;
						}
					}

					newActiveQuest = false;
				}

				menuController.UpdateQuest(this, activeQuest, 1, true);

				mapMarkerButton.colors = questStationMarkerColors;
				mapMarkerName.color = questStationTextColor;
				mapMarkerDistance.color = questStationTextColor;

				mapMarkerName.text = stationName + " [!]";
			}
			else
			{
				if(updateQuestSelection)
				{
					questSelection = new QuestManager.Quest[] { questManager.GenerateQuest(this, firstTasks), questManager.GenerateQuest(this, secondaryTasks), null };
					QuestManager.TaskType[] thirdTasks = questSelection[0].task == questSelection[1].task ? new QuestManager.TaskType[allTasks.Length - 1] : new QuestManager.TaskType[allTasks.Length - 2];
					int i = 0;
					foreach(QuestManager.TaskType taskType in allTasks)
					{
						if(taskType != questSelection[0].taskType && taskType != questSelection[1].taskType)
						{
							thirdTasks[i] = taskType;
							++i;
						}
					}
					questSelection[2] = questManager.GenerateQuest(this, thirdTasks);
					updateQuestSelection = false;
				}

				for(int i = 0; i < 3; ++i)
				{
					menuController.UpdateQuest(this, questSelection[i], i, false);
				}

				newActiveQuest = true;

				mapMarkerButton.colors = originalMarkerColors;
				mapMarkerName.color = originalTextColor;
				mapMarkerDistance.color = originalTextColor;

				mapMarkerName.text = stationName;
			}
		}
	}

	public void UpdateTrading()
	{
		if(menuController.StationIsTrading(this))
		{
			tradingInventory.Clear();
			Dictionary<string, uint> playerInventory = localPlayerMainInventory.GetInventoryContents();
			Dictionary<string, uint> stationInventory = inventoryController.GetInventoryContents();
			foreach(string goodName in playerInventory.Keys)
			{
				uint stationAmount = stationInventory.ContainsKey(goodName) ? stationInventory[goodName] : 0;
				tradingInventory.Add(goodName, new GoodTradingInfo(playerInventory[goodName], stationAmount));
			}
			foreach(string goodName in stationInventory.Keys)
			{
				if(!tradingInventory.ContainsKey(goodName))
				{
					tradingInventory.Add(goodName, new GoodTradingInfo(0, stationInventory[goodName]));
				}
			}

			goodNames.Clear();
			goodNames.AddRange(tradingInventory.Keys);
			goodNames.Sort();
			RectTransform[] tradingEntries = new RectTransform[goodNames.Count];
			for(int i = 0; i < goodNames.Count; ++i)
			{
				RectTransform tradingEntryRectTransform = GameObject.Instantiate<RectTransform>(tradingEntryPrefab);
				GoodManager.Good good = goodManager.GetGood(goodNames[i]);

				tradingEntryRectTransform.GetChild(0).GetComponent<Text>().text = goodNames[i];
				tradingEntryRectTransform.GetChild(1).GetComponent<Text>().text = tradingInventory[goodNames[i]].playerAmount.ToString();
				tradingEntryRectTransform.GetChild(2).GetComponent<Text>().text = tradingInventory[goodNames[i]].stationAmount.ToString();
				tradingEntryRectTransform.GetChild(3).GetComponent<Text>().text = CalculateGoodPrice(goodNames[i], tradingInventory[goodNames[i]].stationAmount, -1) + "$";
				tradingEntryRectTransform.GetChild(4).GetComponent<Text>().text = CalculateGoodPrice(goodNames[i], tradingInventory[goodNames[i]].stationAmount, 1) + "$";
				tradingEntryRectTransform.GetChild(5).GetComponent<Text>().text = good.volume + "/" + localPlayerMainInventory.GetFreeCapacity(good) + " m3";
				tradingEntryRectTransform.GetChild(6).GetComponent<Text>().text = good.mass + " t";
				tradingEntryRectTransform.GetChild(7).GetComponent<Text>().text = good.description;

				if(dockedSpacecraft.Contains(localPlayerMainSpacecraft))
				{
					tradingEntryRectTransform.GetChild(8).gameObject.SetActive(true);
					tradingEntryRectTransform.GetChild(9).gameObject.SetActive(true);
					tradingEntryRectTransform.GetChild(10).gameObject.SetActive(true);

					string localGoodName = goodNames[i];
					InputField localAmountField = tradingEntryRectTransform.GetChild(8).GetComponent<InputField>();
					InventoryController localPlayerInventory = localPlayerMainInventory;
					InventoryController localStationInventory = inventoryController;
					localAmountField.onEndEdit.AddListener(delegate
					{
						UpdateTrading();
					});
					tradingEntryRectTransform.GetChild(9).GetComponent<Button>().onClick.AddListener(delegate
					{
						Trade(localGoodName, 0, localPlayerInventory, localStationInventory, tradingInventory[localGoodName].stationAmount, localAmountField);
					});
					tradingEntryRectTransform.GetChild(10).GetComponent<Button>().onClick.AddListener(delegate
					{
						Trade(localGoodName, 0, localStationInventory, localPlayerInventory, tradingInventory[localGoodName].stationAmount, localAmountField);
					});
				}
				else
				{
					tradingEntryRectTransform.GetChild(8).gameObject.SetActive(false);
					tradingEntryRectTransform.GetChild(9).gameObject.SetActive(false);
					tradingEntryRectTransform.GetChild(10).gameObject.SetActive(false);
				}

				tradingEntries[i] = tradingEntryRectTransform;
			}

			menuController.UpdateTrading(this, tradingEntries, inventoryController.GetMoney(), lastStationUpdate, stationUpdateInterval);
		}
	}

	public bool Trade(string goodName, uint tradeAmount, InventoryController buyer, InventoryController seller, uint stationAmount, InputField amountField = null, bool dumpGoods = false, bool hollowSale = false)
	{
		if(amountField != null)
		{
			if(amountField.text.StartsWith("-"))
			{
				amountField.text = amountField.text.Remove(0, 1);
			}
			tradeAmount = uint.Parse(amountField.text);
		}

		int money = buyer.GetMoney();
		uint availableAmount = seller.GetGoodAmount(goodName);
		int totalPrice = CalculateGoodPrice(goodName, stationAmount, (buyer == localPlayerMainInventory ? (int)-tradeAmount : (int)tradeAmount));
		if(money >= totalPrice)
		{
			if(hollowSale || availableAmount >= tradeAmount)
			{
				if(dumpGoods || buyer.Deposit(goodName, tradeAmount))
				{
					if(!hollowSale)
					{
						seller.Withdraw(goodName, tradeAmount);
					}

					buyer.TransferMoney(-totalPrice);
					seller.TransferMoney(totalPrice);

					if(buyer == localPlayerMainInventory)
					{
						questManager.NotifyTrade(this, goodName, (int)tradeAmount, -totalPrice);
					}
					else
					{
						questManager.NotifyTrade(this, goodName, (int)-tradeAmount, totalPrice);
					}

					UpdateTrading();

					if(buyer == localPlayerMainInventory || seller == localPlayerMainInventory)
					{
						audioController.PlayAudio(tradeAudio, null);
					}

					return true;
				}
				else
				{
					if(buyer == localPlayerMainInventory)
					{
						infoController.AddMessage("Not enough Storage Capacity on your Vessel, all Lavatories are full!", true);
					}
					else if(seller == localPlayerMainInventory)
					{
						infoController.AddMessage("Not enough Storage Capacity on their tiny Station!", true);
					}
				}
			}
			else
			{
				if(buyer == localPlayerMainInventory)
				{
					infoController.AddMessage("Don't get greedy, they don't have that much!", true);
				}
				else if(seller == localPlayerMainInventory)
				{
					infoController.AddMessage("They don't fall for your Trick of selling Stuff you don't possess!", true);
				}
			}
		}
		else
		{
			if(buyer == localPlayerMainInventory)
			{
				infoController.AddMessage("Not enough Cash and they refuse your Credit Cards!", true);
			}
			else if(seller == localPlayerMainInventory)
			{
				infoController.AddMessage("You successfully decapitalized this Station and they can not afford to buy more!", true);
			}
		}

		if(amountField != null && totalPrice <= money)
		{
			amountField.text = Mathf.Min((int)availableAmount, (int)buyer.GetFreeCapacity(goodManager.GetGood(goodName))).ToString();
		}

		return false;
	}

	public bool BuyConstructionMaterials(GoodManager.Load[] materials)
	{
		int totalPrice = 0;
		uint[] supplyAmounts = new uint[materials.Length];
		for(int i = 0; i < materials.Length; ++i)
		{
			supplyAmounts[i] = inventoryController.GetGoodAmount(materials[i].goodName);
			if(supplyAmounts[i] < materials[i].amount)
			{
				infoController.AddMessage("Not enough " + materials[i].goodName + " available at this Station!", false);
				return false;
			}

			totalPrice += CalculateGoodPrice(materials[i].goodName, supplyAmounts[i], -(int)materials[i].amount);
		}

		if(totalPrice > localPlayerMainInventory.GetMoney())
		{
			infoController.AddMessage("You are too poor to buy the Construction Materials!", false);
			return false;
		}

		for(int i = 0; i < materials.Length; ++i)
		{
			if(!Trade(materials[i].goodName, materials[i].amount, localPlayerMainInventory, inventoryController, supplyAmounts[i], null, true))
			{
				Debug.LogWarning("Construction Materials could not be bought, although they should be!");
				return false;
			}
		}

		return true;
	}

	// Works only for Solids
	public bool SellDeconstructionMaterials(GoodManager.Load[] materials)
	{
		if(materials.Length <= 0)
		{
			return true;
		}

		uint sum = 0;
		uint[] supplyAmounts = new uint[materials.Length];
		int totalPrice = 0;
		for(int i = 0; i < materials.Length; ++i)
		{
			if(goodManager.GetGood(materials[i].goodName).state == GoodManager.State.solid)
			{
				sum += materials[i].amount;
			}
			else
			{
				return false;
			}

			supplyAmounts[i] = inventoryController.GetGoodAmount(materials[i].goodName);
			totalPrice += CalculateGoodPrice(materials[i].goodName, supplyAmounts[i], (int)materials[i].amount);
		}

		if(sum > inventoryController.GetFreeCapacity(goodManager.GetGood(materials[0].goodName)))
		{
			infoController.AddMessage("Not enough Storage Capacity available in this Station!", false);
			return false;
		}

		if(totalPrice > inventoryController.GetMoney())
		{
			infoController.AddMessage("The Station is too poor to buy the Deconstruction Materials!", false);
			return false;
		}

		for(int i = 0; i < materials.Length; ++i)
		{
			if(!Trade(materials[i].goodName, materials[i].amount, inventoryController, localPlayerMainInventory, supplyAmounts[i], null, false, true))
			{
				Debug.LogWarning("Construction Materials could not be sold, although they should be!");
				return false;
			}
		}

		return true;
	}

	private IEnumerator<float> DockingTimeout(DockingPort port, SpacecraftController requester)
	{
		infoController.SetDockingExpiryTime(timeController.GetTime() + dockingTimeout);

		yield return dockingTimeout;

		if(port.IsActive() && port.IsFree())
		{
			port.HotkeyDown();
			expectedDockings.Remove(port);

			infoController.AddMessage("Docking Permission expired!", true);
		}
	}

	private IEnumerator<float> UpdateStation()
	{
		while(true)
		{
			lastStationUpdate = timeController.GetTime();

			updateQuestSelection = true;

			Dictionary<string, GoodManager.Good> goods = goodManager.GetGoodDictionary();
			foreach(string goodName in goods.Keys)
			{
				if(!spawnProtection)
				{
					inventoryController.Withdraw(goodName, (uint)Mathf.Min(goods[goodName].consumption, inventoryController.GetGoodAmount(goodName)));
				}
				else if(localPlayerMainSpacecraft.GetModuleCount() > 6)
				{
					spawnProtection = false;
				}

				if(inventoryController.GetGoodAmount(goodName) < goods[goodName].consumption * maxGoodStockFactor)
				{
					inventoryController.Deposit(goodName, (uint)UnityEngine.Random.Range(goods[goodName].consumption * 0.5f, goods[goodName].consumption * 2));
				}
			}
			inventoryController.TransferMoney(UnityEngine.Random.Range(minProfit, maxProfit));

			UpdateTrading();

			yield return stationUpdateInterval;
		}
	}

	public int CalculateGoodPrice(string goodName, uint supplyAmount, int transactionAmount = 1)
	{
		++supplyAmount;                                                                                         // Avoid supplyAmount == 0 without having supplyAmount == 0 and supplyAmount == 1 generate the same Price

		GoodManager.Good good = goodManager.GetGood(goodName);

		int price = 0;
		float sign = Mathf.Sign(transactionAmount);
		for(int i = 0; i < Mathf.Abs(transactionAmount); ++i)
		{
			// Price Formula is ((consumption * targetGoodStockFactor) / supplyAmount)^2 * basePrice
			float newSupply = supplyAmount + ((i + 1) * sign);                                                  // Supply after this Unit is bought/sold
			if(newSupply > 0.0f)
			{
				float supplyConstant = (good.consumption * targetGoodStockFactor) / newSupply;
				price += Mathf.CeilToInt((supplyConstant * supplyConstant) * good.price);
			}
			else
			{
				return price;
			}
		}

		return price;
	}

	public Transform GetTransform()
	{
		return transform;
	}

	public InventoryController GetInventoryController()
	{
		return inventoryController;
	}

	public string GetStationName()
	{
		return stationName;
	}

	public void SetStationName(string stationName)
	{
		menuController = MenuController.GetInstance();
		uiTransform = menuController.GetUITransform();
		mapMarker = GameObject.Instantiate<RectTransform>(mapMarkerPrefab, menuController.GetMapMarkerParent());
		mapMarkerName = mapMarker.GetChild(0).GetComponent<Text>();
		mapMarkerDistance = mapMarker.GetChild(1).GetComponent<Text>();
		mapMarkerButton = mapMarker.GetComponent<Button>();
		mapMarkerButton.onClick.AddListener(delegate
		{
			ToggleStationMenu();
		});
		originalMarkerColors = mapMarkerButton.colors;
		originalTextColor = mapMarkerName.color;

		mapMarkerName.text = stationName;
		this.stationName = stationName;
	}
}
