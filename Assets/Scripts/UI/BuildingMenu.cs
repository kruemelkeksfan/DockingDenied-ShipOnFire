﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BuildingMenu : MonoBehaviour, IUpdateListener, IListener
{
	private struct CurrentModule
	{
		public bool buildable;
		public int index;
		public Module module;
		public Transform transform;
		public Vector3 scale;

		public CurrentModule(int index, Module module)
		{
			buildable = false;
			this.index = index;
			this.module = module;

			if(module != null)
			{
				transform = module.transform;
				scale = transform.localScale;
			}
			else
			{
				transform = null;
				scale = Vector3.one;
			}
		}
	}

	private static BuildingMenu instance = null;

	[SerializeField] private float buildingGridSize = 1.0f;
	[SerializeField] private Button blueprintButtonPrefab = null;
	[SerializeField] private GameObject blueprintMenu = null;
	[SerializeField] private InputField blueprintNameField = null;
	[SerializeField] private RectTransform blueprintContentPane = null;
	[SerializeField] private GameObject blueprintLoadPanel = null;
	[SerializeField] private Module[] modulePrefabs = null;
	[SerializeField] private MeshRenderer reservedZonePrefab = null;
	[SerializeField] private Material zoneValidMaterial = null;
	[SerializeField] private Material zoneInvalidMaterial = null;
	[SerializeField] private string blueprintFolder = "Blueprints";
	[SerializeField] private TextAsset starterShip = null;
	[SerializeField] private Text cheaterModeText = null;
	[SerializeField] private GameObject assembleComponentConfirmationPanel = null;
	[SerializeField] private Text assembleComponentCostText = null;
	[SerializeField] private GameObject assembleComponentButton = null;
	private TimeController timeController = null;
	private GoodManager goodManager = null;
	private SpacecraftManager spacecraftManager = null;
	private MenuController menuController = null;
	private InfoController infoController = null;
	private float inverseBuildingGridSize = 1.0f;
	private Vector2 buildingGridSizeVector = Vector2.one;
	private SpacecraftController localPlayerMainSpacecraft = null;
	private Transform localPlayerMainSpacecraftTransform = null;
	private new Camera camera = null;
	private Vector3 lastMousePosition = Vector3.zero;
	private Vector2Int lastGridPosition = Vector2Int.zero;
	private Plane buildingPlane = new Plane(Vector3.back, 0.0f);
	private int rotation = Directions.UP;
	private CurrentModule currentModule = new CurrentModule(-1, null);
	private Vector2Int[] reservedZones = null;
	private List<Transform> reservedZoneTransforms = null;
	private List<MeshRenderer> reservedZoneRenderers = null;
	private int activeReservedZones = 0;
	private bool erase = false;
	private SpacecraftBlueprintController.SpacecraftData selectedBlueprintData = new SpacecraftBlueprintController.SpacecraftData();
	private GoodManager.Load[] selectedBlueprintCosts = null;
	private Dictionary<string, Module> modulePrefabDictionary = null;
	private bool cheaterMode = false;

	public static BuildingMenu GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		inverseBuildingGridSize = 1.0f / buildingGridSize;
		buildingGridSizeVector = new Vector2(buildingGridSize - 0.002f, buildingGridSize - 0.002f);
		reservedZones = new Vector2Int[0];
		reservedZoneTransforms = new List<Transform>();
		reservedZoneRenderers = new List<MeshRenderer>();

		modulePrefabDictionary = new Dictionary<string, Module>(modulePrefabs.Length);
		foreach(Module module in modulePrefabs)
		{
			modulePrefabDictionary[module.GetModuleName()] = module;
		}

		instance = this;
	}

	private void Start()
	{
		timeController = TimeController.GetInstance();
		goodManager = GoodManager.GetInstance();
		menuController = MenuController.GetInstance();
		infoController = InfoController.GetInstance();
		camera = Camera.main;

		spacecraftManager = SpacecraftManager.GetInstance();
		spacecraftManager.AddSpacecraftChangeListener(this);
		Notify();

		// Add one Reserve Zone for Erase Highlighting
		reservedZoneRenderers.Add(GameObject.Instantiate<MeshRenderer>(reservedZonePrefab, localPlayerMainSpacecraftTransform));
		reservedZoneTransforms.Add(reservedZoneRenderers[0].GetComponent<Transform>());
		reservedZoneTransforms[0].gameObject.SetActive(false);

		gameObject.SetActive(false);
		blueprintMenu.gameObject.SetActive(false);
		infoController.SetShowBuildingResourceDisplay(false);

		timeController.AddUpdateListener(this);
	}

	private void OnDestroy()
	{
		timeController?.RemoveUpdateListener(this);
	}

	public void Notify()
	{
		localPlayerMainSpacecraft = spacecraftManager.GetLocalPlayerMainSpacecraft();
		localPlayerMainSpacecraftTransform = localPlayerMainSpacecraft.GetTransform();
	}

	public void UpdateNotify()
	{
		Vector2Int gridPosition = lastGridPosition;
		if(Input.mousePosition != lastMousePosition)
		{
			Ray lookDirection = camera.ScreenPointToRay(Input.mousePosition);
			float enter;
			if(buildingPlane.Raycast(lookDirection, out enter))
			{
				gridPosition = WorldToGridPosition(lookDirection.GetPoint(enter));
			}
		}
		lastMousePosition = Input.mousePosition;
		lastGridPosition = gridPosition;

		if(currentModule.index >= 0)
		{
			currentModule.transform.localPosition = GridToLocalPosition(gridPosition);

			if(Input.GetButtonUp("Rotate Left"))
			{
				rotation = ((rotation - 1) + 4) % 4;
				currentModule.module.Rotate(rotation);
			}
			if(Input.GetButtonUp("Rotate Right"))
			{
				rotation = (rotation + 1) % 4;
				currentModule.module.Rotate(rotation);
			}
			if(Input.GetButtonUp("Place Module") && currentModule.buildable && !EventSystem.current.IsPointerOverGameObject())
			{
				Constructor constructor = null;
				if(cheaterMode || (constructor = FindBuildingConstructor(currentModule.transform.position, currentModule.module.GetBuildingCosts())) != null)
				{
					// Colliders should not get bigger during Time Speedup, since they are only checked during on-railing
					if(timeController.IsScaled())
					{
						infoController.AddMessage("Building is not allowed during Time Speedup!", true);
						timeController.SetTimeScale(0);
					}

					currentModule.module.Build(gridPosition);
					currentModule.transform.localScale = currentModule.scale;

					constructor?.StartConstruction(currentModule.transform.position);

					SpawnModule(currentModule.index);
				}
			}

			reservedZones = currentModule.module.GetReservedPositions(gridPosition, currentModule.transform.localRotation);
		}
		else if(erase && Input.GetButtonUp("Place Module") && !EventSystem.current.IsPointerOverGameObject())
		{
			Module module = localPlayerMainSpacecraft.GetModule(gridPosition);
			if(module != null)
			{
				if(module.GetModuleName() != "Command Module")
				{
					List<GoodManager.Load> removedComponents = module.RemoveAllComponents();
					if(removedComponents != null)
					{
						Vector3 position = module.GetTransform().position;
						Constructor constructor = null;
						List<GoodManager.Load> deconstructionMaterials = new List<GoodManager.Load>(module.GetBuildingCosts());
						deconstructionMaterials.AddRange(removedComponents);
						if(cheaterMode || (constructor = FindDeconstructionConstructor(position, deconstructionMaterials.ToArray(), localPlayerMainSpacecraft)) != null)
						{
							constructor?.StartConstruction(position);

							module.Deconstruct();
						}
						else
						{
							foreach(GoodManager.Load removedComponent in removedComponents)
							{
								module.InstallComponent(removedComponent.goodName);
							}
						}
					}
					else
					{
						infoController.AddMessage("Can't deconstruct Module, because its Components could not be removed!", true);
					}
				}
				else
				{
					infoController.AddMessage("Can't deconstruct Command Module, surely nobody wants to see the Crews Bodies bust open like Piñatas in Space!", true);
				}
			}
		}

		UpdateReservedZone();
	}

	public void ToggleBuildingMenu()
	{
		gameObject.SetActive(!gameObject.activeSelf);
		blueprintMenu.gameObject.SetActive(gameObject.activeSelf);
		infoController.SetShowBuildingResourceDisplay(gameObject.activeSelf);

		menuController.UpdateFlightControls();

		if(gameObject.activeSelf)
		{
			RefreshBlueprintList();
		}
		else
		{
			SelectModule(-1);
			erase = false;
		}
	}

	public void ToggleCheaterMode()
	{
		cheaterMode = !cheaterMode;

		if(cheaterMode)
		{
			cheaterModeText.text = cheaterModeText.text.Replace("Enable", "Disable");
		}
		else
		{
			cheaterModeText.text = cheaterModeText.text.Replace("Disable", "Enable");
		}
	}

	public void SelectModule(int moduleIndex)
	{
		DeselectBlueprint();

		if(currentModule.index >= 0 && currentModule.module != null)
		{
			GameObject.Destroy(currentModule.module.gameObject);
		}

		reservedZones = new Vector2Int[0];
		for(int i = 0; i < activeReservedZones; ++i)
		{
			reservedZoneTransforms[i].gameObject.SetActive(false);
		}
		activeReservedZones = 0;

		if(moduleIndex >= 0 && moduleIndex != currentModule.index)
		{
			erase = false;
			SpawnModule(moduleIndex);
			infoController.SetBuildingCosts(currentModule.module);
		}
		else
		{
			currentModule.index = -1;
			infoController.SetBuildingCosts(null);
		}
	}

	public bool DeselectModule()
	{
		if(currentModule.index >= 0 || erase)
		{
			SelectModule(-1);
			erase = false;
			return true;
		}
		else
		{
			return false;
		}
	}

	private void SpawnModule(int moduleIndex)
	{
		currentModule = new CurrentModule(moduleIndex, GameObject.Instantiate<Module>(modulePrefabs[moduleIndex], localPlayerMainSpacecraftTransform));
		currentModule.transform.localScale *= 1.02f;
		currentModule.module.Rotate(rotation);
	}

	public void ToggleErase()
	{
		SelectModule(-1);
		erase = !erase;
	}

	private void RefreshBlueprintList()
	{
		for(int i = 0; i < blueprintContentPane.childCount; ++i)
		{
			GameObject.Destroy(blueprintContentPane.GetChild(i).gameObject);
		}

		Button blueprintButton;
		RectTransform blueprintButtonRectTransform;
		string[] blueprintPaths = SpacecraftBlueprintController.GetBlueprintPaths(blueprintFolder);
		for(int i = 0; i < blueprintPaths.Length; ++i)
		{
			blueprintButton = GameObject.Instantiate<Button>(blueprintButtonPrefab, blueprintContentPane);
			blueprintButtonRectTransform = blueprintButton.GetComponent<RectTransform>();

			int startIndex = blueprintPaths[i].LastIndexOf(Path.DirectorySeparatorChar) + 1;
			int endIndex = blueprintPaths[i].LastIndexOf(".");
			blueprintButton.GetComponentInChildren<Text>().text = blueprintPaths[i].Substring(startIndex, endIndex - startIndex);
			string localBlueprintPath = blueprintPaths[i];
			blueprintButton.onClick.AddListener(delegate
			{
				SelectBlueprint(localBlueprintPath);
			});
		}

		blueprintButton = GameObject.Instantiate<Button>(blueprintButtonPrefab, blueprintContentPane);
		blueprintButtonRectTransform = blueprintButton.GetComponent<RectTransform>();

		blueprintButton.GetComponentInChildren<Text>().text = "Starter Ship";
		TextAsset localBlueprint = starterShip;
		blueprintButton.onClick.AddListener(delegate
		{
			SelectBlueprint(localBlueprint);
		});
	}

	public void SaveBlueprint()
	{
		string name = Regex.Replace(blueprintNameField.text, "[^a-zA-Z0-9_ ]{1}", "_");
		if(name == null || name == "")
		{
			name = "X" + DateTime.Now.ToString("ddMMyyyyHHmmss");
		}

		SpacecraftBlueprintController.SaveBlueprint(blueprintFolder, name, localPlayerMainSpacecraft.GetModules());
		RefreshBlueprintList();
		ToggleController.GetInstance().Toggle("SaveBlueprint");
	}

	public void SelectBlueprint(string blueprintPath)
	{
		selectedBlueprintData = SpacecraftBlueprintController.LoadBlueprintModules(blueprintPath);
		SelectBlueprint();
	}

	public void SelectBlueprint(TextAsset blueprint)
	{
		selectedBlueprintData = SpacecraftBlueprintController.LoadBlueprintModules(blueprint);
		SelectBlueprint();
	}

	private void SelectBlueprint()
	{
		if(selectedBlueprintData.moduleData.Count <= 0)
		{
			return;
		}

		DeselectModule();

		selectedBlueprintCosts = SpacecraftBlueprintController.CalculateBlueprintCosts(selectedBlueprintData);
		float moduleMass = 0.0f;
		foreach(GoodManager.Load cost in selectedBlueprintCosts)
		{
			moduleMass += goodManager.GetGood(cost.goodName).mass * cost.amount;
		}
		infoController.SetBuildingCosts(selectedBlueprintCosts, moduleMass);
		blueprintLoadPanel.SetActive(true);

		List<Vector2Int> reservedZoneList = new List<Vector2Int>(64);
		foreach(SpacecraftBlueprintController.ModuleData moduleData in selectedBlueprintData.moduleData)
		{
			reservedZoneList.AddRange(
				modulePrefabDictionary[moduleData.type].GetReservedPositions(
				moduleData.position, Quaternion.Euler(0.0f, 0.0f, moduleData.rotation)));
		}
		reservedZones = reservedZoneList.ToArray();
	}

	public void DeselectBlueprint()
	{
		selectedBlueprintData = new SpacecraftBlueprintController.SpacecraftData();
		selectedBlueprintCosts = null;
		infoController.SetBuildingCosts(null);
		blueprintLoadPanel.SetActive(false);

		reservedZones = new Vector2Int[0];
	}

	public void ConfirmBlueprint()
	{
		if(localPlayerMainSpacecraft.GetModuleCount() <= 1)
		{
			foreach(SpacecraftBlueprintController.ModuleData moduleData in selectedBlueprintData.moduleData)
			{
				if(!CheckBuildingSpaceFree(reservedZones, modulePrefabDictionary[moduleData.type], localPlayerMainSpacecraftTransform.rotation, true, true))
				{
					InfoController.GetInstance().AddMessage("Not enough free Building Space to construct Blueprint!", true);
					return;
				}
			}

			Constructor constructor = null;
			if(cheaterMode || (constructor = FindBuildingConstructor(localPlayerMainSpacecraftTransform.position, selectedBlueprintCosts)) != null)
			{
				// Colliders should not get bigger during Time Speedup, since they are only checked during on-railing
				if(timeController.IsScaled())
				{
					infoController.AddMessage("Blueprint Loading is not allowed during Time Speedup!", true);
					timeController.SetTimeScale(0);
				}

				constructor?.StartConstruction(localPlayerMainSpacecraftTransform.position);

				SpacecraftBlueprintController.InstantiateModules(selectedBlueprintData, localPlayerMainSpacecraftTransform);
				DeselectBlueprint();
			}
		}
		else
		{
			InfoController.GetInstance().AddMessage("Unable to instantiate Blueprint, deconstruct old Modules first!", true);
		}
	}

	public void ToggleComponentAssemblyConfirmation()
	{
		if(!assembleComponentConfirmationPanel.activeSelf)
		{
			StringBuilder costString = new StringBuilder();
			costString.Append("Material Costs: ");
			bool first = true;
			foreach(GoodManager.Load cost in localPlayerMainSpacecraft.CalculateComponentFillCosts())
			{
				if(!first)
				{
					costString.Append(", ");
				}
				costString.Append(cost.amount);
				costString.Append(" ");
				costString.Append(cost.goodName);

				first = false;
			}

			if(!first)
			{
				assembleComponentCostText.text = costString.ToString();
				assembleComponentButton.SetActive(true);
			}
			else
			{
				assembleComponentCostText.text = "No Components missing";
				assembleComponentButton.SetActive(false);
			}
		}

		assembleComponentConfirmationPanel.SetActive(!assembleComponentConfirmationPanel.activeSelf);
	}

	public void AssembleCrudeComponents()
	{
		GoodManager.Load[] componentCosts = localPlayerMainSpacecraft.CalculateComponentFillCosts();
		Constructor constructor = null;
		if(cheaterMode || (constructor = FindBuildingConstructor(localPlayerMainSpacecraftTransform.position, componentCosts)) != null)
		{
			constructor?.StartConstruction(localPlayerMainSpacecraftTransform.position);

			localPlayerMainSpacecraft.FillComponents();
		}
	}

	private void UpdateReservedZone()
	{
		if(erase)
		{
			for(int i = 0; i < activeReservedZones; ++i)
			{
				if(i == 0)
				{
					if(i >= reservedZoneTransforms.Count)
					{
						reservedZoneRenderers.Add(GameObject.Instantiate<MeshRenderer>(reservedZonePrefab, localPlayerMainSpacecraftTransform));
						reservedZoneTransforms.Add(reservedZoneRenderers[i].GetComponent<Transform>());
					}
					else
					{
						reservedZoneTransforms[i].gameObject.SetActive(true);
					}

					reservedZoneTransforms[i].localPosition = GridToLocalPosition(lastGridPosition) + new Vector3(0.0f, 0.0f, reservedZoneTransforms[0].localPosition.z);
				}
				else
				{
					reservedZoneTransforms[i].gameObject.SetActive(false);
				}
			}
			activeReservedZones = 1;
			reservedZoneRenderers[0].material = zoneInvalidMaterial;
		}
		else
		{
			for(int i = 0; i < reservedZones.Length || i < activeReservedZones; ++i)
			{
				if(i < reservedZones.Length)
				{
					if(i >= reservedZoneTransforms.Count)
					{
						reservedZoneRenderers.Add(GameObject.Instantiate<MeshRenderer>(reservedZonePrefab, localPlayerMainSpacecraftTransform));
						reservedZoneTransforms.Add(reservedZoneRenderers[i].GetComponent<Transform>());
					}
					else
					{
						reservedZoneTransforms[i].gameObject.SetActive(true);
					}

					reservedZoneTransforms[i].localPosition = GridToLocalPosition(reservedZones[i]) + new Vector3(0.0f, 0.0f, reservedZoneTransforms[i].localPosition.z);
				}
				else if(i < activeReservedZones)
				{
					reservedZoneTransforms[i].gameObject.SetActive(false);
				}
			}
			activeReservedZones = reservedZones.Length;

			bool buildable = true;
			if(currentModule.index >= 0)
			{
				buildable = CheckBuildingSpaceFree(reservedZones, currentModule.module, currentModule.transform.rotation);
				currentModule.buildable = buildable;
			}
			else if(selectedBlueprintData.moduleData != null && selectedBlueprintData.moduleData.Count > 0)
			{
				foreach(SpacecraftBlueprintController.ModuleData moduleData in selectedBlueprintData.moduleData)
				{
					if(!CheckBuildingSpaceFree(reservedZones, modulePrefabDictionary[moduleData.type], localPlayerMainSpacecraftTransform.rotation, true, true))
					{
						buildable = false;
						break;
					}
				}
			}
			if(buildable)
			{
				for(int i = 0; i < activeReservedZones; ++i)
				{
					reservedZoneRenderers[i].material = zoneValidMaterial;
				}
			}
			else
			{
				for(int i = 0; i < activeReservedZones; ++i)
				{
					reservedZoneRenderers[i].material = zoneInvalidMaterial;
				}
			}
		}
	}

	private bool CheckBuildingSpaceFree(Vector2Int[] reservedPositions, Module module, Quaternion moduleRotation, bool ignoreCommandModule = false, bool ignoreAttachmentPoints = false)
	{
		if(!localPlayerMainSpacecraft.PositionsAvailable(reservedPositions, module.HasAttachableReservePositions(), module.HasOverlappingReservePositions(), ignoreCommandModule, ignoreAttachmentPoints))
		{
			return false;
		}

		foreach(Vector2Int reservedPosition in reservedPositions)
		{
			Collider2D overlap;
			if((overlap = Physics2D.OverlapBox(GridToWorldPosition(reservedPosition), buildingGridSizeVector, moduleRotation.eulerAngles.z)) != null && !overlap.isTrigger && overlap.gameObject != localPlayerMainSpacecraft.gameObject)
			{
				return false;
			}
		}

		return true;
	}

	public Constructor FindBuildingConstructor(Vector2 position, GoodManager.Load[] materials)
	{
		bool constructorInRange = false;
		int errorCode = 0;
		foreach(Constructor constructor in spacecraftManager.GetConstructorsNearPosition(position))
		{
			if(constructor.PositionInRange(position))
			{
				constructorInRange = true;
				SpaceStationController spaceStationController = constructor.GetSpaceStationController();
				if(spaceStationController != null)
				{
					if((errorCode = constructor.TryConstruction(position, materials)) == 0 && spaceStationController.BuyConstructionMaterials(materials))
					{
						return constructor;
					}
				}
				else if((errorCode = constructor.TryConstruction(position, materials)) == 0 && constructor.GetInventoryController().WithdrawBulk(materials))
				{
					return constructor;
				}

				if(errorCode == 0)
				{
					constructor.RollbackConstruction();
				}
			}
		}

		if(!constructorInRange)
		{
			infoController.AddMessage("There is no Constructor in Range!", true);
		}
		else if(errorCode == 1)
		{
			infoController.AddMessage("Command Module is missing a Teleporter!", true);
		}
		else if(errorCode == 2)
		{
			infoController.AddMessage("Constructor Module is missing a Capacitor!", true);
		}
		else if(errorCode == 3)
		{
			infoController.AddMessage("Constructor Module is missing a Construction Unit!", true);
		}
		else if(errorCode == 4)
		{
			infoController.AddMessage("Not enough Energy available in Teleporter Capacitor in Command Module!", true);
		}
		else if(errorCode == 5)
		{
			infoController.AddMessage("Not enough Energy available for Construction!", true);
		}
		else if(errorCode == 0)
		{
			infoController.AddMessage("Not enough Building Materials available!", true);
		}
		else
		{
			infoController.AddMessage("An unknown Error ocurred during Construction!", true);
			Debug.LogWarning("Unknown Error " + constructorInRange + " " + errorCode + " ocurred during Construction!");
		}

		return null;
	}

	public Constructor FindDeconstructionConstructor(Vector2 position, GoodManager.Load[] materials, SpacecraftController deconstructingSpacecraft)
	{
		bool constructorInRange = false;
		int errorCode = 0;
		foreach(Constructor constructor in spacecraftManager.GetConstructorsNearPosition(position))
		{
			if(constructor.GetSpacecraft() != deconstructingSpacecraft && constructor.PositionInRange(position))
			{
				constructorInRange = true;
				SpaceStationController spaceStationController = constructor.GetSpaceStationController();
				if(spaceStationController != null)
				{
					if((errorCode = constructor.TryConstruction(position, materials)) == 0 && spaceStationController.SellDeconstructionMaterials(materials))
					{
						return constructor;
					}
				}
				else if((errorCode = constructor.TryConstruction(position, materials)) == 0 && constructor.GetInventoryController().DepositBulk(materials))
				{
					return constructor;
				}
				
				if(errorCode == 0)
				{
					constructor.RollbackConstruction();
				}
			}
		}

		if(!constructorInRange)
		{
			infoController.AddMessage("There is no Constructor in Range! Ships may not disassemble themselves!", true);
		}
		else if(errorCode == 1)
		{
			infoController.AddMessage("Command Module is missing a Teleporter!", true);
		}
		else if(errorCode == 2)
		{
			infoController.AddMessage("Constructor Module is missing a Capacitor!", true);
		}
		else if(errorCode == 3)
		{
			infoController.AddMessage("Constructor Module is missing a Construction Unit!", true);
		}
		else if(errorCode == 4)
		{
			infoController.AddMessage("Not enough Energy available in Teleporter Capacitor in Command Module!", true);
		}
		else if(errorCode == 5)
		{
			infoController.AddMessage("Not enough Energy available for Deconstruction!", true);
		}
		else if(errorCode == 0)
		{
			infoController.AddMessage("Deconstruction Materials could not be sold or stored!", true);
		}
		else
		{
			infoController.AddMessage("An unknown Error ocurred during Deconstruction!", true);
			Debug.LogWarning("Unknown Error " + constructorInRange + " " + errorCode + " ocurred during Deconstruction!");
		}

		return null;
	}

	public Vector2Int WorldToGridPosition(Vector3 position)
	{
		return Vector2Int.RoundToInt(localPlayerMainSpacecraftTransform.InverseTransformPoint(position) * inverseBuildingGridSize);
	}

	public Vector2Int LocalToGridPosition(Vector3 position)
	{
		return Vector2Int.RoundToInt(position * inverseBuildingGridSize);
	}

	public Vector3 GridToWorldPosition(Vector2Int position)
	{
		return localPlayerMainSpacecraftTransform.TransformPoint(((Vector2)position) * buildingGridSize);
	}

	public Vector3 GridToLocalPosition(Vector2Int position)
	{
		return ((Vector2)position) * buildingGridSize;
	}

	public float GetGridSize()
	{
		return buildingGridSize;
	}

	public Dictionary<string, Module> GetModulePrefabDictionary()
	{
		return modulePrefabDictionary;
	}
}
