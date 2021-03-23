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
		public MeshRenderer meshRenderer;
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
				meshRenderer = module.GetComponentInChildren<MeshRenderer>();
				scale = transform.localScale;
			}
			else
			{
				transform = null;
				collider = null;
				meshRenderer = null;
				scale = Vector3.one;
			}
		}
	}

	private static BuildingMenu instance = null;

	[SerializeField] private float maxPlacementDistance = 1.0f;
	[SerializeField] private float buildingGridSize = 1.0f;
	[SerializeField] private Button moduleButtonPrefab = null;
	[SerializeField] private Button blueprintButtonPrefab = null;
	[SerializeField] private GameObject blueprintMenu = null;
	[SerializeField] private GameObject saveConfirmationPanel = null;
	[SerializeField] private Text blueprintNameField = null;
	[SerializeField] private RectTransform blueprintContentPane = null;
	[SerializeField] private GameObject loadConfirmationPanel = null;
	[SerializeField] private Module[] modulePrefabs = null;
	[SerializeField] private Material validMaterial = null;
	[SerializeField] private Material invalidMaterial = null;
	[SerializeField] private Material moduleMaterial = null;
	[SerializeField] private MeshRenderer reservedZonePrefab = null;
	[SerializeField] private Material zoneValidMaterial = null;
	[SerializeField] private Material zoneInvalidMaterial = null;
	[SerializeField] private string blueprintFolder = "Blueprints";
	[SerializeField] private Spacecraft spacecraft = null;
	private float inverseBuildingGridSize = 1.0f;
	private Vector2 buildingGridSizeVector = Vector2.one;
	private int rotation = Directions.UP;
	private CurrentModule currentModule = new CurrentModule(-1, null);
	private List<Transform> reservedZoneTransforms = null;
	private List<MeshRenderer> reservedZoneRenderers = null;
	private int activeReservedZones = 0;
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
		buildingGridSizeVector = new Vector2(buildingGridSize, buildingGridSize);
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
		for(int i = 1; i < modulePrefabs.Length; ++i)																// Skip Command Module
		{
			Button moduleButton = GameObject.Instantiate<Button>(moduleButtonPrefab, transform);
			RectTransform moduleButtonRectTransform = moduleButton.GetComponent<RectTransform>();
			moduleButtonRectTransform.anchoredPosition =
				new Vector3(moduleButtonRectTransform.anchoredPosition.x, -(moduleButtonRectTransform.rect.height * 0.5f + moduleButtonRectTransform.rect.height * (i - 1)));
			moduleButton.GetComponentInChildren<Text>().text = modulePrefabs[i].GetModuleName();
			int localI = i;
			moduleButton.onClick.AddListener(delegate
				{
					SelectModule(localI);                                                                           // Seems to pass-by-reference
				});
		}

		spacecraftTransform = spacecraft.GetTransform();
		camera = Camera.main;
		cameraTransform = camera.GetComponent<Transform>();

		blueprintMenu.gameObject.SetActive(false);
		gameObject.SetActive(false);
	}

	private void Update()
	{
			if(currentModule.index >= 0)
			{
				currentModule.transform.position = camera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -cameraTransform.position.z));
				Vector2Int gridPosition = LocalToGridPosition(currentModule.transform.localPosition);
				currentModule.transform.localPosition = (Vector2)gridPosition * buildingGridSize;

				Vector2Int[] reservedPositions = currentModule.module.GetReservedPositions(gridPosition);
				for(int i = 0; i < reservedPositions.Length || i < activeReservedZones; ++i)
				{
					if(i < reservedPositions.Length)
					{
						if(i >= reservedZoneTransforms.Count)
						{
							reservedZoneRenderers.Add(GameObject.Instantiate<MeshRenderer>(reservedZonePrefab, transform));
							reservedZoneTransforms.Add(reservedZoneRenderers[i].GetComponent<Transform>());
						}
						else
						{
							reservedZoneTransforms[i].gameObject.SetActive(true);
						}

						reservedZoneTransforms[i].localPosition = (Vector2)reservedPositions[i] * buildingGridSize;
					}
					else if(i < activeReservedZones)
					{
						reservedZoneTransforms[i].gameObject.SetActive(false);
					}
				}
				activeReservedZones = reservedPositions.Length;

				if(spacecraft.PositionsAvailable(reservedPositions, currentModule.module.GetFirstPositionNeighboursOnly())
					&& Physics2D.OverlapBox(currentModule.transform.position, buildingGridSizeVector, currentModule.transform.rotation.eulerAngles.z) == null)
				{
					currentModule.buildable = true;
					currentModule.meshRenderer.material = validMaterial;
					for(int i = 0; i < activeReservedZones; ++i)
					{
						reservedZoneRenderers[i].material = zoneValidMaterial;
					}
				}
				else
				{
					currentModule.buildable = false;
					currentModule.meshRenderer.material = invalidMaterial;
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
					currentModule.meshRenderer.material = moduleMaterial;
					currentModule.transform.localScale = currentModule.scale;

					SpawnModule(currentModule.index);
				}
			}

			if(Input.GetButtonUp("Remove Module") && !EventSystem.current.IsPointerOverGameObject())
			{
				Module module = spacecraft.GetModule(WorldToGridPosition(camera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -cameraTransform.position.z))));
				if(module != null && module.GetModuleName() != "Command Module")
				{
					module.Deconstruct();
				}
			}
	}

	public void ToggleBuildingMenu()
	{
		RefreshBlueprintList();
		gameObject.SetActive(!gameObject.activeSelf);
		blueprintMenu.gameObject.SetActive(!blueprintMenu.gameObject.activeSelf);
		SelectModule(-1);
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

	public void SelectModule(int moduleIndex)
	{
		if(currentModule.index >= 0 && currentModule.module != null)
		{
			GameObject.Destroy(currentModule.module.gameObject);
		}

		if(moduleIndex >= 0 && moduleIndex != currentModule.index)
		{
			SpawnModule(moduleIndex);
		}
		else
		{
			UnselectModule();
		}
	}

	public void ToggleBlueprintSavePanel()
	{
		saveConfirmationPanel.SetActive(!saveConfirmationPanel.activeSelf);
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
		ToggleBlueprintSavePanel();
	}

	public void ToggleBlueprintConfirmationPanel()
	{
		loadConfirmationPanel.SetActive(!loadConfirmationPanel.activeSelf);
	}
	public void SelectBlueprint(string blueprintPath)
	{
		selectedBlueprintPath = blueprintPath;
		ToggleBlueprintConfirmationPanel();
	}

	public void ConfirmBlueprint()
	{
		if(selectedBlueprintPath != null)
		{
			Dictionary<Vector2Int, Module> modules = spacecraft.GetModules();
			List<Vector2Int> moduleKeys = new List<Vector2Int>(modules.Keys);
			foreach(Vector2Int position in moduleKeys)
			{
				if(modules.ContainsKey(position) && modules[position].GetPosition() == position)
				{
					modules[position].Deconstruct();
				}
			}
			SpacecraftBlueprintController.LoadBlueprint(selectedBlueprintPath, modulePrefabDictionary, spacecraftTransform);

			selectedBlueprintPath = null;

			ToggleBlueprintConfirmationPanel();
		}
	}

	private void SpawnModule(int moduleIndex)
	{
		currentModule = new CurrentModule(moduleIndex, GameObject.Instantiate<Module>(modulePrefabs[moduleIndex], spacecraftTransform));
		currentModule.transform.localScale *= 1.02f;
		currentModule.module.Rotate(rotation);
		currentModule.collider.enabled = false;
		currentModule.meshRenderer.material = validMaterial;
	}

	private void UnselectModule()
	{
		currentModule.index = -1;
		for(int i = 0; i < activeReservedZones; ++i)
		{
			reservedZoneTransforms[i].gameObject.SetActive(false);
		}
		activeReservedZones = 0;
	}

	public Vector2Int WorldToGridPosition(Vector3 position)
	{
		return Vector2Int.RoundToInt(transform.InverseTransformPoint(position) * inverseBuildingGridSize);
	}

	public Vector2Int LocalToGridPosition(Vector3 position)
	{
		return Vector2Int.RoundToInt(position * inverseBuildingGridSize);
	}

	public Vector3 GridToLocalPosition(Vector2Int position)
	{
		return ((Vector2)position) * buildingGridSize;
	}
}
