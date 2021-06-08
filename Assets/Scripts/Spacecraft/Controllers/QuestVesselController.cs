using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class QuestVesselController : MonoBehaviour, IUpdateListener, IDockingListener, IListener
{
	[SerializeField] private TextAsset[] questVesselBlueprints = { };
	[SerializeField] private RectTransform mapMarkerPrefab = null;
	[Tooltip("Distance up from which no more digital Digits will be displayed")]
	[SerializeField] private float decimalDigitThreshold = 100.0f;
	[Tooltip("Distance the Player needs to be away for this Vessel to start Despawning when its Quest is completed")]
	[SerializeField] private float playerDespawnDistance = 0.2f;
	private RectTransform uiTransform = null;
	private MenuController menuController = null;
	private Spacecraft spacecraft = null;
	private new Transform transform = null;
	private new Rigidbody2D rigidbody = null;
	private Spacecraft localPlayerSpacecraft = null;
	private Transform localPlayerSpacecraftTransform = null;
	private InventoryController localPlayerMainInventory = null;
	private PlayerSpacecraftUIController playerSpacecraftController = null;
	private new Camera camera = null;
	private Transform cameraTransform = null;
	private RectTransform mapMarker = null;
	private Text mapMarkerName = null;
	private Text mapMarkerDistance = null;
	private string vesselName = null;
	private string progress = null;
	private string hint = null;
	private string interactionLabel = null;
	private UnityAction interaction = null;
	private DockingPort[] dockingPorts = null;
	private QuestManager.Quest quest = null;
	private bool interactable = false;
	private bool playerDocked = false;
	private void Start()
	{
		spacecraft = GetComponent<Spacecraft>();
		transform = spacecraft.GetTransform();
		SpacecraftBlueprintController.LoadBlueprint(questVesselBlueprints[UnityEngine.Random.Range(0, questVesselBlueprints.Length)], transform);

		rigidbody = GetComponent<Rigidbody2D>();
		camera = Camera.main;
		cameraTransform = camera.GetComponent<Transform>();
		dockingPorts = GetComponentsInChildren<DockingPort>();
		foreach(DockingPort port in dockingPorts)
		{
			port.AddDockingListener(this);
			port.HotkeyDown();
		}

		playerDespawnDistance *= playerDespawnDistance;

		SpacecraftManager spacecraftManager = SpacecraftManager.GetInstance();
		localPlayerSpacecraft = spacecraftManager.GetLocalPlayerMainSpacecraft();
		localPlayerSpacecraftTransform = localPlayerSpacecraft.GetTransform();
		localPlayerMainInventory = localPlayerSpacecraft.GetComponent<InventoryController>();
		playerSpacecraftController = localPlayerSpacecraft.GetComponent<PlayerSpacecraftUIController>();
		spacecraftManager.AddSpacecraftChangeListener(this);

		spacecraft.AddUpdateListener(this);
	}

	private void OnDestroy()
	{
		if(mapMarker != null)
		{
			GameObject.Destroy(mapMarker.gameObject);
		}
	}

	public void UpdateNotify()
	{
		Vector2? uiPoint = ScreenUtility.WorldToUIPoint(transform.position, camera, cameraTransform, uiTransform);
		if(uiPoint.HasValue)
		{
			mapMarker.localScale = Vector3.one;
			mapMarker.anchoredPosition = uiPoint.Value;

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
		else
		{
			mapMarker.localScale = Vector3.zero;
		}

		if(quest.progress >= 1.0f && (transform.position - localPlayerSpacecraftTransform.position).sqrMagnitude > playerDespawnDistance)
		{
			StartCoroutine(SpawnController.GetInstance().DespawnObject(rigidbody));
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
		playerSpacecraftController = localPlayerSpacecraft.GetComponent<PlayerSpacecraftUIController>();
	}

	public void ToggleQuestVesselMenu()
	{
		playerSpacecraftController.SetTarget(rigidbody);
		menuController.ToggleQuestVesselMenu(this, vesselName, progress, hint, interactionLabel, (quest.progress < 1.0f && interactable && playerDocked) ? interaction : null);
	}

	public void UpdateQuestVesselMenu()
	{
		progress = "Progress: " + Mathf.FloorToInt(quest.progress * 100.0f) + "%";
		if(quest.progress >= 1.0f)
		{
			hint = "Dock to Station and open Quest Menu for Reward!";
		}

		menuController.UpdateQuestVesselMenu(this, vesselName, progress, hint, interactionLabel, (quest.progress < 1.0f && interactable && playerDocked) ? interaction : null);
	}

	public void SetQuest(QuestManager.Quest quest)
	{
		this.quest = quest;

		menuController = MenuController.GetInstance();
		uiTransform = menuController.GetUITransform();
		mapMarker = GameObject.Instantiate<RectTransform>(mapMarkerPrefab, menuController.GetMapMarkerParent());
		mapMarkerName = mapMarker.GetChild(0).GetComponent<Text>();
		mapMarkerDistance = mapMarker.GetChild(1).GetComponent<Text>();
		mapMarker.GetComponent<Button>().onClick.AddListener(delegate
		{
			ToggleQuestVesselMenu();
		});

		mapMarkerName.text = quest.vesselType.ToString() + " Vessel";
		vesselName = mapMarkerName.text;

		if(quest.taskType == QuestManager.TaskType.Destroy)
		{
			hint = "Kill Me!";
			interactable = false;
		}
		else if(quest.taskType == QuestManager.TaskType.Bribe)
		{
			hint = "Dock to interact!";
			interactable = true;
			interactionLabel = "Bribe with 20$";
			interaction = delegate
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
					};
		}
		else if(quest.taskType == QuestManager.TaskType.JumpStart)
		{
			hint = "Dock to interact!";
			interactable = true;
			interactionLabel = "Jump-Start with 1kWh";
			interaction = delegate
					{
						if(localPlayerMainInventory.TransferEnergy(-3600.0f))
						{
							quest.progress = 1.0f;
						}
						else
						{
							InfoController.GetInstance().AddMessage("Energy could not be supplied, Player Vessel Batteries are charged with " + localPlayerMainInventory.GetEnergyKWH() + "kWh!");
						}
						UpdateQuestVesselMenu();
					};
		}
		else if(quest.taskType == QuestManager.TaskType.Supply)
		{
			hint = "Dock to interact!";
			interactable = true;
			interactionLabel = "Supply " + quest.infoString;
			interaction = delegate
					{
						int amount = Mathf.Min(quest.infoInt, (int)localPlayerMainInventory.GetGoodAmount(quest.infoString));
						if(localPlayerMainInventory.Withdraw(quest.infoString, (uint)amount))
						{
							quest.progress += (float)amount / (float)quest.infoInt;
						}
						else
						{
							Debug.LogWarning(quest.infoInt + " " + quest.infoString + " for Quest could not be supplied, Player Vessel has " + localPlayerMainInventory.GetGoodAmount(quest.infoString) + " " + quest.infoString + "!");
						}
						UpdateQuestVesselMenu();
					};
		}
		else if(quest.taskType == QuestManager.TaskType.Plunder)
		{
			hint = "Dock to interact!";
			interactable = true;
			interactionLabel = "Plunder";
			interaction = delegate
					{
						// TODO: Fun and interactive Gameplay?
						quest.progress = 1.0f;
						UpdateQuestVesselMenu();
					};
		}
		else if(quest.taskType == QuestManager.TaskType.Tow)
		{
			hint = "Dock to start Towing!";
			interactable = false;
			quest.destination.RequestDocking(GetComponentInParent<Spacecraft>(), true);
		}

		UpdateQuestVesselMenu();
	}
}
