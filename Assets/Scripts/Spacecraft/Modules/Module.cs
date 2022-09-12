using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
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
	[SerializeField]
	private GoodManager.Load[] buildingCosts = { new GoodManager.Load("Steel", 0), new GoodManager.Load("Aluminium", 0),
		new GoodManager.Load("Copper", 0), new GoodManager.Load("Gold", 0), new GoodManager.Load("Silicon", 0) };
	[SerializeField] private int maxModuleMenuButtonCharacters = 24;
	[SerializeField] private GameObject moduleSettingPrefab = null;
	protected TimeController timeController = null;
	protected AudioController audioController = null;
	protected GoodManager goodManager = null;
	private MenuController menuController = null;
	protected ToggleController toggleController = null;
	protected InfoController infoController = null;
	protected InventoryController inventoryController = null;
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
	protected string customModuleName = "unnamed";
	private GameObject moduleMenuButton = null;
	protected GameObject moduleMenu = null;
	private RectTransform uiTransform = null;
	private new Camera camera = null;
	private Transform cameraTransform = null;
	private RectTransform moduleMenuButtonTransform = null;
	private RectTransform componentPanel = null;
	private List<RectTransform> componentSlotEntries = null;
	private RectTransform moduleComponentSelectionPanel = null;
	protected RectTransform settingPanel = null;

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
		infoController = InfoController.GetInstance();
	}

	protected virtual void Start()
	{
		if(timeController == null || audioController == null || goodManager == null || infoController == null)
		{
			timeController = TimeController.GetInstance();
			audioController = AudioController.GetInstance();
			goodManager = GoodManager.GetInstance();
			infoController = InfoController.GetInstance();
		}
	}

	protected virtual void OnDestroy()
	{

	}

	public virtual void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		spacecraft = gameObject.GetComponentInParent<SpacecraftController>();

		inventoryController = spacecraft.GetInventoryController();

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

		if(SpacecraftManager.GetInstance().IsPlayerSpacecraft(spacecraft))
		{
			menuController = MenuController.GetInstance();
			toggleController = ToggleController.GetInstance();

			moduleComponentSelectionPanel = menuController.GetModuleComponentSelectionPanel();

			uiTransform = menuController.GetUITransform();
			camera = Camera.main;
			cameraTransform = camera.GetComponent<Transform>();

			Button moduleMenuButton = GameObject.Instantiate<Button>(menuController.GetModuleMenuButtonPrefab(), menuController.GetModuleMenuButtonParent());
			this.moduleMenuButton = moduleMenuButton.gameObject;
			moduleMenuButtonTransform = moduleMenuButton.GetComponent<RectTransform>();
			UpdateModuleMenuButtonText();
			moduleMenuButton.onClick.AddListener(delegate
					{
						ToggleModuleMenu();
					});

			moduleMenu = GameObject.Instantiate<GameObject>(menuController.GetModuleMenuPrefab(), menuController.GetModuleMenuParent());
			moduleMenu.GetComponentInChildren<Button>().onClick.AddListener(delegate
					{
						ToggleModuleMenu();
					});
			InputField moduleNameField = moduleMenu.GetComponentInChildren<InputField>();
			moduleNameField.onEndEdit.AddListener(delegate
				{
					SetCustomModuleName(moduleNameField.text);
				});
			componentPanel = (RectTransform)moduleMenu.GetComponentInChildren<VerticalLayoutGroup>().GetComponent<RectTransform>().GetChild(3);
			componentSlotEntries = new List<RectTransform>();

			if(moduleSettingPrefab != null)
			{
				settingPanel = (RectTransform)moduleMenu.GetComponentInChildren<VerticalLayoutGroup>().GetComponent<RectTransform>().GetChild(7);
				GameObject.Instantiate<GameObject>(moduleSettingPrefab, settingPanel);
			}
			else
			{
				moduleMenu.GetComponentInChildren<VerticalLayoutGroup>().GetComponent<RectTransform>().GetChild(6).gameObject.SetActive(false);
			}

			toggleController.AddToggleObject("ModuleMenuButtons", this.moduleMenuButton);
			this.moduleMenuButton.SetActive(toggleController.IsGroupToggled("ModuleMenuButtons"));
		}

		if(pressurized)
		{
			crewCabin = new CrewCabin();
			AddComponentSlot(GoodManager.ComponentType.CrewCabin, crewCabin);
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

		if(moduleMenu != null)
		{
			RectTransform componentSlotEntry = GameObject.Instantiate<RectTransform>(menuController.GetModuleComponentEntryPrefab(), componentPanel);
			componentPanel.sizeDelta = componentPanel.sizeDelta + new Vector2(0.0f, componentSlotEntry.sizeDelta.y);

			int localSlotId = orderedComponentSlots.Count - 1;
			componentSlotEntry.GetComponent<Button>().onClick.AddListener(delegate
				{
					ComponentSlotClick(localSlotId);
				});

			componentSlotEntries.Add(componentSlotEntry);
		}
	}

	public bool InstallComponent(string componentName)
	{
		GoodManager.ComponentType componentType = goodManager.GetComponentData(componentName).type;
		if(componentSlots.ContainsKey(componentType))
		{
			if(!componentSlots[componentType].IsSet())
			{
				bool componentSwapSuccess = componentSlots[componentType].UpdateComponentData(componentName);
				if(moduleMenu != null)
				{
					UpdateComponentButtons();
				}
				return componentSwapSuccess;
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
				bool componentSwapSuccess = componentSlots[componentType].UpdateComponentData(null);
				if(moduleMenu != null)
				{
					UpdateComponentButtons();
				}
				return componentSwapSuccess;
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

	public bool UninstallAllComponents()
	{
		foreach(GoodManager.ComponentType componentSlot in orderedComponentSlots)
		{
			if(componentSlots[componentSlot].IsSet())
			{
				if(inventoryController.Deposit(componentSlots[componentSlot].GetName(), 1))
				{
					RemoveComponent(componentSlot);
				}
				else
				{
					return false;
				}
			}
		}

		return true;
	}

	private void UpdateComponentButtons()
	{
		for(int i = 0; i < componentSlotEntries.Count; ++i)
		{
			RectTransform componentSlotEntry = componentSlotEntries[i];
			GoodManager.ComponentType componentType = orderedComponentSlots[i];

			Text[] componentSlotEntryTexts = componentSlotEntry.GetComponentsInChildren<Text>();
			if(!componentSlots[componentType].IsSet())
			{
				componentSlotEntryTexts[0].text = goodManager.GetComponentName(componentType);
				componentSlotEntryTexts[1].text = "<empty>";
			}
			else
			{
				componentSlotEntryTexts[0].text = componentSlots[componentType].GetName();
				componentSlotEntryTexts[1].text = componentSlots[componentType].GetAttributeList();
			}
		}
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

	public void UpdateModuleMenuButtonText()
	{
		if(customModuleName.Length <= maxModuleMenuButtonCharacters)
		{
			moduleMenuButton.GetComponentInChildren<Text>().text = customModuleName;
		}
		else
		{
			moduleMenuButton.GetComponentInChildren<Text>().text = customModuleName.Substring(0, maxModuleMenuButtonCharacters) + "...";
		}
	}

	// Don't use ToggleController, since we only want to toggle 1 ModuleMenu, not all
	public void ToggleModuleMenu()
	{
		moduleMenu.GetComponentInChildren<InputField>().text = customModuleName;
		UpdateComponentButtons();

		moduleMenu.SetActive(!moduleMenu.activeSelf);

		menuController.UpdateFlightControls();
	}

	public void ComponentSlotClick(int componentSlotIndex)
	{
		GoodManager.ComponentType slotType = orderedComponentSlots[componentSlotIndex];

		if(componentSlots[slotType].IsSet())
		{
			if(inventoryController.Deposit(componentSlots[slotType].GetName(), 1))
			{
				RemoveComponent(slotType);
			}
			else
			{
				infoController.AddMessage("Unable to remove " + componentSlots[slotType].GetName() + ", because there is to Space to store it!", true);
			}
		}
		else
		{
			RectTransform selectionList = moduleComponentSelectionPanel.GetComponentInChildren<VerticalLayoutGroup>().GetComponent<RectTransform>();

			// Clear Component Selection List
			for(int i = 0; i < selectionList.childCount; ++i)
			{
				GameObject.Destroy(selectionList.GetChild(i).gameObject);
			}

			// Add Assemble Component Option
			string assembleComponentName = goodManager.GetComponentName(slotType) + " [crude]";
			GoodManager.ComponentData crudeComponentData = goodManager.GetComponentData(assembleComponentName);
			RectTransform assembleComponentEntry = GameObject.Instantiate<RectTransform>(menuController.GetModuleComponentEntryPrefab(), selectionList);

			Text[] assembleComponentEntryTexts = assembleComponentEntry.GetComponentsInChildren<Text>();
			StringBuilder assembleString = new StringBuilder();
			assembleString.Append("Assemble Component: \n");
			bool first = true;
			foreach(GoodManager.Load cost in crudeComponentData.buildingCosts)
			{
				if(!first)
				{
					assembleString.Append(", ");
				}
				assembleString.Append(cost.amount);
				assembleString.Append(" ");
				assembleString.Append(cost.goodName);

				first = false;
			}
			assembleComponentEntryTexts[0].text = assembleString.ToString();
			assembleComponentEntryTexts[1].text = ModuleComponent.GetAttributeList(crudeComponentData);

			assembleComponentEntry.GetComponent<Button>().onClick.AddListener(delegate
			{
				Constructor constructor = null;
				if((constructor = BuildingMenu.GetInstance().FindBuildingConstructor(transform.position, crudeComponentData.buildingCosts)) != null)
				{
					InstallComponent(assembleComponentName);
					moduleComponentSelectionPanel.gameObject.SetActive(false);
				}
				else
				{
					infoController.AddMessage("Unable to assemble " + assembleComponentName + ", because there are either no Constructors in Range or no Building Materials available!", true);
				}
			});

			// Fill Component Selection List
			foreach(GoodManager.ComponentData componentData in inventoryController.GetModuleComponentsInInventory(slotType))
			{
				RectTransform componentSlotEntry = GameObject.Instantiate<RectTransform>(menuController.GetModuleComponentEntryPrefab(), selectionList);

				Text[] componentSlotEntryTexts = componentSlotEntry.GetComponentsInChildren<Text>();
				componentSlotEntryTexts[0].text = componentData.type.ToString() + " [" + componentData.quality + "]";
				componentSlotEntryTexts[1].text = ModuleComponent.GetAttributeList(componentData);

				int localComponentData = orderedComponentSlots.Count - 1;
				componentSlotEntry.GetComponent<Button>().onClick.AddListener(delegate
					{
						if(inventoryController.Withdraw(componentData.goodName, 1))
						{
							InstallComponent(componentData.goodName);
							moduleComponentSelectionPanel.gameObject.SetActive(false);
						}
						else
						{
							infoController.AddMessage("Unable to install " + componentSlots[slotType].GetName() + ", because it can not be retrieved from Inventory!", true);
						}
					});
			}

			moduleComponentSelectionPanel.gameObject.SetActive(true);
		}
	}

	public List<GoodManager.ComponentType> GetEmptyComponentSlots()
	{
		List<GoodManager.ComponentType> emptyComponentSlots = new List<GoodManager.ComponentType>(orderedComponentSlots.Count);

		foreach(GoodManager.ComponentType componentSlot in componentSlots.Keys)
		{
			if(!componentSlots[componentSlot].IsSet())
			{
				emptyComponentSlots.Add(componentSlot);
			}
		}

		return emptyComponentSlots;
	}

	public T GetModuleComponent<T>(GoodManager.ComponentType componentType) where T : ModuleComponent
	{
		return (T) componentSlots[componentType];
	}

	public string GetModuleName()
	{
		return moduleName;
	}

	public string GetCustomModuleName()
	{
		return customModuleName;
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

	public virtual void SetCustomModuleName(string customModuleName)
	{
		this.customModuleName = customModuleName;
		if(moduleMenu != null)
		{
			UpdateModuleMenuButtonText();
		}
	}
}
