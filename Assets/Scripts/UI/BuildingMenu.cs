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
		public Collider2D collider;
		public Vector3 scale;

		public CurrentModule(int index, Module module)
		{
			buildable = false;
			this.index = index;
			this.module = module;

			if(module != null)
			{
				transform = module.transform;
				collider = module.GetComponentInChildren<Collider2D>();
				scale = transform.localScale;
			}
			else
			{
				transform = null;
				collider = null;
				scale = Vector3.one;
			}
		}
	}

	private static BuildingMenu instance = null;

	[SerializeField] private float buildingGridSize = 1.0f;
	[SerializeField] private Button moduleButtonPrefab = null;
	[SerializeField] private Button blueprintButtonPrefab = null;
	[SerializeField] private GameObject blueprintMenu = null;
	[SerializeField] private Text blueprintNameField = null;
	[SerializeField] private RectTransform blueprintContentPane = null;
	[SerializeField] private Module[] modulePrefabs = null;
	[SerializeField] private MeshRenderer reservedZonePrefab = null;
	[SerializeField] private Material zoneValidMaterial = null;
	[SerializeField] private Material zoneInvalidMaterial = null;
	[SerializeField] private string blueprintFolder = "Blueprints";
	[SerializeField] private Spacecraft spacecraft = null;
	private float inverseBuildingGridSize = 1.0f;
	private Vector2 buildingGridSizeVector = Vector2.one;
	private Plane buildingPlane = new Plane(Vector3.back, 0.0f);
	private int rotation = Directions.UP;
	private CurrentModule currentModule = new CurrentModule(-1, null);
	private List<Transform> reservedZoneTransforms = null;
	private List<MeshRenderer> reservedZoneRenderers = null;
	private int activeReservedZones = 0;
	private bool erase = false;
	private HotkeyModule activeModuleSettings = null;
	private string selectedBlueprintPath = null;
	private Dictionary<string, Module> modulePrefabDictionary = null;
	private Transform spacecraftTransform = null;
	private new Camera camera = null;
	private Transform cameraTransform = null;

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

		spacecraftTransform = spacecraft.GetTransform();
		camera = Camera.main;
		cameraTransform = camera.GetComponent<Transform>();

		reservedZoneRenderers.Add(GameObject.Instantiate<MeshRenderer>(reservedZonePrefab, spacecraftTransform));       // Add one Reserve Zone for Erase Highlighting
		reservedZoneTransforms.Add(reservedZoneRenderers[0].GetComponent<Transform>());
		reservedZoneTransforms[0].gameObject.SetActive(false);

		blueprintMenu.gameObject.SetActive(false);
		gameObject.SetActive(false);
	}

	private void Update()
	{
		Ray lookDirection = camera.ScreenPointToRay(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -cameraTransform.position.z));
		float enter;
		if(buildingPlane.Raycast(lookDirection, out enter))
		{
			Vector2Int gridPosition = LocalToGridPosition(spacecraftTransform.InverseTransformPoint(lookDirection.GetPoint(enter)));

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

				if(spacecraft.PositionsAvailable(reservedPositions, currentModule.module.HasAttachableReservePositions(), currentModule.module.HasOverlappingReservePositions())
					&& Physics2D.OverlapBox(currentModule.transform.position, buildingGridSizeVector, currentModule.transform.rotation.eulerAngles.z) == null)
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
					currentModule.module.Build(gridPosition);
					currentModule.collider.enabled = true;
					currentModule.transform.localScale = currentModule.scale;

					SpawnModule(currentModule.index);
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
						module.Deconstruct();
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
						HotkeyModule moduleSettings = module as HotkeyModule;
						if(moduleSettings != null)
						{
							if(activeModuleSettings != null && moduleSettings != activeModuleSettings)
							{
								activeModuleSettings.ToggleModuleSettingMenu(true);
							}
							moduleSettings.ToggleModuleSettingMenu();
							activeModuleSettings = moduleSettings;
						}
					}
				}
			}
		}
	}

	public void ToggleBuildingMenu()
	{
		RefreshBlueprintList();
		gameObject.SetActive(!gameObject.activeSelf);
		blueprintMenu.gameObject.SetActive(!blueprintMenu.gameObject.activeSelf);
		SelectModule(-1);
		erase = false;
	}

	public void SelectModule(int moduleIndex)
	{
		if(activeModuleSettings != null)
		{
			activeModuleSettings.ToggleModuleSettingMenu(true);
		}

		if(currentModule.index >= 0 && currentModule.module != null)
		{
			GameObject.Destroy(currentModule.module.gameObject);
		}

		if(moduleIndex >= 0 && moduleIndex != currentModule.index)
		{
			erase = false;
			SpawnModule(moduleIndex);
		}
		else
		{
			currentModule.index = -1;
			for(int i = 0; i < activeReservedZones; ++i)
			{
				reservedZoneTransforms[i].gameObject.SetActive(false);
			}
			activeReservedZones = 0;
		}
	}
	private void SpawnModule(int moduleIndex)
	{
		currentModule = new CurrentModule(moduleIndex, GameObject.Instantiate<Module>(modulePrefabs[moduleIndex], spacecraftTransform));
		currentModule.transform.localScale *= 1.02f;
		currentModule.module.Rotate(rotation);
		currentModule.collider.enabled = false;
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
			blueprintButtonRectTransform.anchoredPosition =
				new Vector3(blueprintButtonRectTransform.anchoredPosition.x, -(blueprintButtonRectTransform.rect.height * 0.5f + blueprintButtonRectTransform.rect.height * i));
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
		ToggleController.GetInstance().ToggleGroup("SaveBlueprint");
	}

	public void SelectBlueprint(string blueprintPath)
	{
		selectedBlueprintPath = blueprintPath;
		ToggleController.GetInstance().ToggleGroup("LoadBlueprint");
	}

	public void ConfirmBlueprint()
	{
		if(selectedBlueprintPath != null)
		{
			SpacecraftBlueprintController.LoadBlueprint(selectedBlueprintPath, spacecraft, spacecraftTransform);
			selectedBlueprintPath = null;
			ToggleController.GetInstance().ToggleGroup("LoadBlueprint");
		}
	}

	public Vector2Int WorldToGridPosition(Vector3 position)
	{
		return Vector2Int.RoundToInt(spacecraftTransform.InverseTransformPoint(position) * inverseBuildingGridSize);
	}

	public Vector2Int LocalToGridPosition(Vector3 position)
	{
		return Vector2Int.RoundToInt(position * inverseBuildingGridSize);
	}

	public Vector3 GridToLocalPosition(Vector2Int position)
	{
		return ((Vector2)position) * buildingGridSize;
	}

	public Dictionary<string, Module> GetModulePrefabDictionary()
	{
		return modulePrefabDictionary;
	}
}
