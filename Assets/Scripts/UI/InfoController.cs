using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class InfoController : MonoBehaviour
{
    private struct Message
	{
        public string message;
        public float timestamp;

		public Message(string message)
		{
			this.message = message;
			this.timestamp = Time.time;
		}
	}

	public static InfoController instance = null;

    [SerializeField] private Text messageField = null;
    [SerializeField] private float messageDuration = 40.0f;
	[SerializeField] private Text controlHint = null;
	[SerializeField] private Text resourceDisplay = null;
	[SerializeField] private Text buildingResourceDisplay = null;
	private Queue<Message> messages = null;

	public static InfoController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		messages = new Queue<Message>();
		instance = this;
	}

	private void Update()
	{
		while(messages.Count > 0 && messages.Peek().timestamp + messageDuration < Time.time)
		{
			messages.Dequeue();
		}

		StringBuilder messageText = new StringBuilder();
		foreach(Message message in messages)
		{
			messageText.AppendLine(message.message);
		}
		messageField.text = messageText.ToString();
	}

	public void UpdateControlHint(Dictionary<string, string[]> keyBindings)
	{
		StringBuilder hint = new StringBuilder(256);
		foreach(string key in keyBindings.Keys)
		{
			if(keyBindings[key].Length > 0)
			{
				hint.Append(key + " -\t");
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

	public void UpdateResourceDisplays(InventoryController inventoryController)
	{
		resourceDisplay.text = inventoryController.GetMoney() + "$ / Energy - 0 / Hydrogen - " + inventoryController.GetGoodAmount("Hydrogen") + " / Oxygen - " + inventoryController.GetGoodAmount("Oxygen")
			+ " / Food - " + inventoryController.GetGoodAmount("Food") + " / Water - " + inventoryController.GetGoodAmount("Water");
		buildingResourceDisplay.text = "Steel - " + inventoryController.GetGoodAmount("Steel") + " / Aluminium - "
			+ inventoryController.GetGoodAmount("Aluminium") + " / Copper - " + inventoryController.GetGoodAmount("Copper") + " / Gold - " + inventoryController.GetGoodAmount("Gold") + " / Silicon - " + inventoryController.GetGoodAmount("Silicon");
	}

	public void AddMessage(string message)
	{
		messages.Enqueue(new Message(message));
	}
}
