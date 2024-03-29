﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class Module : MonoBehaviour, IUpdateListener, IFixedUpdateListener
{
	[SerializeField] protected string moduleName = "Module";
	[SerializeField] protected int maxHp = 100;
	[SerializeField] protected bool pressurized = true;
	[SerializeField] private Vector2Int[] reservedPositions = { Vector2Int.zero };
	[Tooltip("Whether all reserved Positions after the First still provide valid Attachment Points.")]
	[SerializeField] private bool attachableReservePositions = false;
	[Tooltip("Whether all reserved Positions after the First can overlap with other reserved Positions which have this Flag enabled.")]
	[SerializeField] private bool overlappingReservePositions = false;
	[SerializeField] private GoodManager.Load[] buildingCosts = { new GoodManager.Load("Steel", 0), new GoodManager.Load("Aluminium", 0),
		new GoodManager.Load("Copper", 0), new GoodManager.Load("Gold", 0), new GoodManager.Load("Silicon", 0) };
	[SerializeField] private GameObject moduleSettingPrefab = null;
	protected TimeController timeController = null;
	protected AudioController audioController = null;
	protected GoodManager goodManager = null;
	private MenuController menuController = null;
	protected ToggleController toggleController = null;
	protected InfoController infoController = null;
	protected ModuleManager moduleManager = null;
	protected InventoryController inventoryController = null;
	protected CrewCabin crewCabin = null;
	protected int hp = 0;
	protected float temperature = 0.0f;
	protected float condition = 1.0f;
	protected float mass = MathUtil.EPSILON;
	private Vector2Int[] bufferedReservedPositions = { Vector2Int.zero };
	private bool constructed = false;
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
	protected RectTransform statusBarParent = null;
	private RectTransform hpBar = null;
	protected GameObject moduleMenu = null;
	private RectTransform uiTransform = null;
	private new Camera camera = null;
	private Transform cameraTransform = null;
	private RectTransform moduleMenuButtonTransform = null;
	private RectTransform statusPanel = null;
	private Dictionary<string, Text> statusEntries = null;
	private RectTransform componentPanel = null;
	private List<RectTransform> componentSlotEntries = null;
	private RectTransform moduleComponentSelectionPanel = null;
	protected RectTransform settingPanel = null;

	protected virtual void Awake()
	{
		transform = gameObject.GetComponent<Transform>();

		statusEntries = new Dictionary<string, Text>();

		componentSlots = new Dictionary<GoodManager.ComponentType, ModuleComponent>();
		orderedComponentSlots = new List<GoodManager.ComponentType>();

		customModuleName = moduleName;

		// Needs to be retrieved in Awake(), because e.g. Quest Vessels need those Controllers during Spawn
		timeController = TimeController.GetInstance();
		audioController = AudioController.GetInstance();
		goodManager = GoodManager.GetInstance();
		infoController = InfoController.GetInstance();
		moduleManager = ModuleManager.GetInstance();

		hp = maxHp;
		if(moduleManager != null)
		{
			temperature = moduleManager.GetDefaultTemperature();
		}
	}

	protected virtual void Start()
	{
		if(timeController == null || audioController == null || goodManager == null || infoController == null || moduleManager == null)
		{
			timeController = TimeController.GetInstance();
			audioController = AudioController.GetInstance();
			goodManager = GoodManager.GetInstance();
			infoController = InfoController.GetInstance();
			moduleManager = ModuleManager.GetInstance();

			temperature = moduleManager.GetDefaultTemperature();
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

			// Module Menu
			// Header
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
			RectTransform content = moduleMenu.GetComponentInChildren<VerticalLayoutGroup>().GetComponent<RectTransform>();

			// Status
			statusPanel = (RectTransform)content.GetChild(1);
			AddStatusField("HP", (hp + "/" + maxHp));
			AddStatusField("Temperature", (Mathf.FloorToInt(temperature) + " K"));
			AddStatusField("Condition", (Mathf.FloorToInt(condition * 100.0f) + "%"));

			// Inventory
			content.GetChild(2).gameObject.SetActive(false);
			content.GetChild(3).gameObject.SetActive(false);

			// Components
			componentPanel = (RectTransform)content.GetChild(5);
			componentSlotEntries = new List<RectTransform>();

			// Settings
			if(moduleSettingPrefab != null)
			{
				settingPanel = (RectTransform)content.GetChild(9);
				GameObject.Instantiate<GameObject>(moduleSettingPrefab, settingPanel);
			}
			else
			{
				content.GetChild(8).gameObject.SetActive(false);
			}

			// Module Menu Button
			Button moduleMenuButton = GameObject.Instantiate<Button>(menuController.GetModuleMenuButtonPrefab(), menuController.GetModuleMenuButtonParent());
			this.moduleMenuButton = moduleMenuButton.gameObject;
			moduleMenuButtonTransform = moduleMenuButton.GetComponent<RectTransform>();
			statusBarParent = moduleMenuButtonTransform.GetChild(1).GetComponent<RectTransform>();
			UpdateModuleStatus();
			moduleMenuButton.onClick.AddListener(delegate
					{
						ToggleModuleMenu();
					});

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

		timeController.RemoveUpdateListener(this);
		timeController.RemoveFixedUpdateListener(this);

		GameObject.Destroy(gameObject);
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

	protected void AddStatusField(string title, string value)
	{
		RectTransform statusEntry = GameObject.Instantiate<RectTransform>(menuController.GetStatusFieldPrefab(), statusPanel);
		statusPanel.sizeDelta = statusPanel.sizeDelta + new Vector2(0.0f, statusEntry.sizeDelta.y);

		Text[] statusEntryTexts = statusEntry.GetComponentsInChildren<Text>();
		statusEntryTexts[0].text = title + ":";
		statusEntryTexts[1].text = value;

		statusEntries.Add(title, statusEntryTexts[1]);

		if(statusEntries.Count % 2 == 0)
		{
			statusEntry.GetComponentInChildren<Image>().enabled = false;
		}
	}

	protected void UpdateStatusField(string title, string value)
	{
		statusEntries[title].text = value;
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

	public virtual bool InstallComponent(string componentName)
	{
		GoodManager.ComponentData componentData = goodManager.GetComponentData(componentName);
		GoodManager.ComponentType componentType = componentData.type;
		if(componentSlots.ContainsKey(componentType))
		{
			if(!componentSlots[componentType].IsSet())
			{
				bool componentSwapSuccess = componentSlots[componentType].UpdateComponentData(componentName);

				if(moduleMenu != null)
				{
					UpdateComponentButtons();
				}

				if(componentSwapSuccess)
				{
					mass += componentData.mass;
					spacecraft.UpdateMass();
				}

				UpdateModuleStatus();
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

	public virtual bool RemoveComponent(GoodManager.ComponentType componentType)
	{
		if(componentSlots.ContainsKey(componentType))
		{
			if(componentSlots[componentType].IsSet())
			{
				string componentName = componentSlots[componentType].GetName();
				bool componentSwapSuccess = componentSlots[componentType].UpdateComponentData(null);

				if(moduleMenu != null)
				{
					UpdateComponentButtons();
				}

				if(componentSwapSuccess)
				{
					mass -= goodManager.GetComponentData(componentName).mass;
					spacecraft.UpdateMass();
				}

				UpdateModuleStatus();
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

	public List<GoodManager.Load> RemoveAllComponents()
	{
		List<GoodManager.Load> removedComponents = new List<GoodManager.Load>();
		foreach(GoodManager.ComponentType componentSlot in orderedComponentSlots)
		{
			if(componentSlots[componentSlot].IsSet())
			{
				string componentName = componentSlots[componentSlot].GetName();
				if(RemoveComponent(componentSlot))
				{
					removedComponents.Add(new GoodManager.Load(componentName, 1));
				}
				else
				{
					foreach(GoodManager.Load removedComponent in removedComponents)
					{
						InstallComponent(removedComponent.goodName);
					}

					return null;
				}
			}
		}

		return removedComponents;
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
				componentSlotEntryTexts[2].text = string.Empty;
			}
			else
			{
				string[] attributeStrings = componentSlots[componentType].GetAttributeList();
				componentSlotEntryTexts[0].text = componentSlots[componentType].GetName();
				componentSlotEntryTexts[1].text = attributeStrings[0];
				componentSlotEntryTexts[2].text = attributeStrings[1];
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

	// TODO: Call this when HP, Temp or Maintenance change
	public virtual void UpdateModuleStatus()
	{
		if(moduleMenu != null && moduleManager != null)
		{
			Text nameText = moduleMenuButton.GetComponentInChildren<Text>();

			float hpPercentage = (float)hp / (float)maxHp;

			StringBuilder moduleMenuButtonLabelString = new StringBuilder();
			bool bad = false;
			bool critical = false;
			if(customModuleName.Length <= moduleManager.GetMaxModuleMenuButtonCharacters())
			{
				moduleMenuButtonLabelString.Append(customModuleName);
			}
			else
			{
				moduleMenuButtonLabelString.Append(customModuleName.Substring(0, moduleManager.GetMaxModuleMenuButtonCharacters()));
				moduleMenuButtonLabelString.Append("...");
			}

			if(temperature >= moduleManager.GetIgnitionTemperature())
			{
				moduleMenuButtonLabelString.Append("\nTEMP CRIT");
				critical = true;
			}
			else if(temperature >= moduleManager.GetComfortableTemperature())
			{
				moduleMenuButtonLabelString.Append("\nOverheat");
				bad = true;
			}
			if(condition <= moduleManager.GetCriticalMaintenanceThreshold())
			{
				moduleMenuButtonLabelString.Append("\nCONDITION CRIT");
				critical = true;
			}
			else if(condition <= moduleManager.GetLowMaintenanceThreshold())
			{
				moduleMenuButtonLabelString.Append("\nBad Condition");
				bad = true;
			}
			if(hpPercentage <= moduleManager.GetCriticalHpThreshold())
			{
				moduleMenuButtonLabelString.Append("\nHP CRIT");
				critical = true;
			}
			else if(hpPercentage <= moduleManager.GetLowHpThreshold())
			{
				moduleMenuButtonLabelString.Append("\nLow HP");
				bad = true;
			}

			nameText.text = moduleMenuButtonLabelString.ToString();
			if(critical)
			{
				nameText.color = moduleManager.GetCriticalColor();
			}
			else if(bad)
			{
				nameText.color = moduleManager.GetBadColor();
			}
			else
			{
				nameText.color = moduleManager.GetNormalColor();
			}

			if(hpBar == null)
			{
				hpBar = moduleManager.InstantiateStatusBar("HP", moduleManager.GetHpColor(), hpPercentage, statusBarParent);
			}
			else
			{
				moduleManager.UpdateStatusBar(hpBar, hpPercentage);
			}

			UpdateStatusField("HP", (hp + "/" + maxHp));
			UpdateStatusField("Temperature", (Mathf.FloorToInt(temperature) + " K"));
			UpdateStatusField("Condition", (Mathf.FloorToInt(condition * 100.0f) + "%"));
		}
	}

	// Don't use ToggleController, since we only want to toggle 1 ModuleMenu, not all
	public virtual void ToggleModuleMenu()
	{
		moduleMenu.GetComponentInChildren<InputField>().text = customModuleName;
		UpdateComponentButtons();

		moduleMenu.SetActive(!moduleMenu.activeSelf);

		menuController.UpdateFlightControls();
	}

	public virtual void ComponentSlotClick(int componentSlotIndex, bool useTeleporter = true)
	{
		// TODO: Return if useTeleporter == false and no Engineers available
		// TODO: Take Time and Crew for Removal/Installation if useTeleporter == false

		GoodManager.ComponentType slotType = orderedComponentSlots[componentSlotIndex];

		if(componentSlots[slotType].IsSet())
		{
			string componentName = componentSlots[slotType].GetName();
			if(RemoveComponent(slotType))
			{
				if(inventoryController.Deposit(componentName, 1, (useTeleporter ? ((Vector2?) transform.position) : null)))
				{
					infoController.AddMessage("Successfully removed '" + componentName + "'!", false);
				}
				else
				{
					InstallComponent(componentName);
					infoController.AddMessage("Unable to remove '" + componentName + "', because there is no Space or Energy to store it!", true);
				}
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
			assembleString.Append("Assemble Component\n");
			bool first = true;
			int itemsOnLine = 0;
			foreach(GoodManager.Load cost in crudeComponentData.buildingCosts)
			{
				if(!first)
				{
					assembleString.Append(", ");
				}
				if(itemsOnLine >= 2)
				{
					assembleString.Append("\n");
					itemsOnLine = 0;
				}
				assembleString.Append(cost.amount);
				assembleString.Append(" ");
				assembleString.Append(cost.goodName);

				first = false;
				++itemsOnLine;
			}
			string[] attributeStrings = ModuleComponent.GetAttributeList(crudeComponentData);
			assembleComponentEntryTexts[0].text = assembleString.ToString();
			assembleComponentEntryTexts[1].text = attributeStrings[0];
			assembleComponentEntryTexts[2].text = attributeStrings[1];

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
				attributeStrings = ModuleComponent.GetAttributeList(componentData);
				componentSlotEntryTexts[0].text = componentData.goodName;
				componentSlotEntryTexts[1].text = attributeStrings[0];
				componentSlotEntryTexts[2].text = attributeStrings[1];

				int localComponentData = orderedComponentSlots.Count - 1;
				bool localUseTeleporter = useTeleporter;
				Transform localTransform = transform;
				componentSlotEntry.GetComponent<Button>().onClick.AddListener(delegate
					{
						if(inventoryController.Withdraw(componentData.goodName, 1, (localUseTeleporter ? ((Vector2?) localTransform.position) : null)))
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
		return (T)componentSlots[componentType];
	}

	public GoodManager.ComponentType GetComponentType(int componentSlotIndex)
	{
		return orderedComponentSlots[componentSlotIndex];
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
		UpdateModuleStatus();
	}
}
