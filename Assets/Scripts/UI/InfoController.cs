using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class InfoController : MonoBehaviour, IListener
{
	private struct Message
	{
		public string message;
		public float timestamp;
	}

	public static InfoController instance = null;
	private static bool helpActive = true;

	[SerializeField] private Text messageField = null;
	[SerializeField] private float skippingMessageDuration = 0.2f;
	[SerializeField] private float messageDuration = 6.0f;
	[SerializeField] private Text controlHint = null;
	[SerializeField] private Text resourceDisplay = null;
	[SerializeField] private Text secondaryDisplay = null;
	[SerializeField] private Text throttleDisplay = null;
	[SerializeField] private Text autoThrottleDisplay = null;
	[SerializeField] private GameObject keyBindingDisplay = null;
	private Queue<Message> messages = null;
	private float lastDequeue = 0.0f;
	private Dictionary<string, uint> buildingCosts = null;
	private InventoryController inventoryController = null;
	private PlayerSpacecraftUIController playerSpacecraftUIController = null;
	private StringBuilder textBuilder = null;
	private bool updateResourceDisplay = true;
	private bool updateBuildingResourceDisplay = true;
	private bool showBuildingResourceDisplay = false;

	public static InfoController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");                                               // Decimal Points FTW!!elf!

		messages = new Queue<Message>();
		textBuilder = new StringBuilder();

		instance = this;
	}

	private void Start()
	{
		SpacecraftManager.GetInstance().AddSpacecraftChangeListener(this);
		Notify();
	}

	// TODO: Put this into a Method which only gets called when a new Message is added or a Message Timestamp runs out (Coroutine)
	private void Update()
	{
		if(updateResourceDisplay)
		{
			textBuilder.Clear();
			textBuilder.Append(inventoryController.GetMoney());
			textBuilder.Append("$ / Energy - ");
			textBuilder.Append(inventoryController.GetEnergyKWH());
			textBuilder.Append("kWh");
			resourceDisplay.text = textBuilder.ToString();
			/* + " / Hydrogen - " + inventoryController.GetGoodAmount("Hydrogen") + " / Oxygen - " + inventoryController.GetGoodAmount("Oxygen")
		+ " / Food - " + inventoryController.GetGoodAmount("Food") + " / Water - " + inventoryController.GetGoodAmount("Water")*/

			updateResourceDisplay = false;
		}

		if(showBuildingResourceDisplay && updateBuildingResourceDisplay)
		{
			textBuilder.Clear();
			textBuilder.Append("Steel - ");
			textBuilder.Append(inventoryController.GetGoodAmount("Steel"));
			if(buildingCosts != null)
			{
				textBuilder.Append(" (");
				textBuilder.Append(buildingCosts.ContainsKey("Steel") ? buildingCosts["Steel"] : 0);
				textBuilder.Append(")");
			}
			textBuilder.Append(" / Aluminium - ");
			textBuilder.Append(inventoryController.GetGoodAmount("Aluminium"));
			if(buildingCosts != null)
			{
				textBuilder.Append(" (");
				textBuilder.Append(buildingCosts.ContainsKey("Aluminium") ? buildingCosts["Aluminium"] : 0);
				textBuilder.Append(")");
			}
			textBuilder.Append(" / Copper - ");
			textBuilder.Append(inventoryController.GetGoodAmount("Copper"));
			if(buildingCosts != null)
			{
				textBuilder.Append(" (");
				textBuilder.Append(buildingCosts.ContainsKey("Copper") ? buildingCosts["Copper"] : 0);
				textBuilder.Append(")");
			}
			textBuilder.Append(" / Gold - ");
			textBuilder.Append(inventoryController.GetGoodAmount("Gold"));
			if(buildingCosts != null)
			{
				textBuilder.Append(" (");
				textBuilder.Append(buildingCosts.ContainsKey("Gold") ? buildingCosts["Gold"] : 0);
				textBuilder.Append(")");
			}
			textBuilder.Append(" / Silicon - ");
			textBuilder.Append(inventoryController.GetGoodAmount("Silicon"));
			if(buildingCosts != null)
			{
				textBuilder.Append(" (");
				textBuilder.Append(buildingCosts.ContainsKey("Silicon") ? buildingCosts["Silicon"] : 0);
				textBuilder.Append(")");
			}
			secondaryDisplay.text = textBuilder.ToString();

			updateBuildingResourceDisplay = false;
		}
		else if(!showBuildingResourceDisplay)
		{
			Vector3 flightData = playerSpacecraftUIController.GetFlightData();
			textBuilder.Clear();
			textBuilder.Append("Altitude - ");
			textBuilder.Append((int) flightData.x);
			textBuilder.Append("km / Station Speed - ");
			textBuilder.Append(flightData.y.ToString("F4"));
			textBuilder.Append("km/s / Orbital Speed - ");
			textBuilder.Append(flightData.z.ToString("F4"));
			textBuilder.Append("km/s");
			secondaryDisplay.text = textBuilder.ToString();
		}

		if(Input.GetButtonDown("ShowHelp"))
		{
			helpActive = !helpActive;
		}
		if(keyBindingDisplay.activeSelf != helpActive)
		{
			keyBindingDisplay.SetActive(helpActive);
		}

		float messageDuration = Input.GetButton("Skip Info Log") ? skippingMessageDuration : this.messageDuration;

		while(messages.Count > 0 && messages.Peek().timestamp + messageDuration < Time.realtimeSinceStartup && lastDequeue + messageDuration < Time.realtimeSinceStartup)
		{
			messages.Dequeue();
			lastDequeue = Time.realtimeSinceStartup;
		}

		textBuilder.Clear();
		foreach(Message message in messages)
		{
			textBuilder.AppendLine(message.message);
		}
		messageField.text = textBuilder.ToString();
	}

	public void Notify()
	{
		inventoryController = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft().GetComponent<InventoryController>();
		playerSpacecraftUIController = inventoryController.GetComponent<PlayerSpacecraftUIController>();
	}

	public void UpdateControlHint(Dictionary<string, string[]> keyBindings)
	{
		StringBuilder hint = new StringBuilder(256);
		foreach(string key in keyBindings.Keys)
		{
			if(keyBindings[key].Length > 0)
			{
				hint.Append(key + " - \t");
			}

			bool first = true;
			foreach(string action in keyBindings[key])
			{
				if(!first)
				{
					hint.Append("\t\t");
				}
				hint.Append(action + "\n");
				first = false;
			}
		}

		controlHint.text = hint.ToString();
	}

	public void UpdateResourceDisplay()
	{
		updateResourceDisplay = true;
	}

	public void UpdateBuildingResourceDisplay()
	{
		updateBuildingResourceDisplay = true;
	}

	public void UpdateThrottleDisplay(float throttle, bool autoThrottle)
	{
		throttleDisplay.text = "Throttle - " + ((int)(throttle * 100.0f)) + "%";
		autoThrottleDisplay.text = "AutoThrottle - " + (autoThrottle ? "on" : "off");
	}

	public void AddMessage(string message)
	{
		Message messageRecord = new Message();
		messageRecord.message = message;
		messageRecord.timestamp = Time.realtimeSinceStartup;

		messages.Enqueue(messageRecord);
	}

	public int GetMessageCount()
	{
		return messages.Count;
	}

	public void SetShowBuildingResourceDisplay(bool showBuildingResourceDisplay)
	{
		updateBuildingResourceDisplay = showBuildingResourceDisplay;
		this.showBuildingResourceDisplay = showBuildingResourceDisplay;
	}

	public void SetBuildingCosts(GoodManager.Load[] buildingCosts)
	{
		if(buildingCosts != null)
		{
			this.buildingCosts = new Dictionary<string, uint>(buildingCosts.Length);
			foreach(GoodManager.Load cost in buildingCosts)
			{
				this.buildingCosts.Add(cost.goodName, cost.amount);
			}
		}
		else
		{
			this.buildingCosts = null;
		}

		UpdateBuildingResourceDisplay();
	}
}
