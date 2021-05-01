﻿using System.Collections;
using System.Collections.Generic;
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

	private static WaitForSeconds waitForEconomyUpdateInterval = null;
	private static WaitForSeconds waitForDockingTimeout = null;

	[SerializeField] private RectTransform uiTransform = null;
	[SerializeField] private RectTransform mapMarker = null;
	[SerializeField] private Text mapMarkerName = null;
	[SerializeField] private Text mapMarkerDistance = null;
	[Tooltip("Distance up from which no more digital Digits will be displayed")]
	[SerializeField] private float decimalDigitThreshold = 100.0f;
	[Tooltip("Maximum Distance from which a Docking Permission can be granted")]
	[SerializeField] private float maxApproachDistance = 1.0f;
	[Tooltip("Additional Discount Percentage per Item in Favour of the Player for Buying and Selling to encourage Bulk Trading.")]
	[SerializeField] private float bulkDiscount = 0.002f;
	[Tooltip("Maximum Time in Seconds before Docking Permission expires")]
	[SerializeField] private float dockingTimeout = 600.0f;
	[SerializeField] private float economyUpdateInterval = 600.0f;
	[SerializeField] private int maxGoodStock = 100;
	[Tooltip("Minimum Money Change of the Station per Economy Update.")]
	[SerializeField] private int minProfit = -100;
	[Tooltip("Maximum Money Change of the Station per Economy Update.")]
	[SerializeField] private int maxProfit = 200;
	[SerializeField] private GameObject stationMenu = null;
	[SerializeField] private Text stationName = null;
	[SerializeField] private GameObject tradingMenu = null;
	[SerializeField] private GameObject tradingEntryPrefab = null;
	[SerializeField] private RectTransform tradingContentPane = null;
	[SerializeField] private GameObject emptyListIndicator = null;
	private GoodManager goodManager = null;
	private Spacecraft spacecraft = null;
	private new Transform transform = null;
	private InventoryController inventoryController = null;
	private Spacecraft localPlayerSpacecraft = null;
	private Transform localPlayerSpacecraftTransform = null;
	private InventoryController localPlayerMainInventory = null;
	private new Camera camera = null;
	private DockingPort[] dockingPorts = null;
	private Dictionary<DockingPort, Spacecraft> expectedDockings = null;
	private HashSet<Spacecraft> dockedSpacecraft = null;

	private void Start()
	{
		if(waitForEconomyUpdateInterval == null || waitForDockingTimeout == null)
		{
			waitForEconomyUpdateInterval = new WaitForSeconds(economyUpdateInterval);
			waitForDockingTimeout = new WaitForSeconds(dockingTimeout);
		}

		ToggleController.GetInstance().AddToggleObject("StationMarkers", mapMarker.gameObject);

		maxApproachDistance *= maxApproachDistance;
		spacecraft = GetComponent<Spacecraft>();
		transform = spacecraft.GetTransform();
		inventoryController = GetComponent<InventoryController>();
		camera = Camera.main;
		dockingPorts = GetComponentsInChildren<DockingPort>();
		foreach(DockingPort port in dockingPorts)
		{
			port.AddDockingListener(this);
		}
		expectedDockings = new Dictionary<DockingPort, Spacecraft>();
		dockedSpacecraft = new HashSet<Spacecraft>();

		goodManager = GoodManager.GetInstance();
		SpacecraftManager spacecraftManager = SpacecraftManager.GetInstance();
		localPlayerSpacecraft = spacecraftManager.GetLocalPlayerMainSpacecraft();
		localPlayerSpacecraftTransform = localPlayerSpacecraft.GetTransform();
		localPlayerMainInventory = localPlayerSpacecraft.GetComponent<InventoryController>();
		spacecraftManager.AddSpacecraftChangeListener(this);

		spacecraft.AddUpdateListener(this);

		StartCoroutine(CalculateEconomy());
	}

	private void OnDestroy()
	{
		ToggleController.GetInstance().RemoveToggleObject("StationMarkers", mapMarker.gameObject);
	}

	public void UpdateNotify()
	{
		Vector2 screenPoint;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(uiTransform, camera.WorldToScreenPoint(transform.position), null, out screenPoint);
		mapMarker.anchoredPosition = screenPoint;

		float distance = (transform.position - localPlayerSpacecraftTransform.position).magnitude;
		if(distance > decimalDigitThreshold)
		{
			mapMarkerDistance.text = distance.ToString("F0") + "km";
		}
		else
		{
			mapMarkerDistance.text = distance.ToString("F2") + "km";
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
			stationMenu.SetActive(false);
			InfoController.GetInstance().AddMessage("Undocking successful, good Flight!");
		}
	}

	public void Notify()
	{
		localPlayerSpacecraft = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft();
		localPlayerSpacecraftTransform = localPlayerSpacecraft.GetTransform();
		localPlayerMainInventory = localPlayerSpacecraft.GetComponent<InventoryController>();
	}

	public void ToggleStationMenu()                         // ToggleController would need to know the Name of the Station and therefore a Method here would be necessary anyways
	{
		stationMenu.SetActive(!stationMenu.activeSelf);
		InputController.SetFlightControls(!stationMenu.activeSelf);
	}

	public void ToggleTradingMenu()
	{
		tradingMenu.SetActive(!tradingMenu.activeSelf);
	}

	public void RequestDocking()
	{
		// TODO: Check if Ship is on Fire etc.
		Spacecraft requester = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft();
		if(!dockedSpacecraft.Contains(requester))
		{
			if(!expectedDockings.ContainsValue(requester))
			{
				if((requester.GetTransform().position - transform.position).sqrMagnitude <= maxApproachDistance)
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
						StartCoroutine(DockingTimeout(alignedPort, requester));

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

	// TODO: Maybe outsource this to TradingScreenController and StationEconomyController Classes
	public void UpdateTrading()
	{
		Dictionary<string, string> amountSettings = new Dictionary<string, string>(tradingContentPane.childCount - 1);
		for(int i = 2; i < tradingContentPane.childCount; ++i)
		{
			Transform child = tradingContentPane.GetChild(i);
			amountSettings[child.GetChild(0).GetComponent<Text>().text] = child.GetChild(4).GetComponent<InputField>().text;
			GameObject.Destroy(child.gameObject);
		}

		Dictionary<string, GoodTradingInfo> tradingInventory = new Dictionary<string, GoodTradingInfo>();
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

		int j = 0;
		List<string> tradingInventoryKeys = new List<string>(tradingInventory.Keys);
		tradingInventoryKeys.Sort();
		foreach(string goodName in tradingInventoryKeys)
		{
			GameObject tradingEntry = GameObject.Instantiate<GameObject>(tradingEntryPrefab, tradingContentPane);
			RectTransform tradingEntryRectTransform = tradingEntry.GetComponent<RectTransform>();
			tradingEntryRectTransform.anchoredPosition =
				new Vector3(tradingEntryRectTransform.anchoredPosition.x, -(tradingEntryRectTransform.rect.height * 0.5f + 20.0f + tradingEntryRectTransform.rect.height * j));

			tradingEntryRectTransform.GetChild(0).GetComponent<Text>().text = goodName;
			tradingEntryRectTransform.GetChild(1).GetComponent<Text>().text = tradingInventory[goodName].playerAmount.ToString();
			tradingEntryRectTransform.GetChild(2).GetComponent<Text>().text = tradingInventory[goodName].stationAmount.ToString();
			tradingEntryRectTransform.GetChild(3).GetComponent<Text>().text = tradingInventory[goodName].price.ToString();

			if(dockedSpacecraft.Contains(localPlayerSpacecraft))
			{
				tradingEntryRectTransform.GetChild(4).gameObject.SetActive(true);
				tradingEntryRectTransform.GetChild(5).gameObject.SetActive(true);
				tradingEntryRectTransform.GetChild(6).gameObject.SetActive(true);

				string localGoodName = goodName;
				InputField localAmountField = tradingEntryRectTransform.GetChild(4).GetComponent<InputField>();
				if(amountSettings.ContainsKey(goodName))
				{
					localAmountField.text = amountSettings[goodName];
				}
				InventoryController localPlayerInventory = localPlayerMainInventory;
				InventoryController localStationInventory = inventoryController;
				tradingEntryRectTransform.GetChild(5).GetComponent<Button>().onClick.AddListener(delegate
				{
					Trade(localGoodName, localAmountField, localPlayerInventory, localStationInventory, tradingInventory[goodName].price, tradingInventory[goodName].stationAmount);
				});
				tradingEntryRectTransform.GetChild(6).GetComponent<Button>().onClick.AddListener(delegate
				{
					Trade(localGoodName, localAmountField, localStationInventory, localPlayerInventory, tradingInventory[goodName].price, tradingInventory[goodName].stationAmount);
				});
			}
			else
			{
				tradingEntryRectTransform.GetChild(4).gameObject.SetActive(false);
				tradingEntryRectTransform.GetChild(5).gameObject.SetActive(false);
				tradingEntryRectTransform.GetChild(6).gameObject.SetActive(false);
			}

			++j;
		}

		if(tradingContentPane.childCount <= 2)
		{
			emptyListIndicator.SetActive(true);
		}
		else
		{
			emptyListIndicator.SetActive(false);
		}
	}

	public void Trade(string goodName, InputField amountField, InventoryController buyer, InventoryController seller, int price, uint stationAmount)
	{
		if(amountField.text.StartsWith("-"))
		{
			amountField.text = amountField.text.Remove(0, 1);
		}
		uint tradeAmount = uint.Parse(amountField.text);
		int money = buyer.GetMoney();
		uint availableAmount = seller.GetGoodAmount(goodName);
		int totalPrice = CalculateGoodPrice(goodName, stationAmount, (buyer == localPlayerMainInventory ? (int) tradeAmount : (int) -tradeAmount));
		if(money >= totalPrice)
		{
			if(availableAmount >= tradeAmount)
			{
				if(buyer.Deposit(goodName, tradeAmount))
				{
					seller.Withdraw(goodName, tradeAmount);

					buyer.TransferMoney(-totalPrice);
					seller.TransferMoney(totalPrice);

					UpdateTrading();

					return;
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

		amountField.text = Mathf.Min(new int[] { (int)availableAmount, (int)(money / (price * tradeAmount)), (int)buyer.GetFreeCapacity(goodManager.GetGood(goodName).state) }).ToString();
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

	private IEnumerator CalculateEconomy()
	{
		while(true)
		{
			Dictionary<string, GoodManager.Good> goods = goodManager.GetGoodDictionary();
			foreach(string goodName in goods.Keys)
			{
				uint stock = inventoryController.GetGoodAmount(goodName);
				if(stock < maxGoodStock)
				{
					inventoryController.Deposit(goodName, (uint) Random.Range(1, goods[goodName].consumption * 2));
				}
				inventoryController.Withdraw(goodName, (uint) Mathf.Max(goods[goodName].consumption, (int) stock));
			}

			inventoryController.TransferMoney(Random.Range(minProfit, maxProfit));

			UpdateTrading();
			yield return waitForEconomyUpdateInterval;
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
			return Mathf.CeilToInt(price + (bulkDiscount * transactionAmount * price));
		}
		else
		{
			float price = (consumptionPriceConstant / (supplyAmount)) - (consumptionPriceConstant / (supplyAmount + transactionAmount));
			return Mathf.CeilToInt(price - (bulkDiscount * transactionAmount * price));
		}
	}

	public void SetStationName(string stationName)
	{
		mapMarkerName.text = stationName;
		this.stationName.text = stationName;
	}
}
