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
	[Tooltip("Delay before a Towing Quest is completed after Docking to make sure that the Station did not cancel the Docking")]
	[SerializeField] private float towingQuestCompleteDelay = 1.0f;
	[Tooltip("Delay for Reactivation of the Docking Port, if it is disabled, for Example after docking to the wrong Port of a Station")]
	[SerializeField] private float dockingPortReactivateDelay = 20.0f;
	[SerializeField] private float despawnDelay = 300.0f;
	private TimeController timeController = null;
	private RectTransform uiTransform = null;
	private MenuController menuController = null;
	private SpacecraftController spacecraft = null;
	private new Transform transform = null;
	private new Rigidbody2D rigidbody = null;
	private SpacecraftController localPlayerSpacecraft = null;
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
	private double questCompleteTime = -1.0f;

	private void Start()
	{
		spacecraft = GetComponent<SpacecraftController>();
		transform = spacecraft.GetTransform();
		SpacecraftBlueprintController.InstantiateModules(SpacecraftBlueprintController.LoadBlueprintModules(questVesselBlueprints[UnityEngine.Random.Range(0, questVesselBlueprints.Length)]), transform);

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

		timeController = TimeController.GetInstance();
		timeController.AddUpdateListener(this);
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
		if(quest.progress >= 1.0f)
		{
			if(questCompleteTime < 0.0f)
			{
				questCompleteTime = timeController.GetTime();
				mapMarker.localScale = Vector3.zero;
			}
			else if(timeController.GetTime() > questCompleteTime + despawnDelay && (transform.position - localPlayerSpacecraftTransform.position).sqrMagnitude > playerDespawnDistance)
			{
				timeController.StartCoroutine(SpawnController.GetInstance().DespawnObject(rigidbody), false);
			}
		}
		else
		{
			Vector2? uiPoint = ScreenUtility.WorldToUIPoint(transform.position, camera, cameraTransform, uiTransform);
			if(uiPoint.HasValue)
			{
				mapMarker.localScale = Vector3.one;
				mapMarker.anchoredPosition = uiPoint.Value;

				float distance = (transform.position - localPlayerSpacecraftTransform.position).magnitude;
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
	}

	public void Docked(DockingPort port, DockingPort otherPort)
	{
		if(otherPort.GetComponentInParent<SpacecraftController>() == localPlayerSpacecraft)
		{
			playerDocked = true;

			if(quest.taskType == QuestManager.TaskType.Tow)
			{
				hint = "Tow this Vessel to the Station!";
			}
		}

		if(quest.taskType == QuestManager.TaskType.Tow && otherPort.GetComponentInParent<SpaceStationController>() == quest.destination)
		{
			timeController.StartCoroutine(CompleteTowingQuest(port), false);
		}

		UpdateQuestVesselMenu();
	}

	private IEnumerator<float> CompleteTowingQuest(DockingPort port)
	{
		yield return towingQuestCompleteDelay;

		// Still docked?
		if(!port.IsFree())
		{
			quest.progress = 1.0f;
			quest.destination.AbortDocking(spacecraft);
		}
	}

	public void Undocked(DockingPort port, DockingPort otherPort)
	{
		if(otherPort.GetComponentInParent<SpacecraftController>() == localPlayerSpacecraft)
		{
			playerDocked = false;

			if(quest.taskType == QuestManager.TaskType.Tow)
			{
				hint = "Dock to start Towing!";
			}
		}

		// Check if Port and gameObject are still active, to avoid starting Coroutines when getting undocked due to Vessel Destruction
		if(!port.IsActive() && port.gameObject.activeSelf && gameObject.activeSelf)
		{
			quest.progress = 0.0002f;
			timeController.StartCoroutine(ReactivateDockingPort(port), false);
		}

		UpdateQuestVesselMenu();
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
		playerSpacecraftController.SetTarget(spacecraft, transform, rigidbody);
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

	public IEnumerator<float> ReactivateDockingPort(DockingPort port)
	{
		yield return dockingPortReactivateDelay;

		if(!port.IsActive())
		{
			port.HotkeyDown();
		}
	}

	public QuestManager.Quest GetQuest()
	{
		return quest;
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

		/* TODO: if(quest.taskType == QuestManager.TaskType.Destroy)
		{
			hint = "Kill Me!";
			interactable = false;
		}
		else */
		if(quest.taskType == QuestManager.TaskType.Bribe)
		{
			hint = "Dock to interact!";
			interactable = true;
			interactionLabel = "Bribe with 200$";
			interaction = delegate
					{
						if(localPlayerMainInventory.TransferMoney(-200))
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
			hint = "Dock to interact! You will need a Battery!";
			interactable = true;
			interactionLabel = "Jump-Start with 5kWh";
			interaction = delegate
					{
						if(localPlayerMainInventory.TransferEnergy(-18000.0f))
						{
							quest.progress = 1.0f;
						}
						else
						{
							InfoController.GetInstance().AddMessage("Energy could not be supplied, your Batteries are charged with " + localPlayerMainInventory.GetEnergyKWH() + "!");
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
			quest.destination.RequestDocking(GetComponentInParent<SpacecraftController>());
		}

		UpdateQuestVesselMenu();
	}
}
