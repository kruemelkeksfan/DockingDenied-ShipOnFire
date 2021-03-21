using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// TODO: What happens to attached Modules when link is severed? Maybe implement Shift LeftClick to make Selection and select the severed Modules automatically for easy Movement/Reattachment

public class Spacecraft : MonoBehaviour
{
	private enum ThrusterGroup
	{
		down,
		left,
		up,
		right,
		turnLeft,
		turnRight,
		all
	};

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

	[SerializeField] private Module[] essentialModules = null;
	[SerializeField] private Transform centerOfMassIndicator = null;
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
	private Dictionary<Vector2Int, Module> modules = null;
	private HashSet<Module> updateListeners = null;
	private HashSet<Module> fixedUpdateListeners = null;
	private new Transform transform = null;
	private new Rigidbody2D rigidbody = null;
	private HashSet<ThrusterModule>[] thrusters = null;
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
	private new Camera camera = null;
	private Transform cameraTransform = null;

	private void Awake()
	{
		modules = new Dictionary<Vector2Int, Module>();
		updateListeners = new HashSet<Module>();
		fixedUpdateListeners = new HashSet<Module>();
		transform = gameObject.GetComponent<Transform>();
		rigidbody = gameObject.GetComponentInChildren<Rigidbody2D>();

		thrusters = new HashSet<ThrusterModule>[Enum.GetValues(typeof(ThrusterGroup)).Length];
		for(int i = 0; i < thrusters.Length; ++i)
		{
			thrusters[i] = new HashSet<ThrusterModule>();
		}

		rigidbody.centerOfMass = Vector2.zero;
	}

	private void Start()
	{
		Vector2Int position = Vector2Int.zero;
		for(int i = 0; i < essentialModules.Length; ++i)
		{
			GameObject.Instantiate<Module>(essentialModules[i], transform).Build(position);
			position += Vector2Int.down;
		}

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
		foreach(Module module in updateListeners)
		{
			module.UpdateNotify();
		}

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

	private void FixedUpdate()
	{
		foreach(Module module in fixedUpdateListeners)
		{
			module.FixedUpdateNotify();
		}
	}

	public void SetThrottles(float horizontal, float vertical, float rotationSpeed)
	{
		HashSet<ThrusterModule> inactiveThrusters = new HashSet<ThrusterModule>(thrusters[(int)ThrusterGroup.all]);

		if(vertical > 0.0f)
		{
			SetThrusterGroupThrottle(ThrusterGroup.up, vertical, inactiveThrusters);
		}
		else if(vertical < 0.0f)
		{
			SetThrusterGroupThrottle(ThrusterGroup.down, -vertical, inactiveThrusters);
		}

		if(horizontal < 0.0f)
		{
			SetThrusterGroupThrottle(ThrusterGroup.left, -horizontal, inactiveThrusters);
		}
		else if(horizontal > 0.0f)
		{
			SetThrusterGroupThrottle(ThrusterGroup.right, horizontal, inactiveThrusters);
		}

		if(rotationSpeed < 0.0f)
		{
			SetThrusterGroupThrottle(ThrusterGroup.turnLeft, -rotationSpeed, inactiveThrusters);
		}
		else if(rotationSpeed > 0.0f)
		{
			SetThrusterGroupThrottle(ThrusterGroup.turnRight, rotationSpeed, inactiveThrusters);
		}

		foreach(ThrusterModule thruster in inactiveThrusters)
		{
			thruster.SetThrottle(0.0f);
		}
	}

	private void SetThrusterGroupThrottle(ThrusterGroup thrusterGroup, float throttle, HashSet<ThrusterModule> inactiveThrusters)
	{
		foreach(ThrusterModule thruster in thrusters[(int)thrusterGroup])
		{
			thruster.SetThrottle(throttle);
			inactiveThrusters.Remove(thruster);
		}
	}

	private bool PositionHasNeighbour(Vector2Int position)
	{
		foreach(Vector2Int direction in Directions.VECTORS)
		{
			Vector2Int neighbour = position + direction;
			if(modules.ContainsKey(neighbour) && (!modules[neighbour].GetFirstPositionNeighboursOnly() || neighbour == modules[neighbour].GetPosition()))
			{
				return true;
			}
		}

		return false;
	}

	public void UpdateModuleMass(Vector2 position, float massDifference)
	{
		if(rigidbody.mass < 0.0002f)        // Set Rigidbody Mass when updating for the first Time
		{
			rigidbody.centerOfMass = position;
			rigidbody.mass = massDifference;
		}
		else
		{
			rigidbody.mass += massDifference;
			rigidbody.centerOfMass += (position - rigidbody.centerOfMass) * (massDifference / (rigidbody.mass));
		}

		centerOfMassIndicator.localPosition = rigidbody.centerOfMass;
	}

	public bool PositionAvailable(Vector2Int position)
	{
		return !modules.ContainsKey(position) && PositionHasNeighbour(position);
	}

	public bool PositionsAvailable(Vector2Int[] positions, bool firstPositionNeighboursOnly)
	{
		bool neighbour = false;
		foreach(Vector2Int position in positions)
		{
			if(modules.ContainsKey(position))
			{
				return false;
			}

			if(!neighbour)
			{
				if(PositionHasNeighbour(position))
				{
					neighbour = true;
				}
				else if(firstPositionNeighboursOnly)
				{
					return false;
				}
			}
		}

		return neighbour;
	}

	public Module GetModule(Vector2Int position)
	{
		if(modules.ContainsKey(position))
		{
			return modules[position];
		}
		else
		{
			return null;
		}
	}

	public void AddModule(Vector2Int position, Module module)
	{
		modules[position] = module;
	}

	public bool RemoveModule(Vector2Int position)
	{
		return modules.Remove(position);
	}

	public void AddUpdateListener(Module module)
	{
		updateListeners.Add(module);
	}

	public void RemoveUpdateListener(Module module)
	{
		updateListeners.Remove(module);
	}

	public void AddFixedUpdateListener(Module module)
	{
		fixedUpdateListeners.Add(module);
	}

	public void RemoveFixedUpdateListener(Module module)
	{
		fixedUpdateListeners.Remove(module);
	}

	// Call this for all Thrusters when building the Ship
	public void AddThruster(ThrusterModule thruster)
	{
		thrusters[(int)ThrusterGroup.all].Add(thruster);

		// Add linear Thrust Group
		Vector2 thrust = thruster.GetThrustVector();
		if(thrust.x < -0.0002f)
		{
			thrusters[(int)ThrusterGroup.left].Add(thruster);
		}
		if(thrust.x > 0.0002f)
		{
			thrusters[(int)ThrusterGroup.right].Add(thruster);
		}
		if(thrust.y < -0.0002f)
		{
			thrusters[(int)ThrusterGroup.down].Add(thruster);
		}
		if(thrust.y > 0.0002f)
		{
			thrusters[(int)ThrusterGroup.up].Add(thruster);
		}

		// Add rotational Thrust Group
		// M = r x F
		// M - Torque
		// r - Lever
		// F - Thrust
		Vector2 lever = (Vector2)thruster.transform.localPosition - rigidbody.centerOfMass;
		float torque = Vector3.Cross(lever, thrust).z;
		if(torque < -0.0002f)
		{
			thrusters[(int)ThrusterGroup.turnLeft].Add(thruster);
		}
		else if(torque > 0.0002f)
		{
			thrusters[(int)ThrusterGroup.turnRight].Add(thruster);
		}
	}

	public void RemoveThruster(ThrusterModule thruster)
	{
		foreach(HashSet<ThrusterModule> thrusterGroup in thrusters)
		{
			thrusterGroup.Remove(thruster);
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
		if( name == null ||  name == "")
		{
			 name = "X" + DateTime.Now.ToString("ddMMyyyyHHmmss");
		}

		SpacecraftBlueprintController.SaveBlueprint(blueprintFolder,  name, modules);
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
			List<Vector2Int> moduleKeys = new List<Vector2Int>(modules.Keys);
			foreach(Vector2Int position in moduleKeys)
			{
				if(modules.ContainsKey(position) && modules[position].GetPosition() == position)
				{
					modules[position].Deconstruct();
				}
			}
			SpacecraftBlueprintController.LoadBlueprint(selectedBlueprintPath, modulePrefabDictionary, transform);

			selectedBlueprintPath = null;

			ToggleBlueprintConfirmationPanel();
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
		return ((Vector2)position) * buildingGridSize;
	}
}
