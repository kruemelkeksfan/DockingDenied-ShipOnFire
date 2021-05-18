using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class QuestVesselController : MonoBehaviour, IUpdateListener, IDockingListener, IListener
{
	[SerializeField] private RectTransform uiTransform = null;
	[SerializeField] private RectTransform mapMarker = null;
	[SerializeField] private Text mapMarkerName = null;
	[SerializeField] private Text mapMarkerDistance = null;
	[Tooltip("Distance up from which no more digital Digits will be displayed")]
	[SerializeField] private float decimalDigitThreshold = 100.0f;
	[SerializeField] private GameObject questVesselMenu = null;
	[SerializeField] private Text nameField = null;
	[SerializeField] private Text progressField = null;
	[SerializeField] private Text hintField = null;
	[SerializeField] private Button interactButton = null;
	private Spacecraft spacecraft = null;
	private new Transform transform = null;
	private Spacecraft localPlayerSpacecraft = null;
	private Transform localPlayerSpacecraftTransform = null;
	private InventoryController localPlayerMainInventory = null;
	private new Camera camera = null;
	private DockingPort[] dockingPorts = null;
	private QuestManager.Quest quest = null;
	private bool interactable = false;
	private bool playerDocked = false;

	private void Start()
	{
		ToggleController.GetInstance().AddToggleObject("SpacecraftMarkers", mapMarker.gameObject);

		spacecraft = GetComponent<Spacecraft>();
		transform = spacecraft.GetTransform();
		camera = Camera.main;
		dockingPorts = GetComponentsInChildren<DockingPort>();
		foreach(DockingPort port in dockingPorts)
		{
			port.AddDockingListener(this);
			port.HotkeyDown();
		}

		SpacecraftManager spacecraftManager = SpacecraftManager.GetInstance();
		localPlayerSpacecraft = spacecraftManager.GetLocalPlayerMainSpacecraft();
		localPlayerSpacecraftTransform = localPlayerSpacecraft.GetTransform();
		localPlayerMainInventory = localPlayerSpacecraft.GetComponent<InventoryController>();
		spacecraftManager.AddSpacecraftChangeListener(this);

		spacecraft.AddUpdateListener(this);
	}

	private void OnDestroy()
	{
		ToggleController.GetInstance().RemoveToggleObject("SpacecraftMarkers", mapMarker.gameObject);
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
		if(otherPort.GetComponentInParent<Spacecraft>() == localPlayerSpacecraft)
		{
			playerDocked = true;
		}

		if(quest.taskType == QuestManager.TaskType.Tow && otherPort.GetComponentInParent<SpaceStationController>() == quest.destination)
		{
			quest.progress = 1.0f;
			UpdateQuestVesselMenu();
		}
	}

	public void Undocked(DockingPort port, DockingPort otherPort)
	{
		if(otherPort.GetComponentInParent<Spacecraft>() == localPlayerSpacecraft)
		{
			playerDocked = false;
		}
	}

	public void Notify()
	{
		localPlayerSpacecraft = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft();
		localPlayerSpacecraftTransform = localPlayerSpacecraft.GetTransform();
		localPlayerMainInventory = localPlayerSpacecraft.GetComponent<InventoryController>();
	}

	public void ToggleQuestVesselMenu()
	{
		questVesselMenu.SetActive(!questVesselMenu.activeSelf);
	}

	public void UpdateQuestVesselMenu()
	{
		progressField.text = "Progress: " + Mathf.FloorToInt(quest.progress * 100.0f) + "%";

		if(quest.progress < 1.0f)
		{
			if(interactable && playerDocked)
			{
				interactButton.gameObject.SetActive(true);
			}
			else
			{
				interactButton.gameObject.SetActive(false);
			}
		}
		else
		{
			hintField.text = "Open Quest Menu of Station for Reward!";
			interactButton.gameObject.SetActive(false);
		}
	}

	public void SetQuest(QuestManager.Quest quest)
	{
		this.quest = quest;
		mapMarkerName.text = quest.vesselType.ToString() + " Vessel";
		nameField.text = mapMarkerName.text;

		if(quest.taskType == QuestManager.TaskType.Destroy)
		{
			hintField.text = "Kill Me!";
			interactable = false;
		}
		else if(quest.taskType == QuestManager.TaskType.Bribe)
		{
			hintField.text = "Dock to interact!";
			interactable = true;
			interactButton.GetComponentInChildren<Text>().text = "Bribe with 20$";
			interactButton.onClick.AddListener(delegate
					{
						if(localPlayerMainInventory.TransferMoney(-20))
						{
							quest.progress = 1.0f;
						}
						else
						{
							InfoController.GetInstance().AddMessage("This Guy couldn't even buy a Beer of the lousy Cash you have at Hand!");
						}
						UpdateQuestVesselMenu();
					});
		}
		else if(quest.taskType == QuestManager.TaskType.JumpStart)
		{
			hintField.text = "Dock to interact!";
			interactable = true;
			interactButton.GetComponentInChildren<Text>().text = "Jump-Start Engine";
			interactButton.onClick.AddListener(delegate
					{
						if(true)    // TODO: Drain Electricity
						{
							quest.progress = 1.0f;
						}
						else
						{
							InfoController.GetInstance().AddMessage("Your Batteries are running dry themselves!");
						}
						UpdateQuestVesselMenu();
					});
		}
		else if(quest.taskType == QuestManager.TaskType.Supply)
		{
			hintField.text = "Dock to interact!";
			interactable = true;
			interactButton.GetComponentInChildren<Text>().text = "Supply " + quest.infoString;
			interactButton.onClick.AddListener(delegate
					{
						int amount = Mathf.Max(quest.infoInt, (int)localPlayerMainInventory.GetGoodAmount(quest.infoString));
						if(localPlayerMainInventory.Withdraw(quest.infoString, (uint)amount))
						{
							quest.progress += (float)amount / (float)quest.infoInt;
						}
						else
						{
							Debug.LogWarning(quest.infoInt + " " + quest.infoString + " for Quest could not be supplied, Player Vessel has " + localPlayerMainInventory.GetGoodAmount(quest.infoString) + " " + quest.infoString + "!");
						}
						UpdateQuestVesselMenu();
					});
		}
		else if(quest.taskType == QuestManager.TaskType.Plunder)
		{
			hintField.text = "Dock to interact!";
			interactable = true;
			interactButton.onClick.AddListener(delegate
					{
						// TODO: Fun and interactive Gameplay?
						quest.progress = 1.0f;
						UpdateQuestVesselMenu();
					});
		}
		else if(quest.taskType == QuestManager.TaskType.Tow)
		{
			hintField.text = "Dock to start Towing!";
			interactable = false;
			quest.destination.RequestDocking(GetComponentInParent<Spacecraft>(), true);
		}
	}
}
