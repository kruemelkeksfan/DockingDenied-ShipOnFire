using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Module : MonoBehaviour, IUpdateListener, IFixedUpdateListener
{
	[SerializeField] protected string moduleName = "Module";
	[SerializeField] protected int hp = 100;
	[SerializeField] protected bool pressurized = true;
	[SerializeField] private Vector2Int[] reservedPositions = { Vector2Int.zero };
	[Tooltip("Whether all reserved Positions after the First still provide valid Attachment Points.")]
	[SerializeField] private bool attachableReservePositions = false;
	[Tooltip("Whether all reserved Positions after the First can overlap with other reserved Positions which have this Flag enabled.")]
	[SerializeField] private bool overlappingReservePositions = false;
	[SerializeField] private GoodManager.Load[] buildingCosts = { new GoodManager.Load("Steel", 0), new GoodManager.Load("Aluminium", 0),
		new GoodManager.Load("Copper", 0), new GoodManager.Load("Gold", 0), new GoodManager.Load("Silicon", 0) };
	[TextArea(1, 2)] [SerializeField] private string description = "Module Description missing!";
	protected TimeController timeController = null;
	protected AudioController audioController = null;
	protected GoodManager goodManager = null;
	protected ToggleController toggleController = null;
	protected CrewCabin crewCabin = null;
	protected float mass = MathUtil.EPSILON;
	private Vector2Int[] bufferedReservedPositions = { Vector2Int.zero };
	protected bool constructed = false;
	protected new Transform transform = null;
	protected SpacecraftController spacecraft = null;
	protected Vector2Int position = Vector2Int.zero;
	// Dictionary to enable easy Searching for Component Types
	private Dictionary<GoodManager.ComponentType, ModuleComponent> componentSlots = null;
	// Need a second, ordered List for Display in Module Menu
	private List<GoodManager.ComponentType> orderedComponentSlots = null;
	// TODO: Save Custom Name in SpacecraftBlueprintController
	private string customModuleName = "unnamed";
	private GameObject moduleMenuButton = null;
	private GameObject moduleMenu = null;
	private RectTransform uiTransform = null;
	private new Camera camera = null;
	private Transform cameraTransform = null;
	private RectTransform moduleMenuButtonTransform = null;

	protected virtual void Awake()
	{
		transform = gameObject.GetComponent<Transform>();

		componentSlots = new Dictionary<GoodManager.ComponentType, ModuleComponent>();
		orderedComponentSlots = new List<GoodManager.ComponentType>();

		customModuleName = moduleName;

		// Needs to be retrieved in Awake(), because e.g. Quest Vessels need those Controllers during Spawn
		timeController = TimeController.GetInstance();
		audioController = AudioController.GetInstance();
		goodManager = GoodManager.GetInstance();
	}

	protected virtual void Start()
	{
		if(timeController == null || audioController == null || goodManager == null)
		{
			timeController = TimeController.GetInstance();
			audioController = AudioController.GetInstance();
			goodManager = GoodManager.GetInstance();
		}
	}

	protected virtual void OnDestroy()
	{

	}

	public virtual void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		spacecraft = gameObject.GetComponentInParent<SpacecraftController>();

		this.position = position;
		transform.localPosition = BuildingMenu.GetInstance().GridToLocalPosition(position);
		UpdateReservedPositionBuffer(position, transform.localRotation);
		constructed = true;

		foreach(Vector2Int bufferedReservedPosition in bufferedReservedPositions)
		{
			spacecraft.AddModule(bufferedReservedPosition, this);
		}

		if(mass <= MathUtil.EPSILON * 2.0f)
		{
			TryCalculateMass();
		}
		spacecraft.UpdateMass();

		if(pressurized)
		{
			crewCabin = new CrewCabin();
			AddComponentSlot(GoodManager.ComponentType.CrewCabin, crewCabin);
		}

		if(SpacecraftManager.GetInstance().IsPlayerSpacecraft(spacecraft))
		{
			MenuController menuController = MenuController.GetInstance();
			toggleController = ToggleController.GetInstance();

			uiTransform = menuController.GetUITransform();
			camera = Camera.main;
			cameraTransform = camera.GetComponent<Transform>();

			moduleMenu = GameObject.Instantiate<GameObject>(menuController.GetModuleMenuPrefab(), menuController.GetModuleMenuParent());
			moduleMenu.GetComponentInChildren<Button>().onClick.AddListener(delegate
					{
						ToggleModuleMenu();
					});

			Button moduleMenuButton = GameObject.Instantiate<Button>(menuController.GetModuleMenuButtonPrefab(), menuController.GetModuleMenuButtonParent());
			moduleMenuButton.GetComponentInChildren<Text>().text = customModuleName;
			moduleMenuButton.onClick.AddListener(delegate
					{
						ToggleModuleMenu();
					});

			this.moduleMenuButton = moduleMenuButton.gameObject;
			moduleMenuButtonTransform = moduleMenuButton.GetComponent<RectTransform>();
			toggleController.AddToggleObject("ModuleMenuButtons", this.moduleMenuButton);
			this.moduleMenuButton.SetActive(toggleController.IsGroupToggled("ModuleMenuButtons"));
		}

		if(timeController == null)
		{
			timeController = TimeController.GetInstance();
		}
		if(listenUpdates || moduleMenuButton != null)
		{
			timeController.AddUpdateListener(this);
		}
		if(listenFixedUpdates)
		{
			timeController.AddFixedUpdateListener(this);
		}
	}

	public virtual void Deconstruct()
	{
		if(toggleController != null && moduleMenuButton != null && moduleMenu != null)
		{
			toggleController.RemoveToggleObject("ModuleMenuButtons", moduleMenuButton.gameObject);

			GameObject.Destroy(moduleMenuButton);
			GameObject.Destroy(moduleMenu);
		}

		foreach(Vector2Int bufferedReservedPosition in bufferedReservedPositions)
		{
			spacecraft.RemoveModule(bufferedReservedPosition);
		}

		spacecraft.UpdateMass();

		GameObject.Destroy(gameObject);

		timeController.RemoveUpdateListener(this);
		timeController.RemoveFixedUpdateListener(this);
	}

	public void Rotate(int direction)
	{
		Rotate(direction * -90.0f);
	}

	public void Rotate(float angle)
	{
		if(angle % 90.0f == 0.0f)
		{
			transform.localRotation = Quaternion.Euler(0.0f, 0.0f, angle);
		}
		else
		{
			Debug.LogWarning("Trying to rotate Module " + this + " by " + angle + "Degrees which is not a Multiple of 90.0 Degrees!");
		}
	}

	public virtual void UpdateNotify()
	{
		if(moduleMenuButton != null && moduleMenuButton.activeSelf)
		{
			Vector2? uiPoint = ScreenUtility.WorldToUIPoint(transform.position, camera, cameraTransform, uiTransform);
			if(uiPoint.HasValue)
			{
				moduleMenuButtonTransform.localScale = Vector3.one;
				moduleMenuButtonTransform.anchoredPosition = uiPoint.Value;
			}
			else
			{
				moduleMenuButtonTransform.localScale = Vector3.zero;
			}
		}
	}

	public virtual void FixedUpdateNotify()
	{

	}

	protected void AddComponentSlot(GoodManager.ComponentType componentType, ModuleComponent component)
	{
		componentSlots.Add(componentType, component);
		orderedComponentSlots.Add(componentType);
	}

	public bool InstallComponent(string componentName)
	{
		GoodManager.ComponentType componentType = goodManager.GetComponentData(componentName).type;
		if(componentSlots.ContainsKey(componentType))
		{
			if(!componentSlots[componentType].IsSet())
			{
				return componentSlots[componentType].UpdateComponentData(componentName);
			}
			else
			{
				Debug.LogWarning("Trying to install " + componentType + "-Component " + componentName + " in an occupied Slot of " + moduleName + "!");
			}
		}
		else
		{
			Debug.LogWarning("Trying to install unsupported " + componentType + "-Component " + componentName + " in " + moduleName + "!");
		}

		return false;
	}

	public bool RemoveComponent(GoodManager.ComponentType componentType)
	{
		if(componentSlots.ContainsKey(componentType))
		{
			if(componentSlots[componentType].IsSet())
			{
				return componentSlots[componentType].UpdateComponentData(null);
			}
			else
			{
				Debug.LogWarning("Trying to remove " + componentType + "-Component from an unoccupied Slot of " + moduleName + "!");
			}
		}
		else
		{
			Debug.LogWarning("Trying to remove unsupported " + componentType + "-Component from " + moduleName + "!");
		}

		return false;
	}

	private void TryCalculateMass()
	{
		GoodManager goodManager = GoodManager.GetInstance();
		if(goodManager != null)
		{
			mass = 0.0f;
			foreach(GoodManager.Load cost in buildingCosts)
			{
				mass += goodManager.GetGood(cost.goodName).mass * cost.amount;
			}
			if(mass <= 0.0f)
			{
				mass = 0.0002f;
			}
		}
	}

	private void UpdateReservedPositionBuffer(Vector2Int position, Quaternion localRotation)
	{
		if(!constructed)
		{
			bufferedReservedPositions = new Vector2Int[reservedPositions.Length];
			for(int i = 0; i < bufferedReservedPositions.Length; ++i)
			{
				bufferedReservedPositions[i] = Vector2Int.RoundToInt(position + (Vector2)(localRotation * (Vector2)reservedPositions[i]));
			}
		}
	}

	// Don't use ToggleController, since we only want to toggle 1 ModuleMenu, not all
	public void ToggleModuleMenu()
	{
		moduleMenu.GetComponentsInChildren<Text>()[2].text = customModuleName;

		moduleMenu.SetActive(!moduleMenu.activeSelf);
	}

	public string GetModuleName()
	{
		return moduleName;
	}

	public string GetDescription()
	{
		return description;
	}

	public Vector2Int GetPosition()
	{
		return position;
	}

	public Transform GetTransform()
	{
		return transform;
	}

	public SpacecraftController GetSpacecraft()
	{
		return spacecraft;
	}

	public Vector2Int[] GetReservedPositions(Vector2Int position, Quaternion localRotation)
	{
		UpdateReservedPositionBuffer(position, localRotation);

		return bufferedReservedPositions;
	}

	public int GetReservedPositionCount()
	{
		return reservedPositions.Length;
	}

	public bool HasAttachableReservePositions()
	{
		return attachableReservePositions;
	}

	public bool HasOverlappingReservePositions()
	{
		return overlappingReservePositions;
	}

	public GoodManager.Load[] GetBuildingCosts()
	{
		return buildingCosts;
	}

	public float GetMass()
	{
		if(mass <= MathUtil.EPSILON * 2.0f)
		{
			TryCalculateMass();
		}

		return mass;
	}
}
