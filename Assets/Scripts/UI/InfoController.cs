﻿using System.Collections;
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
	[SerializeField] private Text buildingResourceDisplay = null;
	[SerializeField] private Text throttleDisplay = null;
	[SerializeField] private Text autoThrottleDisplay = null;
	[SerializeField] private GameObject keyBindingDisplay = null;
	private Queue<Message> messages = null;
	private float lastDequeue = 0.0f;
	private Dictionary<string, uint> buildingCosts = null;
	private InventoryController inventoryController = null;

	public static InfoController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");												// Decimal Points FTW!!elf!

		messages = new Queue<Message>();
		instance = this;
	}

	private void Start()
	{
		SpacecraftManager spacecraftManager = SpacecraftManager.GetInstance();
		inventoryController = spacecraftManager.GetLocalPlayerMainSpacecraft().GetComponent<InventoryController>();
		spacecraftManager.AddSpacecraftChangeListener(this);
	}

	// TODO: Put this into a Method which only gets called when a new Message is added or a Message Timestamp runs out (Coroutine)
	private void Update()
	{
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

		StringBuilder messageText = new StringBuilder();
		foreach(Message message in messages)
		{
			messageText.AppendLine(message.message);
		}
		messageField.text = messageText.ToString();
	}

	public void Notify()
	{
		inventoryController = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft().GetComponent<InventoryController>();
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

	// TODO: Rather set a bool here and update in Update() when bool is true, to avoid Updating dozens of Times each frame
	public void UpdateResourceDisplays()
	{
		if(inventoryController != null)
		{
			resourceDisplay.text = inventoryController.GetMoney() + "$ / Energy - " + inventoryController.GetEnergyKWH() + "kWh"
				/* + " / Hydrogen - " + inventoryController.GetGoodAmount("Hydrogen") + " / Oxygen - " + inventoryController.GetGoodAmount("Oxygen")
			+ " / Food - " + inventoryController.GetGoodAmount("Food") + " / Water - " + inventoryController.GetGoodAmount("Water")*/;
			if(buildingCosts == null)
			{
				buildingResourceDisplay.text = "Steel - " + inventoryController.GetGoodAmount("Steel")
					+ " / Aluminium - " + inventoryController.GetGoodAmount("Aluminium")
					+ " / Copper - " + inventoryController.GetGoodAmount("Copper")
					+ " / Gold - " + inventoryController.GetGoodAmount("Gold")
					+ " / Silicon - " + inventoryController.GetGoodAmount("Silicon");
			}
			else
			{
				buildingResourceDisplay.text = "Steel - " + inventoryController.GetGoodAmount("Steel") + " (" + (buildingCosts.ContainsKey("Steel") ? buildingCosts["Steel"] : 0) + ")"
					+ " / Aluminium - " + inventoryController.GetGoodAmount("Aluminium") + " (" + (buildingCosts.ContainsKey("Aluminium") ? buildingCosts["Aluminium"] : 0) + ")"
					+ " / Copper - " + inventoryController.GetGoodAmount("Copper") + " (" + (buildingCosts.ContainsKey("Copper") ? buildingCosts["Copper"] : 0) + ")"
					+ " / Gold - " + inventoryController.GetGoodAmount("Gold") + " (" + (buildingCosts.ContainsKey("Gold") ? buildingCosts["Gold"] : 0) + ")"
					+ " / Silicon - " + inventoryController.GetGoodAmount("Silicon") + " (" + (buildingCosts.ContainsKey("Silicon") ? buildingCosts["Silicon"] : 0) + ")";
			}
		}
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

		UpdateResourceDisplays();
	}
}
