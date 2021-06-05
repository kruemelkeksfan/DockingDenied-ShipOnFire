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
		public int price;

		public GoodTradingInfo(uint playerAmount, uint stationAmount, int price)
		{
			this.playerAmount = playerAmount;
			this.stationAmount = stationAmount;
			this.price = price;
		}
	}

	private static WaitForSeconds waitForStationUpdateInterval = null;
	private static WaitForSeconds waitForDockingTimeout = null;

	[SerializeField] private RectTransform mapMarkerPrefab = null;
	[Tooltip("Distance up from which no more digital Digits will be displayed")]
	[SerializeField] private float decimalDigitThreshold = 100.0f;
	[Tooltip("Maximum Distance from which a Docking Permission can be granted")]
	[SerializeField] private float maxApproachDistance = 1.0f;
	[Tooltip("Additional Discount Percentage per Item in Favour of the Player for Buying and Selling to encourage Bulk Trading.")]
	[SerializeField] private float bulkDiscount = 0.002f;
	[Tooltip("Maximum Time in Seconds before Docking Permission expires")]
	[SerializeField] private float dockingTimeout = 600.0f;
	[SerializeField] private float stationUpdateInterval = 600.0f;
	[Tooltip("Determines the maximum Amount of this Good this Station will stockpile by itself, depending on the Consumption.")]
	[SerializeField] private float maxGoodStockFactor = 12.0f;
	[Tooltip("Minimum Money Change of the Station per Economy Update.")]
	[SerializeField] private int minProfit = -100;
	[Tooltip("Maximum Money Change of the Station per Economy Update.")]
	[SerializeField] private int maxProfit = 200;
	[SerializeField] private RectTransform questEntryPrefab = null;
	[SerializeField] private RectTransform tradingEntryPrefab = null;
	private GoodManager goodManager = null;
	private QuestManager questManager = null;
	private MenuController menuController = null;
	private Spacecraft spacecraft = null;
	private new Transform transform = null;
	private new Rigidbody2D rigidbody = null;
	private InventoryController inventoryController = null;
	private RectTransform uiTransform = null;
	private Spacecraft localPlayerMainSpacecraft = null;
	private Transform localPlayerMainTransform = null;
	private InventoryController localPlayerMainInventory = null;
	private PlayerSpacecraftUIController playerSpacecraftController = null;
	private new Camera camera = null;
	private Transform cameraTransform = null;
	private RectTransform mapMarker = null;
	private Text mapMarkerName = null;
	private Text mapMarkerDistance = null;
	private string stationName = null;
	private DockingPort[] dockingPorts = null;
	private Dictionary<DockingPort, Spacecraft> expectedDockings = null;
	private HashSet<Spacecraft> dockedSpacecraft = null;
	private QuestManager.TaskType[] firstTasks = null;
	private QuestManager.TaskType[] secondaryTasks = null;
	private QuestManager.Quest[] questSelection = null;
	private bool updateQuestSelection = true;
	private float lastStationUpdate = 0.0f;
	private Dictionary<string, GoodTradingInfo> tradingInventory;
	private List<string> goodNames = null;

	private void Start()
	{
		if(waitForStationUpdateInterval == null || waitForDockingTimeout == null)
		{
			waitForStationUpdateInterval = new WaitForSeconds(stationUpdateInterval);
			waitForDockingTimeout = new WaitForSeconds(dockingTimeout);
		}

		maxApproachDistance *= maxApproachDistance;
		spacecraft = GetComponent<Spacecraft>();
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
		expectedDockings = new Dictionary<DockingPort, Spacecraft>();
		dockedSpacecraft = new HashSet<Spacecraft>();

		tradingInventory = new Dictionary<string, GoodTradingInfo>();
		goodNames = new List<string>();

		goodManager = GoodManager.GetInstance();
		questManager = QuestManager.GetInstance();
		SpacecraftManager spacecraftManager = SpacecraftManager.GetInstance();
		localPlayerMainSpacecraft = spacecraftManager.GetLocalPlayerMainSpacecraft();
		localPlayerMainTransform = localPlayerMainSpacecraft.GetTransform();
		localPlayerMainInventory = localPlayerMainSpacecraft.GetComponent<InventoryController>();
		playerSpacecraftController = localPlayerMainSpacecraft.GetComponent<PlayerSpacecraftUIController>();
		spacecraftManager.AddSpacecraftChangeListener(this);

		firstTasks = new QuestManager.TaskType[] { QuestManager.TaskType.Bribe, QuestManager.TaskType.JumpStart, QuestManager.TaskType.Tow };
		secondaryTasks = new QuestManager.TaskType[] { QuestManager.TaskType.Trade };

		Dictionary<string, GoodManager.Good> goods = goodManager.GetGoodDictionary();
		foreach(string goodName in goods.Keys)
		{
			inventoryController.Deposit(goodName, (uint)Mathf.CeilToInt(goods[goodName].consumption * maxGoodStockFactor));
		}

		spacecraft.AddUpdateListener(this);

		StartCoroutine(UpdateStation());
	}

	private void OnDestroy()
	{
		if(mapMarker != null)
		{
			ToggleController.GetInstance()?.RemoveToggleObject("SpacecraftMarkers", mapMarker.gameObject);
			GameObject.Destroy(mapMarker.gameObject);
		}

		spacecraft?.RemoveUpdateListener(this);
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
				mapMarkerDistance.text = distance.ToString("F0") + "km";
			}
			else
			{
				mapMarkerDistance.text = distance.ToString("F2") + "km";
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
			Spacecraft otherSpacecraft = otherPort.GetComponentInParent<Spacecraft>();
			if(expectedDockings[port] == otherSpacecraft)
			{
				expectedDockings.Remove(port);
				dockedSpacecraft.Add(otherSpacecraft);
			}
			else
			{
				if(otherSpacecraft == SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft())
				{
					InfoController.GetInstance().AddMessage("You have no Docking Permission for this Docking Port!");
				}
				port.HotkeyDown();
			}
		}
	}

	public void Undocked(DockingPort port, DockingPort otherPort)
	{
		dockedSpacecraft.Remove(otherPort.GetComponentInParent<Spacecraft>());

		if(port.IsActive())
		{
			port.HotkeyDown();
		}
		if(otherPort.GetComponentInParent<Spacecraft>() == SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft())
		{
			menuController.CloseStationMenu(this);
			InfoController.GetInstance().AddMessage("Undocking successful, good Flight!");
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
		playerSpacecraftController.SetTarget(rigidbody);
		menuController.ToggleStationMenu(this, stationName);
	}

	public void RequestDocking(Spacecraft requester, bool aiRequest = false)
	{
		// TODO: Check if Ship is on Fire etc.
		if(!dockedSpacecraft.Contains(requester))
		{
			if(!expectedDockings.ContainsValue(requester))
			{
				if(aiRequest || (requester.GetTransform().position - transform.position).sqrMagnitude <= maxApproachDistance)
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
						if(!aiRequest)
						{
							StartCoroutine(DockingTimeout(alignedPort, requester));
						}

						if(requester == SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft())
						{
							InfoController.GetInstance().AddMessage("Docking Permission granted for Docking Port " + alignedPort.GetActionName() + "!");
						}
					}
					else if(requester == SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft())
					{
						InfoController.GetInstance().AddMessage("No free Docking Ports available!");
					}
				}
				else if(requester == SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft())
				{
					InfoController.GetInstance().AddMessage("You are too far away to request Docking Permission!");
				}
			}
			else
			{
				InfoController.GetInstance().AddMessage("You already have an active Docking Permission for this Station!");
			}
		}
		else
		{
			InfoController.GetInstance().AddMessage("You are already docked at this Station!");
		}
	}

	public void UpdateQuests()
	{
		if(menuController.StationIsQuesting(this))
		{
			QuestManager.Quest activeQuest = questManager.GetActiveQuest(this);
			if(activeQuest != null)
			{
				questSelection = null;
				menuController.UpdateQuest(this, activeQuest, 1, true, dockedSpacecraft.Contains(localPlayerMainSpacecraft));
			}
			else
			{
				if(questSelection == null || updateQuestSelection)
				{
					questSelection = new QuestManager.Quest[] { questManager.GenerateQuest(this, firstTasks), questManager.GenerateQuest(this, secondaryTasks), questManager.GenerateQuest(this) };
					updateQuestSelection = false;
				}

				for(int i = 0; i < 3; ++i)
				{
					menuController.UpdateQuest(this, questSelection[i], i, false);
				}
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
				tradingInventory.Add(goodName, new GoodTradingInfo(playerInventory[goodName], stationAmount, CalculateGoodPrice(goodName, stationAmount)));
			}
			foreach(string goodName in stationInventory.Keys)
			{
				if(!tradingInventory.ContainsKey(goodName))
				{
					tradingInventory.Add(goodName, new GoodTradingInfo(0, stationInventory[goodName], CalculateGoodPrice(goodName, stationInventory[goodName])));
				}
			}

			goodNames.Clear();
			goodNames.AddRange(tradingInventory.Keys);
			goodNames.Sort();
			RectTransform[] tradingEntries = new RectTransform[goodNames.Count];
			for(int i = 0; i < goodNames.Count; ++i)
			{
				RectTransform tradingEntryRectTransform = GameObject.Instantiate<RectTransform>(tradingEntryPrefab);

				tradingEntryRectTransform.GetChild(0).GetComponent<Text>().text = goodNames[i];
				tradingEntryRectTransform.GetChild(1).GetComponent<Text>().text = tradingInventory[goodNames[i]].playerAmount.ToString();
				tradingEntryRectTransform.GetChild(2).GetComponent<Text>().text = tradingInventory[goodNames[i]].stationAmount.ToString();
				tradingEntryRectTransform.GetChild(3).GetComponent<Text>().text = tradingInventory[goodNames[i]].price.ToString();
				tradingEntryRectTransform.GetChild(4).GetComponent<Text>().text = goodManager.GetGood(goodNames[i]).decription;

				if(dockedSpacecraft.Contains(localPlayerMainSpacecraft))
				{
					tradingEntryRectTransform.GetChild(5).gameObject.SetActive(true);
					tradingEntryRectTransform.GetChild(6).gameObject.SetActive(true);
					tradingEntryRectTransform.GetChild(7).gameObject.SetActive(true);

					string localGoodName = goodNames[i];
					InputField localAmountField = tradingEntryRectTransform.GetChild(5).GetComponent<InputField>();
					InventoryController localPlayerInventory = localPlayerMainInventory;
					InventoryController localStationInventory = inventoryController;
					tradingEntryRectTransform.GetChild(6).GetComponent<Button>().onClick.AddListener(delegate
					{
						Trade(localGoodName, 0, localPlayerInventory, localStationInventory, tradingInventory[goodNames[i]].stationAmount, localAmountField);
					});
					tradingEntryRectTransform.GetChild(7).GetComponent<Button>().onClick.AddListener(delegate
					{
						Trade(localGoodName, 0, localStationInventory, localPlayerInventory, tradingInventory[goodNames[i]].stationAmount, localAmountField);
					});
				}
				else
				{
					tradingEntryRectTransform.GetChild(5).gameObject.SetActive(false);
					tradingEntryRectTransform.GetChild(6).gameObject.SetActive(false);
					tradingEntryRectTransform.GetChild(7).gameObject.SetActive(false);
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
		int totalPrice = CalculateGoodPrice(goodName, stationAmount, (buyer == localPlayerMainInventory ? (int)tradeAmount : (int)-tradeAmount));
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

					return true;
				}
				else
				{
					if(buyer == localPlayerMainInventory)
					{
						InfoController.GetInstance().AddMessage("Not enough Storage Capacity on your Vessel, all Lavatories are full!");
					}
					else
					{
						InfoController.GetInstance().AddMessage("Not enough Storage Capacity on their tiny Station!");
					}
				}
			}
			else
			{
				if(buyer == localPlayerMainInventory)
				{
					InfoController.GetInstance().AddMessage("Don't get greedy, they don't have that much!");
				}
				else
				{
					InfoController.GetInstance().AddMessage("They don't fall for your Trick of selling Stuff you don't possess!");
				}
			}
		}
		else
		{
			if(buyer == localPlayerMainInventory)
			{
				InfoController.GetInstance().AddMessage("Not enough Cash and they refuse your Credit Cards!");
			}
			else
			{
				InfoController.GetInstance().AddMessage("You successfully decapitalized this Station and they can not afford to buy more!");
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
				InfoController.GetInstance().AddMessage("Not enough " + materials[i].goodName + " available at this Station!");
				return false;
			}

			totalPrice += CalculateGoodPrice(materials[i].goodName, supplyAmounts[i], (int)materials[i].amount);
			++i;
		}

		if(totalPrice > localPlayerMainInventory.GetMoney())
		{
			InfoController.GetInstance().AddMessage("You are too poor to buy the Construction Materials!");
			return false;
		}

		for(int i = 0; i < materials.Length; ++i)
		{
			if(!Trade(materials[i].goodName, materials[i].amount, localPlayerMainInventory, inventoryController, supplyAmounts[i], null, true))
			{
				Debug.LogWarning("Construction Materials could not be bought, although they should be!");
				return false;
			}
			++i;
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
			++i;
		}

		if(sum > inventoryController.GetFreeCapacity(goodManager.GetGood(materials[0].goodName)))
		{
			InfoController.GetInstance().AddMessage("Not enough Storage Capacity available in this Station!");
			return false;
		}

		if(totalPrice > inventoryController.GetMoney())
		{
			InfoController.GetInstance().AddMessage("The Station is too poor to buy the Deconstruction Materials!");
			return false;
		}

		for(int i = 0; i < materials.Length; ++i)
		{
			if(!Trade(materials[i].goodName, materials[i].amount, inventoryController, localPlayerMainInventory, supplyAmounts[i], null, false, true))
			{
				Debug.LogWarning("Construction Materials could not be sold, although they should be!");
				return false;
			}
			++i;
		}

		return true;
	}

	private IEnumerator DockingTimeout(DockingPort port, Spacecraft requester)
	{
		yield return waitForDockingTimeout;

		if(port.IsActive() && port.IsFree())
		{
			port.HotkeyDown();
			expectedDockings.Remove(port);

			if(requester == SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft())
			{
				InfoController.GetInstance().AddMessage("Docking Permission expired!");
			}
		}
	}

	private IEnumerator UpdateStation()
	{
		while(true)
		{
			lastStationUpdate = Time.realtimeSinceStartup;

			updateQuestSelection = true;

			Dictionary<string, GoodManager.Good> goods = goodManager.GetGoodDictionary();
			foreach(string goodName in goods.Keys)
			{
				inventoryController.Withdraw(goodName, (uint)Mathf.Min(goods[goodName].consumption, inventoryController.GetGoodAmount(goodName)));

				if(inventoryController.GetGoodAmount(goodName) < goods[goodName].consumption * maxGoodStockFactor)
				{
					inventoryController.Deposit(goodName, (uint)UnityEngine.Random.Range(1, goods[goodName].consumption * 2));
				}
			}
			inventoryController.TransferMoney(UnityEngine.Random.Range(minProfit, maxProfit));

			UpdateQuests();
			UpdateTrading();

			yield return waitForStationUpdateInterval;
		}
	}

	private int CalculateGoodPrice(string goodName, uint supplyAmount, int transactionAmount = 1)
	{
		++supplyAmount;
		transactionAmount = Mathf.Max(transactionAmount, 1);

		GoodManager.Good good = goodManager.GetGood(goodName);

		// Price Formula is (consumption / supplyAmount)^2 * basePrice
		// To calculate Bulk Price without looping through the Bulk use Integral
		// Integral of Price Formula is (-(consumption^2) * basePrice) / amount
		// To get the precise Number calculate Integral of upper Bound (supplyAmount + transactionAmount when Station is buying) minus Integral of lower Bound (supplyAmount when Station is buying)
		float consumptionPriceConstant = -(good.consumption * good.consumption) * good.price;
		if(transactionAmount >= 0)
		{
			float price = (consumptionPriceConstant / (supplyAmount + transactionAmount)) - (consumptionPriceConstant / (supplyAmount));
			return Mathf.RoundToInt(price + (bulkDiscount * transactionAmount * price)) + 1;                                                        // Round instead of Ceil to avoid different Values for amount = 1 and amount = many, + 1 to avoid price = 0
		}
		else
		{
			float price = (consumptionPriceConstant / (supplyAmount)) - (consumptionPriceConstant / (supplyAmount + transactionAmount));
			return Mathf.RoundToInt(price - (bulkDiscount * transactionAmount * price)) + 1;                                                        // Round instead of Ceil to avoid different Values for amount = 1 and amount = many, + 1 to avoid price = 0
		}
	}

	public Transform GetTransform()
	{
		return transform;
	}

	public InventoryController GetInventoryController()
	{
		return inventoryController;
	}

	public void SetStationName(string stationName)
	{
		menuController = MenuController.GetInstance();
		uiTransform = menuController.GetUITransform();
		mapMarker = GameObject.Instantiate<RectTransform>(mapMarkerPrefab, uiTransform);
		mapMarkerName = mapMarker.GetChild(0).GetComponent<Text>();
		mapMarkerDistance = mapMarker.GetChild(1).GetComponent<Text>();
		mapMarker.GetComponent<Button>().onClick.AddListener(delegate
		{
			ToggleStationMenu();
		});
		ToggleController.GetInstance().AddToggleObject("SpacecraftMarkers", mapMarker.gameObject);

		mapMarkerName.text = stationName;
		this.stationName = stationName;
	}
}
