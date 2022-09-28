using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// TODO: What happens to attached Modules when link is severed? Maybe implement Shift LeftClick to make Selection and select the severed Modules automatically for easy Movement/Reattachment

public class SpacecraftController : GravityObjectController, IUpdateListener, IFixedUpdateListener, IDockingListener
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

	[SerializeField] private CommandModule commandModulePrefab = null;
	[SerializeField] private Transform centerOfMassIndicator = null;
	[SerializeField] private Transform[] centerOfThrustIndicators = { };
	[SerializeField] private Transform foreignCenterOfMassIndicator = null;
	[Tooltip("Minimum Fraction of Force which must be exerted by a Thruster in a Direction to add it to the corresponding Direction Thruster Group")]
	[SerializeField] private float directionalForceThreshold = 0.2f;
	[Tooltip("Rotations with less than this minAngularVelocity will be stopped after a short Time")]
	[SerializeField] private float minAngularVelocity = 1.0f;
	[Tooltip("Time until a slow Rotation will be stopped, if the angularVelocity stays too low")]
	[SerializeField] private float rotationStopTime = 2.0f;
	[SerializeField] private AudioClip thrusterAudio = null;
	private GoodManager goodManager = null;
	private AudioController audioController = null;
	private HashSet<Module> modules = null;
	private Dictionary<Vector2Int, Module> modulesPositions = null;
	private BuildingMenu buildingMenu = null;
	private InventoryController inventoryController = null;
	private Teleporter teleporter = null;
	private float inertiaFactor = 1.0f;
	private HashSet<Thruster>[] thrusters = null;
	private HashSet<Thruster> inactiveThrusters = null;
	private bool calculateCollider = false;
	private float halfGridSize = 0.0f;
	private PolygonCollider2D spacecraftCollider = null;
	private Dictionary<SpacecraftController, Transform> dockedSpacecraft = null;
	private bool thrusting = false;
	private bool stoppingRotation = false;
	private bool rendererActive = true;
	private bool thrustAudioActive = false;

	protected override void Awake()
	{
		base.Awake();

		goodManager = GoodManager.GetInstance();

		modules = new HashSet<Module>();
		modulesPositions = new Dictionary<Vector2Int, Module>();
		inventoryController = gameObject.GetComponent<InventoryController>();

		thrusters = new HashSet<Thruster>[Enum.GetValues(typeof(ThrusterGroup)).Length];
		for(int i = 0; i < thrusters.Length; ++i)
		{
			thrusters[i] = new HashSet<Thruster>();
		}
		inactiveThrusters = new HashSet<Thruster>();

		dockedSpacecraft = new Dictionary<SpacecraftController, Transform>(2);

		rigidbody.centerOfMass = Vector2.zero;
	}

	protected override void Start()
	{
		base.Start();

		audioController = AudioController.GetInstance();
		buildingMenu = BuildingMenu.GetInstance();
		halfGridSize = buildingMenu.GetGridSize() * 0.5f;
		spacecraftCollider = GetComponent<PolygonCollider2D>();

		float moduleWidth = buildingMenu.GetGridSize();
		inertiaFactor = (1.0f / 6.0f) * (moduleWidth * moduleWidth);

		// If no Blueprint was loaded during Awake()
		if(modules.Count <= 0)
		{
			CommandModule commandModule = GameObject.Instantiate<CommandModule>(commandModulePrefab, transform);
			commandModule.Build(Vector2Int.zero);
			teleporter = commandModule.GetTeleporter();
		}
		else
		{
			teleporter = GetComponentInChildren<CommandModule>().GetTeleporter();
		}

		ToggleController toggleController = ToggleController.GetInstance();
		toggleController.AddToggleObject("COMIndicators", centerOfMassIndicator.gameObject);
		toggleController.AddToggleObject("COMIndicators", foreignCenterOfMassIndicator.gameObject);
		foreach(Transform centerOfThrustIndicator in centerOfThrustIndicators)
		{
			toggleController.AddToggleObject("COMIndicators", centerOfThrustIndicator.gameObject);
		}
		centerOfMassIndicator.gameObject.SetActive(toggleController.IsGroupToggled("COMIndicators"));
		foreignCenterOfMassIndicator.gameObject.SetActive(toggleController.IsGroupToggled("COMIndicators"));

		timeController.AddUpdateListener(this);
		timeController.AddFixedUpdateListener(this);
		gravityWellController.AddGravityObject(this);
	}

	private void OnDestroy()
	{
		if(rigidbody != null)
		{
			gravityWellController?.RemoveGravityObject(rigidbody);
		}

		ToggleController toggleController = ToggleController.GetInstance();
		toggleController?.RemoveToggleObject("COMIndicators", centerOfMassIndicator.gameObject);
		toggleController?.RemoveToggleObject("COMIndicators", foreignCenterOfMassIndicator.gameObject);
		foreach(Transform centerOfThrustIndicator in centerOfThrustIndicators)
		{
			toggleController?.RemoveToggleObject("COMIndicators", centerOfThrustIndicator.gameObject);
		}

		// Deconstruct all Modules to make sure that all Deconstructors are called properly
		DeconstructModules(false);

		timeController?.RemoveUpdateListener(this);
		timeController?.RemoveFixedUpdateListener(this);
	}

	public void UpdateNotify()
	{
		// Do this in Update instead of FixedUpdate(), since it might take some Time
		if(calculateCollider)
		{
			CalculateSpacecraftCollider();
			calculateCollider = false;
		}
	}

	public void FixedUpdateNotify()
	{
		if(!stoppingRotation && rigidbody.angularVelocity != 0.0f && Mathf.Abs(rigidbody.angularVelocity) < minAngularVelocity)
		{
			timeController.StartCoroutine(StopRotation(), false);
		}
	}

	public void Docked(DockingPort port, DockingPort otherPort)
	{
		SpacecraftController otherSpacecraft = otherPort.GetSpacecraft();
		if(!dockedSpacecraft.ContainsKey(otherSpacecraft))                                                                      // First Caller manages both Spacecraft
		{
			dockedSpacecraft.Add(otherSpacecraft, otherSpacecraft.transform);
			otherSpacecraft.dockedSpacecraft.Add(this, transform);

			// UpdateForeignMass() automatically updates the own and all docked Spacecraft
			// Delay Execution, because Spacecraft will move violently during the first Physics-Frames after establishing the Joint and the Indicator would be displaced
			timeController.StartCoroutine(UpdateForeignMassDelayed(), false);
		}
	}

	private IEnumerator<float> UpdateForeignMassDelayed()
	{
		yield return 0.5f;
		UpdateForeignMass();
	}

	public void Undocked(DockingPort port, DockingPort otherPort)
	{
		SpacecraftController otherSpacecraft = otherPort.GetSpacecraft();
		if(dockedSpacecraft.Remove(otherPort.GetSpacecraft()))                                                                  // First Caller manages both Spacecraft // TODO: What if attached via multiple Ports? (maybe check every other port in a foreach loop)
		{
			otherSpacecraft.dockedSpacecraft.Remove(this);

			otherSpacecraft.UpdateForeignMass();
			UpdateForeignMass();
		}
	}

	public void Kill()
	{
		// TODO: Switch to other Player Spacecraft if available
		if(this == SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft())
		{
			GameController.GetInstance().Restart("Game Over...just one more Round...");
		}
	}

	public void SetThrottles(float horizontal, float vertical, float rotationSpeed)
	{
		// inactiveThrusters is needed, because Thruster Particle System stops when throttle is set to 0
		inactiveThrusters.Clear();
		inactiveThrusters.UnionWith(thrusters[(int)ThrusterGroup.all]);

		thrusting = false;
		if(vertical > 0.0f)
		{
			if(SetThrusterGroupThrottle(ThrusterGroup.up, vertical, inactiveThrusters))
			{
				thrusting = true;
			}
		}
		if(vertical < 0.0f)
		{
			if(SetThrusterGroupThrottle(ThrusterGroup.down, -vertical, inactiveThrusters))
			{
				thrusting = true;
			}
		}

		if(horizontal < 0.0f)
		{
			if(SetThrusterGroupThrottle(ThrusterGroup.left, -horizontal, inactiveThrusters))
			{
				thrusting = true;
			}
		}
		if(horizontal > 0.0f)
		{
			if(SetThrusterGroupThrottle(ThrusterGroup.right, horizontal, inactiveThrusters))
			{
				thrusting = true;
			}
		}

		if(rotationSpeed < 0.0f)
		{
			if(SetThrusterGroupThrottle(ThrusterGroup.turnLeft, -rotationSpeed, inactiveThrusters))
			{
				thrusting = true;
			}
		}
		if(rotationSpeed > 0.0f)
		{
			if(SetThrusterGroupThrottle(ThrusterGroup.turnRight, rotationSpeed, inactiveThrusters))
			{
				thrusting = true;
			}
		}

		foreach(Thruster thruster in inactiveThrusters)
		{
			thruster.SetThrottle(0.0f);
		}

		if(!thrustAudioActive && thrusting)
		{
			audioController.LoopAudioStart(thrusterAudio, gameObject);
			thrustAudioActive = true;
		}

		if(!thrusting && thrustAudioActive)
		{
			audioController.LoopAudioStop(thrusterAudio, gameObject);
			thrustAudioActive = false;
		}
	}

	// TODO: Unity.Rigidbody Max Mass is 1000000, so assure that this is not exceeded (e.g. let Spacestations deny docking/undock after surpassing a Threshold to force the Player to dump Goods/deconstruct Modules)
	// TODO: Optimize by just doing the Weight Check and setting a Flag here and calculating the heavy Stuff at the End of the Frame/Beginning of the next Frame
	public void UpdateMass()
	{
		if(buildingMenu == null)
		{
			buildingMenu = BuildingMenu.GetInstance();
		}

		// Calculate inefficiently by looping through all Modules, because we need to loop through all Modules for Inertia Calculation anyways

		// Calculate Center of Mass
		Vector2 centerOfMass = Vector2.zero;
		float totalMass = 0.0f;
		foreach(KeyValuePair<Vector2Int, Module> moduleData in modulesPositions)
		{
			if(moduleData.Key == moduleData.Value.GetPosition() || !moduleData.Value.HasOverlappingReservePositions())
			{
				int massPositionCount = (moduleData.Value.HasOverlappingReservePositions() ? 1 : moduleData.Value.GetReservedPositionCount());
				float positionMass = moduleData.Value.GetMass() / massPositionCount;

				centerOfMass += (Vector2)buildingMenu.GridToLocalPosition(moduleData.Key) * positionMass;
				totalMass += positionMass;
			}
		}

		if(totalMass > MathUtil.EPSILON)
		{
			// Finish Center of Mass Calculation
			centerOfMass /= totalMass;

			// Calculate Moment of Inertia
			float inertia = 0.0f;
			foreach(KeyValuePair<Vector2Int, Module> moduleData in modulesPositions)
			{
				if(moduleData.Key == moduleData.Value.GetPosition() || !moduleData.Value.HasOverlappingReservePositions())
				{
					int massPositionCount = (moduleData.Value.HasOverlappingReservePositions() ? 1 : moduleData.Value.GetReservedPositionCount());
					float positionMass = moduleData.Value.GetMass() / massPositionCount;

					// Approximate Modules as Cubes and use Steiner's Theorem to calculate their Moment of Inertia around the Ships Center of Mass
					// https://en.wikipedia.org/wiki/List_of_moments_of_inertia
					// https://en.wikipedia.org/wiki/Parallel_axis_theorem
					inertia += (inertiaFactor + ((Vector2)buildingMenu.GridToLocalPosition(moduleData.Key) - centerOfMass).sqrMagnitude) * positionMass;
				}
			}

			// Apply Values
			rigidbody.centerOfMass = centerOfMass;
			rigidbody.mass = totalMass;

			centerOfMassIndicator.localPosition = rigidbody.centerOfMass;
			rigidbody.inertia = inertia;
		}
		else
		{
			// Use Makeshift Values for empty Spacecraft (happens for Example when loading new Blueprint)
			rigidbody.centerOfMass = Vector2.zero;
			rigidbody.mass = 1.0f;
			rigidbody.inertia = 1.0f;
		}

		// Update Center of Mass of docked Spacecraft
		UpdateForeignMass();

		// Recalculate Thruster Groups
		CalculateThrusterTurningGroups();
	}

	public void UpdateForeignMass()
	{
		// Check to avoid Division by Zero in totalMass Calculation
		if(dockedSpacecraft.Count > 0)
		{
			// GetRecursivelyDockedSpacecraft() includes this Spacecraft
			HashSet<SpacecraftController> recursivelyDockedSpacecraft = GetDockedSpacecraftRecursively();
			Vector2 globalCenterOfMass = Vector2.zero;
			float totalMass = 0.0f;
			foreach(SpacecraftController dockedSpacecraft in recursivelyDockedSpacecraft)
			{
				float spacecraftMass = dockedSpacecraft.rigidbody.mass;

				// Convert other Spacecrafts Center of Mass to World Coordinates and then to this Spacecrafts local Coordinates
				globalCenterOfMass += (Vector2)dockedSpacecraft.transform.TransformPoint(dockedSpacecraft.rigidbody.centerOfMass) * spacecraftMass;
				totalMass += spacecraftMass;
			}
			globalCenterOfMass /= totalMass;

			foreach(SpacecraftController dockedSpacecraft in recursivelyDockedSpacecraft)
			{
				dockedSpacecraft.foreignCenterOfMassIndicator.position = globalCenterOfMass;
			}
		}
		else
		{
			foreignCenterOfMassIndicator.localPosition = rigidbody.centerOfMass;
		}
	}

	public void UpdateCenterOfThrust()
	{
		for(int i = 0; i < 4; ++i)
		{
			Vector2 centerOfThrust = Vector2.zero;
			float totalThrust = 0.0f;
			foreach(Thruster thruster in thrusters[i])
			{
				float thrust = thruster.GetThrust();
				centerOfThrust += ((Vector2)thruster.GetTransform().localPosition) * thrust;
				totalThrust += thrust;
			}
			if(totalThrust > MathUtil.EPSILON)
			{
				centerOfThrust /= totalThrust;
			}

			centerOfThrustIndicators[i].localPosition = centerOfThrust;
		}
	}

	public bool PositionsAvailable(Vector2Int[] positions, bool HasAttachableReservePositions, bool HasOverlappingReservePositions, bool ignoreCommandModule = false, bool ignoreAttachmentPoints = false)
	{
		bool neighbour = ignoreAttachmentPoints;
		for(int i = 0; i < positions.Length; ++i)
		{
			if(modulesPositions.ContainsKey(positions[i])                                                                                                           // Position is already in Use
				&& (i == 0 || positions[i] == modulesPositions[positions[i]].GetPosition()                                                                          // Either Requester or current Position User have their Main Position on this Position
				|| !HasOverlappingReservePositions || !modulesPositions[positions[i]].HasOverlappingReservePositions())                                             // Either Requester or current Position User do not allow overlapping Reserve Positions
				&& !(ignoreCommandModule && modulesPositions[positions[i]].GetModuleName() == "Command Module"))
			{
				return false;
			}

			if(!neighbour)
			{
				foreach(Vector2Int direction in Directions.VECTORS)
				{
					Vector2Int neighbourPosition = positions[i] + direction;
					if(modulesPositions.ContainsKey(neighbourPosition)                                                                                               // Position has a Neighbour
						&& (i == 0 || HasAttachableReservePositions)                                                                                        // Requester either neighbours with his Main Position or allows attachable Reserve Positions
						&& (neighbourPosition == modulesPositions[neighbourPosition].GetPosition() || modulesPositions[neighbourPosition].HasAttachableReservePositions()))   // Neighbour either neighbours with his Main Position or allows attachable Reserve Positions
					{
						neighbour = true;
						break;
					}
				}

				if(!neighbour && !HasAttachableReservePositions)
				{
					return false;
				}
			}
		}

		return neighbour;
	}

	public void DeconstructModules(bool keepCommandModule)
	{
		List<Module> modules = new List<Module>(this.modules);          // Avoid concurrent Modification
		foreach(Module module in modules)
		{
			if(!keepCommandModule || module.GetModuleName() != "Command Module")
			{
				module.Deconstruct();
			}
		}
	}

	public GoodManager.Load[] CalculateComponentFillCosts()
	{
		Dictionary<string, uint> componentFillCostsDictionary = new Dictionary<string, uint>();

		foreach(Module module in modules)
		{
			foreach(GoodManager.ComponentType emptyComponentSlot in module.GetEmptyComponentSlots())
			{
				GoodManager.ComponentData componentData = goodManager.GetComponentData(goodManager.GetComponentName(emptyComponentSlot) + " [crude]");
				foreach(GoodManager.Load cost in componentData.buildingCosts)
				{
					if(componentFillCostsDictionary.ContainsKey(cost.goodName))
					{
						componentFillCostsDictionary[cost.goodName] += cost.amount;
					}
					else
					{
						componentFillCostsDictionary[cost.goodName] = cost.amount;
					}
				}
			}
		}

		GoodManager.Load[] componentFillCosts = new GoodManager.Load[componentFillCostsDictionary.Count];
		int i = 0;
		foreach(KeyValuePair<string, uint> cost in componentFillCostsDictionary)
		{
			componentFillCosts[i] = new GoodManager.Load(cost.Key, cost.Value);
			++i;
		}

		return componentFillCosts;
	}

	public void FillComponents(GoodManager.ComponentQuality quality = GoodManager.ComponentQuality.crude)
	{
		foreach(Module module in modules)
		{
			foreach(GoodManager.ComponentType emptyComponentSlot in module.GetEmptyComponentSlots())
			{
				module.InstallComponent(goodManager.GetComponentName(emptyComponentSlot) + " [" + quality.ToString() + "]");
			}
		}
	}

	private bool SetThrusterGroupThrottle(ThrusterGroup thrusterGroup, float throttle, HashSet<Thruster> inactiveThrusters)
	{
		bool thrusting = false;
		// Buffer in extra Variable to avoid Concurrent Modification when Fuel is being taken from Inventory and Center of Mass is recalculated
		HashSet<Thruster> thrusters = new HashSet<Thruster>(this.thrusters[(int)thrusterGroup]);
		foreach(Thruster thruster in thrusters)
		{
			if(thruster.SetThrottle(throttle))
			{
				thrusting = true;
			}
			inactiveThrusters.Remove(thruster);
		}
		return thrusting;
	}

	private void CalculateSpacecraftCollider()
	{
		if(modulesPositions.Count <= 0)
		{
			spacecraftCollider.SetPath(0, new Vector2[] { Vector2.zero });
			return;
		}

		// Find Maximum Y Value of this Spacecrafts Modules
		int maxY = 0;
		foreach(Vector2Int maxPosition in modulesPositions.Keys)
		{
			if(maxPosition.y > maxY)
			{
				maxY = maxPosition.y;
			}
		}

		// Go from Top Bounding Border of the Spacecraft downwards until you find the topmost Module at X = 0
		Vector2Int position = new Vector2Int(0, maxY);
		while(!modulesPositions.ContainsKey(position) || (position != modulesPositions[position].GetPosition() && modulesPositions[position].HasOverlappingReservePositions()))
		{
			position += Vector2Int.down;
		}

		// Set some Variables (helpful Comment tm)
		// We will go around the Vessel in clockwise Direction, starting at the Top, therefore the first scanDirection is Right
		List<Vector2> points = new List<Vector2>();
		int scanDirectionIndex = Directions.RIGHT;
		int borderNormalIndex = (scanDirectionIndex + 3) % 4;
		do
		{
			// Go along the Modules in scanDirection, until you are either not standing on a Module anymore or there is a solid Space in the Direction there previously was empty Space
			while(CheckBorderPosition(position + Directions.VECTORS[scanDirectionIndex], position + Directions.VECTORS[scanDirectionIndex] + Directions.VECTORS[borderNormalIndex]))
			{
				position += Directions.VECTORS[scanDirectionIndex];
			}

			// Add the last Corner which is known to border empty Space and a Module to points
			points.Add(buildingMenu.GridToLocalPosition(position) + (Vector3)(((Vector2)Directions.VECTORS[scanDirectionIndex] * halfGridSize) + ((Vector2)Directions.VECTORS[borderNormalIndex] * halfGridSize)));

			// Turn into a new Scan Direction, 90° from the previous Scan Direction, never forward (known to lead into the Vessels Interior or out into Space) or backward
			// Since we circle the Vessel clockwise, we will prefer to turn left to cover the whole Vessel
			if(CheckBorderPosition(position + Directions.VECTORS[scanDirectionIndex] + Directions.VECTORS[(scanDirectionIndex + 3) % 4], position + Directions.VECTORS[borderNormalIndex]))
			{
				position += Directions.VECTORS[scanDirectionIndex];
				scanDirectionIndex = (scanDirectionIndex + 3) % 4;
				borderNormalIndex = (borderNormalIndex + 3) % 4;
				position += Directions.VECTORS[scanDirectionIndex];
			}
			else if(CheckBorderPosition(position, position + Directions.VECTORS[(borderNormalIndex + 1) % 4]))
			{
				scanDirectionIndex = (scanDirectionIndex + 1) % 4;
				borderNormalIndex = (borderNormalIndex + 1) % 4;
			}
			else
			{
				Debug.LogError("Got stuck while Calculating Spacecraft Collider!");
				break;
			}
		}
		while(points.Count < 4 || points[0] != points[points.Count - 1]);

		// Remove last Point because it is identical with the first
		points.RemoveAt(points.Count - 1);

		spacecraftCollider.SetPath(0, points);
	}

	private bool CheckBorderPosition(Vector2Int position, Vector2Int borderingPosition)
	{
		return modulesPositions.ContainsKey(position) && (position == modulesPositions[position].GetPosition() || !modulesPositions[position].HasOverlappingReservePositions())
				&& (!modulesPositions.ContainsKey(borderingPosition) || (borderingPosition != modulesPositions[borderingPosition].GetPosition() && modulesPositions[borderingPosition].HasOverlappingReservePositions()));
	}

	private void CalculateThrusterTurningGroups()
	{
		thrusters[(int)ThrusterGroup.turnLeft].Clear();
		thrusters[(int)ThrusterGroup.turnRight].Clear();
		foreach(Thruster thruster in thrusters[(int)ThrusterGroup.all])
		{
			CalculateThrusterTurningGroup(thruster);
		}
	}

	private void CalculateThrusterTurningGroup(Thruster thruster)
	{
		// Add rotational Thrust Group
		// M = r x F
		// M - Torque
		// r - Lever
		// F - Thrust
		// Use normalized Values for lever and thrust, because we only check the Sign for torque and normalized Magnitudes for rotationFraction
		Vector2 lever = ((Vector2)thruster.transform.localPosition - rigidbody.centerOfMass);
		if(lever.sqrMagnitude > MathUtil.EPSILON)
		{
			lever.Normalize();
		}
		Vector2 thrust = thruster.GetThrustDirection();
		// To find the Fraction of Thrust used for Rotation, project (normalized) thrust on lever and subtract the Result from 100%
		float rotationFraction = 1.0f - Mathf.Abs(Vector2.Dot(lever, thrust));
		if(rotationFraction > directionalForceThreshold)
		{
			float torque = Vector3.Cross(lever, thrust).z;
			if(torque < 0.0f)
			{
				thrusters[(int)ThrusterGroup.turnLeft].Add(thruster);
			}
			else if(torque > 0.0f)
			{
				thrusters[(int)ThrusterGroup.turnRight].Add(thruster);
			}
		}
	}

	private IEnumerator<float> StopRotation()
	{
		stoppingRotation = true;

		double startTime = timeController.GetTime();
		do
		{
			yield return -1.0f;

			if(Mathf.Abs(rigidbody.angularVelocity) >= minAngularVelocity)
			{
				stoppingRotation = false;
				yield break;
			}
		}
		while(timeController.GetTime() - startTime < rotationStopTime);

		rigidbody.angularVelocity = 0.0f;
		stoppingRotation = false;
	}

	public override void ToggleRenderer(bool activateRenderer)
	{
		if(activateRenderer != rendererActive)
		{
			foreach(MeshRenderer renderer in gameObject.GetComponentsInChildren<MeshRenderer>())
			{
				renderer.enabled = activateRenderer;
			}

			rendererActive = activateRenderer;
		}
	}

	public bool IsDockedToStation()
	{
		foreach(KeyValuePair<SpacecraftController, Transform> dockedSpacecraft in dockedSpacecraft)
		{
			if(dockedSpacecraft.Key.GetComponent<SpaceStationController>() != null)
			{
				return true;
			}
		}

		return false;
	}

	public bool IsThrusting()
	{
		// Return true, if this Spacecraft is thrusting...
		if(thrusting)
		{
			return true;
		}

		// ...or if any connected Spacecraft is thrusting...
		foreach(SpacecraftController spacecraft in dockedSpacecraft.Keys)
		{
			if(spacecraft.thrusting)
			{
				return true;
			}
		}

		// ...or if nobody is thrusting, return false
		return false;
	}

	public Module GetModule(Vector2Int position)
	{
		if(modulesPositions.ContainsKey(position))
		{
			return modulesPositions[position];
		}
		else
		{
			return null;
		}
	}

	public int GetModuleCount()
	{
		return modules.Count;
	}

	public Dictionary<Vector2Int, Module> GetModules()
	{
		return modulesPositions;
	}

	public Teleporter GetTeleporter()
	{
		if(teleporter == null)
		{
			teleporter = GetComponentInChildren<CommandModule>().GetTeleporter();
		}

		return teleporter;
	}

	public int GetDockedSpacecraftCount()
	{
		return dockedSpacecraft.Count;
	}

	public HashSet<SpacecraftController> GetDockedSpacecraftRecursively(HashSet<SpacecraftController> recursivelyDockedSpacecraft = null)
	{
		if(recursivelyDockedSpacecraft == null)
		{
			recursivelyDockedSpacecraft = new HashSet<SpacecraftController>();
			recursivelyDockedSpacecraft.Add(this);
		}
		foreach(SpacecraftController dockedSpacecraft in dockedSpacecraft.Keys)
		{
			if(recursivelyDockedSpacecraft.Add(dockedSpacecraft))
			{
				dockedSpacecraft.GetDockedSpacecraftRecursively(recursivelyDockedSpacecraft);
			}
		}

		return recursivelyDockedSpacecraft;
	}

	public InventoryController GetInventoryController()
	{
		return inventoryController;
	}

	public void AddModule(Vector2Int position, Module module)
	{
		modules.Add(module);
		modulesPositions[position] = module;
		calculateCollider = true;
	}

	public void RemoveModule(Vector2Int position)
	{
		if(position == modulesPositions[position].GetPosition())
		{
			modules.Remove(modulesPositions[position]);
		}
		modulesPositions.Remove(position);
		calculateCollider = true;
	}

	// Call this for all Thrusters when building the Ship
	public void AddThruster(Thruster thruster)
	{
		thrusters[(int)ThrusterGroup.all].Add(thruster);

		// Add linear Thrust Group
		Vector2 thrust = thruster.GetThrustDirection();
		if(thrust.x < -directionalForceThreshold)
		{
			thrusters[(int)ThrusterGroup.left].Add(thruster);
		}
		if(thrust.x > directionalForceThreshold)
		{
			thrusters[(int)ThrusterGroup.right].Add(thruster);
		}
		if(thrust.y < -directionalForceThreshold)
		{
			thrusters[(int)ThrusterGroup.down].Add(thruster);
		}
		if(thrust.y > directionalForceThreshold)
		{
			thrusters[(int)ThrusterGroup.up].Add(thruster);
		}

		CalculateThrusterTurningGroup(thruster);
	}

	public void RemoveThruster(Thruster thruster)
	{
		foreach(HashSet<Thruster> thrusterGroup in thrusters)
		{
			thrusterGroup.Remove(thruster);
		}
	}
}
