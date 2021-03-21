using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.IO;

// TODO: What happens to attached Modules when link is severed? Maybe implement Shift LeftClick to make Selection and select the severed Modules automatically for easy Movement/Reattachment

public class BuildingController : MonoBehaviour
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

	[SerializeField] private float maxPlacementDistance = 1.0f;
	[SerializeField] private float buildingGridSize = 1.0f;
	[SerializeField] private Button moduleButtonPrefab = null;
	[SerializeField] private Button blueprintButtonPrefab = null;
	[SerializeField] private Transform buildingMenu = null;
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
	private float inverseBuildingGridSize = 1.0f;
	private Vector2 buildingGridSizeVector = Vector2.one;
	private int rotation = Directions.UP;
	private CurrentModule currentModule = new CurrentModule(-1, null);
	private List<Transform> reservedZoneTransforms = null;
	private List<MeshRenderer> reservedZoneRenderers = null;
	private int activeReservedZones = 0;
	private string selectedBlueprintPath = null;
	private Dictionary<string, Module> modulePrefabDictionary = null;
	private Spacecraft spacecraft = null;
	private new Transform transform = null;
	private new Camera camera = null;
	private Transform cameraTransform = null;

	private void Start()
	{
		for(int i = 0; i < modulePrefabs.Length; ++i)
		{
			Button moduleButton = GameObject.Instantiate<Button>(moduleButtonPrefab, buildingMenu);
			RectTransform moduleButtonRectTransform = moduleButton.GetComponent<RectTransform>();
			moduleButtonRectTransform.anchoredPosition =
				new Vector3(moduleButtonRectTransform.anchoredPosition.x, -(moduleButtonRectTransform.rect.height * 0.5f + moduleButtonRectTransform.rect.height * i));
			moduleButton.GetComponentInChildren<Text>().text = modulePrefabs[i].GetModuleName();
			int localI = i;
			moduleButton.onClick.AddListener(delegate
			{
				SelectModule(localI);                                                                         // Seems to pass-by-reference
			});                      
		}

		inverseBuildingGridSize = 1.0f / buildingGridSize;
		buildingGridSizeVector = new Vector2(buildingGridSize, buildingGridSize);
		reservedZoneTransforms = new List<Transform>();
		reservedZoneRenderers = new List<MeshRenderer>();

		modulePrefabDictionary = new Dictionary<string, Module>(modulePrefabs.Length);
		foreach(Module module in modulePrefabs)
		{
			modulePrefabDictionary[module.GetModuleName()] = module;
		}

		spacecraft = gameObject.GetComponent<Spacecraft>();
		transform = gameObject.GetComponent<Transform>();
		camera = Camera.main;
		cameraTransform = camera.GetComponent<Transform>();
	}

	private void Update()
	{
		if(buildingMenu.gameObject.activeSelf)
		{
			if(currentModule.index >= 0)
			{
				currentModule.transform.position = camera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -cameraTransform.position.z));
				Vector2Int gridPosition = (Vector2Int)Vector3Int.RoundToInt(currentModule.transform.localPosition * inverseBuildingGridSize);
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
				Vector2Int gridPosition = (Vector2Int)Vector3Int.RoundToInt(
					transform.InverseTransformPoint(camera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -cameraTransform.position.z))) * inverseBuildingGridSize);
				Module module = spacecraft.GetModule(gridPosition);
				if(module != null && module.GetModuleName() != "Command Module")
				{
					module.Deconstruct();
				}
			}
		}
	}

	public void ToggleBuildingMenu()
	{
		RefreshBlueprintList();
		buildingMenu.gameObject.SetActive(!buildingMenu.gameObject.activeSelf);
		blueprintMenu.gameObject.SetActive(!blueprintMenu.gameObject.activeSelf);
		SelectModule(-1);
	}

	private void RefreshBlueprintList()
	{
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
			GameObject.Destroy(currentModule.module.gameObject, 0.02f);
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
		if(blueprintNameField.text == null || blueprintNameField.text == "")
		{
			blueprintNameField.text = "X" + DateTime.Now.ToString("ddMMyyyyHHmmss");
		}

		spacecraft.SaveBlueprint(blueprintFolder, blueprintNameField.text);
		RefreshBlueprintList();
		saveConfirmationPanel.SetActive(false);
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
			spacecraft.LoadBlueprint(selectedBlueprintPath, modulePrefabDictionary);
			selectedBlueprintPath = null;
		}
	}

	private void SpawnModule(int moduleIndex)
	{
		currentModule = new CurrentModule(moduleIndex, GameObject.Instantiate<Module>(modulePrefabs[moduleIndex], transform));
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

	public Vector3 IntToLocalPosition(Vector2Int position)
	{
		return ((Vector2) position) * buildingGridSize;
	}
}
