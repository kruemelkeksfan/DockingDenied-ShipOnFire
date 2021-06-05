using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BuildingMenu : MonoBehaviour
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

	[SerializeField] private GameObject buildingResourceDisplay = null;
	[SerializeField] private float buildingGridSize = 1.0f;
	[SerializeField] private Button moduleButtonPrefab = null;
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
	[SerializeField] private Spacecraft spacecraft = null;              // TODO: Get this from SpacecraftManager
	[SerializeField] private Text cheaterModeText = null;
	private MenuController menuController = null;
	private SpacecraftManager spacecraftManager = null;
	private InfoController infoController = null;
	private float inverseBuildingGridSize = 1.0f;
	private Vector2 buildingGridSizeVector = Vector2.one;
	private Vector3 lastLookPoint = Vector3.zero;
	private Vector2Int lastGridPosition = Vector2Int.zero;
	private Plane buildingPlane = new Plane(Vector3.back, 0.0f);
	private int rotation = Directions.UP;
	private CurrentModule currentModule = new CurrentModule(-1, null);
	private List<Transform> reservedZoneTransforms = null;
	private List<MeshRenderer> reservedZoneRenderers = null;
	private int activeReservedZones = 0;
	private bool erase = false;
	private string selectedBlueprintPath = null;
	private GoodManager.Load[] selectedBlueprintCosts = null;
	private Dictionary<string, Module> modulePrefabDictionary = null;
	private Transform spacecraftTransform = null;
	private new Camera camera = null;
	private Transform cameraTransform = null;
	private bool cheaterMode = false;

	public static BuildingMenu GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		inverseBuildingGridSize = 1.0f / buildingGridSize;
		buildingGridSizeVector = new Vector2(buildingGridSize - 0.002f, buildingGridSize - 0.002f);
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
		Transform transform = GetComponent<Transform>();
		for(int i = 1; i < modulePrefabs.Length; ++i)                                                                   // Skip Command Module
		{
			Button moduleButton = GameObject.Instantiate<Button>(moduleButtonPrefab, transform);
			RectTransform moduleButtonRectTransform = moduleButton.GetComponent<RectTransform>();
			moduleButtonRectTransform.anchoredPosition =
				new Vector3(moduleButtonRectTransform.anchoredPosition.x, -(moduleButtonRectTransform.rect.height * 0.5f + moduleButtonRectTransform.rect.height * (i - 1)));
			moduleButton.GetComponentInChildren<Text>().text = modulePrefabs[i].GetModuleName();
			int localI = i;
			moduleButton.onClick.AddListener(delegate
				{
					SelectModule(localI);                                                                               // Seems to pass-by-reference
				});
		}

		menuController = MenuController.GetInstance();
		spacecraftManager = SpacecraftManager.GetInstance();
		infoController = InfoController.GetInstance();
		spacecraftTransform = spacecraft.GetTransform();
		camera = Camera.main;
		cameraTransform = camera.GetComponent<Transform>();

		reservedZoneRenderers.Add(GameObject.Instantiate<MeshRenderer>(reservedZonePrefab, spacecraftTransform));       // Add one Reserve Zone for Erase Highlighting
		reservedZoneTransforms.Add(reservedZoneRenderers[0].GetComponent<Transform>());
		reservedZoneTransforms[0].gameObject.SetActive(false);

		gameObject.SetActive(false);
		buildingResourceDisplay.SetActive(false);
		blueprintMenu.gameObject.SetActive(false);
	}

	private void Update()
	{
		Vector3 lookPoint = new Vector3(Input.mousePosition.x, Input.mousePosition.y, -cameraTransform.position.z);
		Vector2Int gridPosition = lastGridPosition;
		if(lookPoint != lastLookPoint)
		{
			Ray lookDirection = camera.ScreenPointToRay(lookPoint);
			float enter;
			if(buildingPlane.Raycast(lookDirection, out enter))
			{
				gridPosition = WorldToGridPosition(lookDirection.GetPoint(enter));
			}
		}
		lastLookPoint = lookPoint;
		lastGridPosition = gridPosition;

		if(currentModule.index >= 0)
		{
			currentModule.transform.localPosition = GridToLocalPosition(gridPosition);

			Vector2Int[] reservedPositions = currentModule.module.GetReservedPositions(gridPosition);
			for(int i = 0; i < reservedPositions.Length || i < activeReservedZones; ++i)
			{
				if(i < reservedPositions.Length)
				{
					if(i >= reservedZoneTransforms.Count)
					{
						reservedZoneRenderers.Add(GameObject.Instantiate<MeshRenderer>(reservedZonePrefab, spacecraftTransform));
						reservedZoneTransforms.Add(reservedZoneRenderers[i].GetComponent<Transform>());
					}
					else
					{
						reservedZoneTransforms[i].gameObject.SetActive(true);
					}

					reservedZoneTransforms[i].localPosition = GridToLocalPosition(reservedPositions[i]) + new Vector3(0.0f, 0.0f, reservedZoneTransforms[i].localPosition.z);
				}
				else if(i < activeReservedZones)
				{
					reservedZoneTransforms[i].gameObject.SetActive(false);
				}
			}
			activeReservedZones = reservedPositions.Length;

			Collider2D overlap;
			if(spacecraft.PositionsAvailable(reservedPositions, currentModule.module.HasAttachableReservePositions(), currentModule.module.HasOverlappingReservePositions())
				&& ((overlap = Physics2D.OverlapBox(currentModule.transform.position, buildingGridSizeVector, currentModule.transform.rotation.eulerAngles.z)) == null || overlap.isTrigger || overlap.gameObject == spacecraft.gameObject))
			{
				currentModule.buildable = true;
				for(int i = 0; i < activeReservedZones; ++i)
				{
					reservedZoneRenderers[i].material = zoneValidMaterial;
				}
			}
			else
			{
				currentModule.buildable = false;
				for(int i = 0; i < activeReservedZones; ++i)
				{
					reservedZoneRenderers[i].material = zoneInvalidMaterial;
				}
			}

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
					currentModule.module.Build(gridPosition);
					currentModule.transform.localScale = currentModule.scale;

					constructor?.StartConstruction(currentModule.transform.position);

					SpawnModule(currentModule.index);
				}
			}
		}
		else if(erase)
		{
			reservedZoneTransforms[0].localPosition = GridToLocalPosition(gridPosition) + new Vector3(0.0f, 0.0f, reservedZoneTransforms[0].localPosition.z);
			if(Input.GetButtonUp("Place Module") && !EventSystem.current.IsPointerOverGameObject())
			{
				Module module = spacecraft.GetModule(gridPosition);
				if(module != null && module.GetModuleName() != "Command Module")
				{
					Vector3 position = module.GetTransform().position;
					Constructor constructor = null;
					if(cheaterMode || (constructor = FindDeconstructionConstructor(position, module.GetBuildingCosts(), spacecraft)) != null)
					{
						constructor?.StartConstruction(position);

						module.Deconstruct();
					}
				}
			}
		}
		else
		{
			if(Input.GetButtonUp("Place Module") && !EventSystem.current.IsPointerOverGameObject())
			{
				Module module = spacecraft.GetModule(gridPosition);
				if(module != null)
				{
					HotkeyModule hotkeyModule = module as HotkeyModule;
					if(hotkeyModule != null)
					{
						menuController.ToggleModuleMenu(hotkeyModule);
					}
				}
			}
		}
	}

	public void ToggleBuildingMenu()
	{
		gameObject.SetActive(!gameObject.activeSelf);
		buildingResourceDisplay.SetActive(gameObject.activeSelf);
		blueprintMenu.gameObject.SetActive(gameObject.activeSelf);

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
		menuController.CloseModuleMenu();

		if(currentModule.index >= 0 && currentModule.module != null)
		{
			GameObject.Destroy(currentModule.module.gameObject);
		}

		if(moduleIndex >= 0 && moduleIndex != currentModule.index)
		{
			erase = false;
			SpawnModule(moduleIndex);
			infoController.SetBuildingCosts(currentModule.module.GetBuildingCosts());
			infoController.AddMessage(currentModule.module.GetDescription());
		}
		else
		{
			currentModule.index = -1;
			for(int i = 0; i < activeReservedZones; ++i)
			{
				reservedZoneTransforms[i].gameObject.SetActive(false);
			}
			activeReservedZones = 0;
			infoController.SetBuildingCosts(null);
		}
	}

	public bool DeselectModule()
	{
		if(currentModule.index >= 0)
		{
			SelectModule(-1);
			return true;
		}
		else
		{
			return false;
		}
	}

	private void SpawnModule(int moduleIndex)
	{
		currentModule = new CurrentModule(moduleIndex, GameObject.Instantiate<Module>(modulePrefabs[moduleIndex], spacecraftTransform));
		currentModule.transform.localScale *= 1.02f;
		currentModule.module.Rotate(rotation);
	}

	public void ToggleErase()
	{
		SelectModule(-1);
		erase = !erase;

		if(erase)
		{
			reservedZoneRenderers[0].material = zoneInvalidMaterial;
			reservedZoneTransforms[0].gameObject.SetActive(true);
		}
		else
		{
			reservedZoneTransforms[0].gameObject.SetActive(false);
		}
	}

	private void RefreshBlueprintList()
	{
		for(int i = 0; i < blueprintContentPane.childCount; ++i)
		{
			GameObject.Destroy(blueprintContentPane.GetChild(i).gameObject);
		}

		string[] blueprintPaths = SpacecraftBlueprintController.GetBlueprintPaths(blueprintFolder);
		for(int i = 0; i < blueprintPaths.Length; ++i)
		{
			Button blueprintButton = GameObject.Instantiate<Button>(blueprintButtonPrefab, blueprintContentPane);
			RectTransform blueprintButtonRectTransform = blueprintButton.GetComponent<RectTransform>();

			int startIndex = blueprintPaths[i].LastIndexOf(Path.DirectorySeparatorChar) + 1;
			int endIndex = blueprintPaths[i].LastIndexOf(".");
			blueprintButton.GetComponentInChildren<Text>().text = blueprintPaths[i].Substring(startIndex, endIndex - startIndex);
			string localBlueprintPath = blueprintPaths[i];
			blueprintButton.onClick.AddListener(delegate
			{
				SelectBlueprint(localBlueprintPath);
			});
		}
	}

	public void SaveBlueprint()
	{
		string name = blueprintNameField.text;
		if(name == null || name == "")
		{
			name = "X" + DateTime.Now.ToString("ddMMyyyyHHmmss");
		}

		SpacecraftBlueprintController.SaveBlueprint(blueprintFolder, name, spacecraft.GetModules());
		RefreshBlueprintList();
		ToggleController.GetInstance().Toggle("SaveBlueprint");
	}

	public void SelectBlueprint(string blueprintPath)
	{
		selectedBlueprintPath = blueprintPath;
		selectedBlueprintCosts = SpacecraftBlueprintController.CalculateBlueprintCosts(blueprintPath);
		infoController.SetBuildingCosts(selectedBlueprintCosts);
		blueprintLoadPanel.SetActive(true);
	}

	public void DeselectBlueprint()
	{
		selectedBlueprintPath = null;
		selectedBlueprintCosts = null;
		infoController.SetBuildingCosts(null);
		blueprintLoadPanel.SetActive(false);
	}

	public void ConfirmBlueprint()
	{
		if(spacecraft.GetModules().Count <= 1)
		{
			if(cheaterMode || FindBuildingConstructor(spacecraftTransform.position, selectedBlueprintCosts) != null)
			{
				SpacecraftBlueprintController.LoadBlueprint(selectedBlueprintPath, spacecraftTransform);
				DeselectBlueprint();
			}
		}
		else
		{
			InfoController.GetInstance().AddMessage("Unable to instantiate Blueprint, deconstruct old Modules first!");
			return;
		}
	}

	private Constructor FindBuildingConstructor(Vector2 position, GoodManager.Load[] materials)
	{
		foreach(Constructor constructor in spacecraftManager.GetConstructorsNearPosition(position))
		{
			if(constructor.PositionInRange(position))
			{
				SpaceStationController spaceStationController = constructor.GetSpaceStationController();
				if(spaceStationController != null)
				{
					if(spaceStationController.BuyConstructionMaterials(materials))
					{
						return constructor;
					}
				}
				else if(constructor.GetInventoryController().WithdrawBulk(materials))
				{
					return constructor;
				}
			}
		}

		infoController.AddMessage("Either there is no Constructor in Range or no Construction Materials could be provided!");

		return null;
	}

	private Constructor FindDeconstructionConstructor(Vector2 position, GoodManager.Load[] materials, Spacecraft deconstructingSpacecraft)
	{
		foreach(Constructor constructor in spacecraftManager.GetConstructorsNearPosition(position))
		{
			if(constructor.GetSpacecraft() != deconstructingSpacecraft && constructor.PositionInRange(position))
			{
				SpaceStationController spaceStationController = constructor.GetSpaceStationController();
				if(spaceStationController != null && spaceStationController.SellDeconstructionMaterials(materials))
				{
					return constructor;
				}
				else if(constructor.GetInventoryController().DepositBulk(materials))
				{
					return constructor;
				}
			}
		}

		infoController.AddMessage("Either there is no Constructor in Range or the Materials could not be sold or stored! Ships may not disassemble themselves!");

		return null;
	}

	public Vector2Int WorldToGridPosition(Vector3 position)
	{
		return Vector2Int.RoundToInt(spacecraftTransform.InverseTransformPoint(position) * inverseBuildingGridSize);
	}

	public Vector2Int LocalToGridPosition(Vector3 position)
	{
		return Vector2Int.RoundToInt(position * inverseBuildingGridSize);
	}

	public Vector3 GridToWorldPosition(Vector2Int position)
	{
		return spacecraftTransform.TransformPoint(((Vector2)position) * buildingGridSize);
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
