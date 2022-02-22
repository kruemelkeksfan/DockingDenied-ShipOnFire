﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// TODO: What happens to attached Modules when link is severed? Maybe implement Shift LeftClick to make Selection and select the severed Modules automatically for easy Movement/Reattachment

public class SpacecraftController : GravityObjectController, IDockingListener
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

	private static WaitForFixedUpdate waitForFixedUpdate = null;

	[SerializeField] private Module commandModulePrefab = null;
	[SerializeField] private Transform centerOfMassIndicator = null;
	[SerializeField] private Transform foreignCenterOfMassIndicator = null;
	[Tooltip("Minimum Fraction of Force which must be exerted by a Thruster in a Direction to add it to the corresponding Direction Thruster Group")]
	[SerializeField] private float directionalForceThreshold = 0.2f;
	[Tooltip("Rotations with less than this minAngularVelocity will be stopped after a short Time")]
	[SerializeField] private float minAngularVelocity = 1.0f;
	[Tooltip("Time until a slow Rotation will be stopped, if the angularVelocity stays too low")]
	[SerializeField] private float rotationStopTime = 2.0f;
	private Dictionary<Vector2Int, Module> modules = null;
	private HashSet<IUpdateListener> updateListeners = null;
	private HashSet<IFixedUpdateListener> fixedUpdateListeners = null;
	private BuildingMenu buildingMenu = null;
	private InventoryController inventoryController = null;
	private float inertiaFactor = 1.0f;
	private Vector2 foreignCenterOfMass = Vector2.zero;
	private HashSet<Thruster>[] thrusters = null;
	private HashSet<Thruster> inactiveThrusters = null;
	private bool calculateCollider = false;
	private float halfGridSize = 0.0f;
	private PolygonCollider2D spacecraftCollider = null;
	private Dictionary<SpacecraftController, Transform> dockedSpacecraft = null;
	private bool thrusting = false;
	private bool stoppingRotation = false;

	protected override void Awake()
	{
		base.Awake();

		waitForFixedUpdate = new WaitForFixedUpdate();

		modules = new Dictionary<Vector2Int, Module>();
		updateListeners = new HashSet<IUpdateListener>();
		fixedUpdateListeners = new HashSet<IFixedUpdateListener>();
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

		buildingMenu = BuildingMenu.GetInstance();
		halfGridSize = buildingMenu.GetGridSize() * 0.5f;
		spacecraftCollider = GetComponent<PolygonCollider2D>();

		float moduleWidth = buildingMenu.GetGridSize();
		inertiaFactor = (1.0f / 6.0f) * (moduleWidth * moduleWidth);

		// If no Blueprint was loaded during Awake()
		if(modules.Count <= 0)
		{
			GameObject.Instantiate<Module>(commandModulePrefab, transform).Build(Vector2Int.zero);
		}

		ToggleController toggleController = ToggleController.GetInstance();
		toggleController.AddToggleObject(ToggleController.GroupNames.COMIndicators, centerOfMassIndicator.gameObject);
		toggleController.AddToggleObject(ToggleController.GroupNames.COMIndicators, foreignCenterOfMassIndicator.gameObject);
		centerOfMassIndicator.gameObject.SetActive(toggleController.IsGroupToggled(ToggleController.GroupNames.COMIndicators));
		foreignCenterOfMassIndicator.gameObject.SetActive(toggleController.IsGroupToggled(ToggleController.GroupNames.COMIndicators));

		gravityWellController.AddGravityObject(this);
	}

	private void OnDestroy()
	{
		if(rigidbody != null)
		{
			gravityWellController?.RemoveGravityObject(rigidbody);
		}

		ToggleController toggleController = ToggleController.GetInstance();
		toggleController?.RemoveToggleObject(ToggleController.GroupNames.COMIndicators, centerOfMassIndicator.gameObject);
		toggleController?.RemoveToggleObject(ToggleController.GroupNames.COMIndicators, foreignCenterOfMassIndicator.gameObject);
	}

	private void Update()
	{
		foreach(IUpdateListener listener in updateListeners)
		{
			listener.UpdateNotify();
		}

		// Do this in Update instead of FixedUpdate(), since it might take some Time
		if(calculateCollider)
		{
			CalculateSpacecraftCollider();
			calculateCollider = false;
		}
	}

	private void FixedUpdate()
	{
		foreach(IFixedUpdateListener listener in fixedUpdateListeners)
		{
			listener.FixedUpdateNotify();
		}

		if(!stoppingRotation && rigidbody.angularVelocity != 0.0f && Mathf.Abs(rigidbody.angularVelocity) < minAngularVelocity)
		{
			StartCoroutine(StopRotation());
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
			UpdateForeignMass();
		}
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
		if(!Mathf.Approximately(vertical, 0.0f) || !Mathf.Approximately(vertical, 0.0f) || !Mathf.Approximately(vertical, 0.0f))
		{
			thrusting = true;
			// This Loop interferes with resetting objectRecord.thrusting here (Race Condition), so instead reset it in GravityWellController
			foreach(SpacecraftController spacecraft in dockedSpacecraft.Keys)
			{
				spacecraft.thrusting = true;
			}
		}

		// inactiveThrusters is needed, because Thruster Particle System stops when throttle is set to 0
		inactiveThrusters.Clear();
		inactiveThrusters.UnionWith(thrusters[(int)ThrusterGroup.all]);

		if(vertical > 0.0f)
		{
			SetThrusterGroupThrottle(ThrusterGroup.up, vertical, inactiveThrusters);
		}
		if(vertical < 0.0f)
		{
			SetThrusterGroupThrottle(ThrusterGroup.down, -vertical, inactiveThrusters);
		}

		if(horizontal < 0.0f)
		{
			SetThrusterGroupThrottle(ThrusterGroup.left, -horizontal, inactiveThrusters);
		}
		if(horizontal > 0.0f)
		{
			SetThrusterGroupThrottle(ThrusterGroup.right, horizontal, inactiveThrusters);
		}

		if(rotationSpeed < 0.0f)
		{
			SetThrusterGroupThrottle(ThrusterGroup.turnLeft, -rotationSpeed, inactiveThrusters);
		}
		if(rotationSpeed > 0.0f)
		{
			SetThrusterGroupThrottle(ThrusterGroup.turnRight, rotationSpeed, inactiveThrusters);
		}

		foreach(Thruster thruster in inactiveThrusters)
		{
			thruster.SetThrottle(0.0f);
		}
	}

	// TODO: Unity.Rigidbody Max Mass is 1000000, so assure that this is not exceeded (e.g. let Spacestations deny docking/undock after surpassing a Threshold to force the Player to dump Goods/deconstruct Modules)
	public void UpdateMass()
	{
		// Calculate inefficiently by looping through all Modules, because we need to loop through all Modules for Inertia Calculation anyways

		// Calculate Center of Mass
		Vector2 centerOfMass = Vector2.zero;
		float totalMass = 0.0f;
		foreach(KeyValuePair<Vector2Int, Module> moduleData in modules)
		{
			if(moduleData.Key == moduleData.Value.GetPosition())
			{
				float moduleMass = moduleData.Value.GetMass();

				centerOfMass += (Vector2)moduleData.Value.GetTransform().localPosition * moduleMass;
				totalMass += moduleMass;
			}
		}

		if(totalMass > MathUtil.EPSILON)
		{
			// Finish Center of Mass Calculation
			centerOfMass /= totalMass;

			// Calculate Moment of Inertia
			float inertia = 0.0f;
			foreach(KeyValuePair<Vector2Int, Module> moduleData in modules)
			{
				if(moduleData.Key == moduleData.Value.GetPosition())
				{
					// Approximate Modules as Cubes and use Steiner's Theorem to calculate their Moment of Inertia around the Ships Center of Mass
					// https://en.wikipedia.org/wiki/List_of_moments_of_inertia
					// https://en.wikipedia.org/wiki/Parallel_axis_theorem
					inertia += (inertiaFactor + ((Vector2)moduleData.Value.GetTransform().localPosition - centerOfMass).sqrMagnitude) * moduleData.Value.GetMass();
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
		// Short Circuit Check, all Code below could handle an empty dockedSpacecraft-Dictionary, but we can save Performance by skipping to the relevant Part
		if(dockedSpacecraft.Count > 0)
		{
			// GetRecursivelyDockedSpacecraft() includes this Spacecraft
			HashSet<SpacecraftController> recursivelyDockedSpacecraft = GetDockedSpacecraftRecursively();
			Vector2 centerOfMass = Vector2.zero;
			float totalMass = 0.0f;
			foreach(SpacecraftController dockedSpacecraft in recursivelyDockedSpacecraft)
			{
				float spacecraftMass = dockedSpacecraft.rigidbody.mass;

				// Convert other Spacecrafts Center of Mass to World Coordinates and then to this Spacecrafts local Coordinates
				centerOfMass += (Vector2)transform.InverseTransformPoint(dockedSpacecraft.transform.TransformPoint(dockedSpacecraft.rigidbody.centerOfMass)) * spacecraftMass;
				totalMass += spacecraftMass;
			}
			centerOfMass /= totalMass;

			foreach(SpacecraftController dockedSpacecraft in recursivelyDockedSpacecraft)
			{
				Vector2 otherCenterOfMass = centerOfMass;
				if(dockedSpacecraft != this)
				{
					otherCenterOfMass = dockedSpacecraft.transform.InverseTransformPoint(transform.TransformPoint(centerOfMass));
				}

				dockedSpacecraft.foreignCenterOfMass = otherCenterOfMass;
				dockedSpacecraft.foreignCenterOfMassIndicator.localPosition = otherCenterOfMass;
				dockedSpacecraft.CalculateThrusterTurningGroups();
			}
		}
		else
		{
			foreignCenterOfMass = rigidbody.centerOfMass;
			foreignCenterOfMassIndicator.localPosition = foreignCenterOfMass;
			CalculateThrusterTurningGroups();
		}
	}

	public bool PositionsAvailable(Vector2Int[] positions, bool HasAttachableReservePositions, bool HasOverlappingReservePositions, bool ignoreCommandModule = false, bool ignoreAttachmentPoints = false)
	{
		bool neighbour = ignoreAttachmentPoints;
		for(int i = 0; i < positions.Length; ++i)
		{
			if(modules.ContainsKey(positions[i])                                                                                                           // Position is already in Use
				&& (i == 0 || positions[i] == modules[positions[i]].GetPosition()                                                                          // Either Requester or current Position User have their Main Position on this Position
				|| !HasOverlappingReservePositions || !modules[positions[i]].HasOverlappingReservePositions())                                             // Either Requester or current Position User do not allow overlapping Reserve Positions
				&& !(ignoreCommandModule && modules[positions[i]].GetModuleName() == "Command Module"))
			{
				return false;
			}

			if(!neighbour)
			{
				foreach(Vector2Int direction in Directions.VECTORS)
				{
					Vector2Int neighbourPosition = positions[i] + direction;
					if(modules.ContainsKey(neighbourPosition)                                                                                               // Position has a Neighbour
						&& (i == 0 || HasAttachableReservePositions)                                                                                        // Requester either neighbours with his Main Position or allows attachable Reserve Positions
						&& (neighbourPosition == modules[neighbourPosition].GetPosition() || modules[neighbourPosition].HasAttachableReservePositions()))   // Neighbour either neighbours with his Main Position or allows attachable Reserve Positions
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

	public void DeconstructModules()
	{
		List<Vector2Int> moduleKeys = new List<Vector2Int>(modules.Keys);                       // Avoid concurrent Modification
		foreach(Vector2Int position in moduleKeys)
		{
			if(modules.ContainsKey(position) && modules[position].GetPosition() == position)    // Check if Module has already been deleted first
			{
				modules[position].Deconstruct();
			}
		}
	}

	private void SetThrusterGroupThrottle(ThrusterGroup thrusterGroup, float throttle, HashSet<Thruster> inactiveThrusters)
	{
		foreach(Thruster thruster in thrusters[(int)thrusterGroup])
		{
			thruster.SetThrottle(throttle);
			inactiveThrusters.Remove(thruster);
		}
	}

	private void CalculateSpacecraftCollider()
	{
		if(modules.Count <= 0)
		{
			spacecraftCollider.SetPath(0, new Vector2[] { Vector2.zero });
			return;
		}

		// Find Maximum Y Value of this Spacecrafts Modules
		int maxY = 0;
		foreach(Vector2Int maxPosition in modules.Keys)
		{
			if(maxPosition.y > maxY)
			{
				maxY = maxPosition.y;
			}
		}

		// Go from Top Bounding Border of the Spacecraft downwards until you find the topmost Module at X = 0
		Vector2Int position = new Vector2Int(0, maxY);
		while(!modules.ContainsKey(position) || (position != modules[position].GetPosition() && modules[position].HasOverlappingReservePositions()))
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
		return modules.ContainsKey(position) && (position == modules[position].GetPosition() || !modules[position].HasOverlappingReservePositions())
				&& (!modules.ContainsKey(borderingPosition) || (borderingPosition != modules[borderingPosition].GetPosition() && modules[borderingPosition].HasOverlappingReservePositions()));
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
		Vector2 lever = (Vector2)thruster.transform.localPosition - foreignCenterOfMass;
		Vector2 thrust = thruster.GetThrustVector();
		float torque = Vector3.Cross(lever, thrust).z;
		// To find the Fraction of Thrust used for Rotation, Project thrust on lever, normalize by dividing by thrust.magnitude and subtract the Result from 100%
		float rotationFraction = 1.0f - Mathf.Abs((Vector2.Dot(lever, thrust) / lever.magnitude) / thrust.magnitude);
		if(rotationFraction > directionalForceThreshold)
		{
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

	private IEnumerator StopRotation()
	{
		stoppingRotation = true;

		float startTime = Time.time;
		do
		{
			yield return waitForFixedUpdate;

			if(Mathf.Abs(rigidbody.angularVelocity) >= minAngularVelocity)
			{
				stoppingRotation = false;
				yield break;
			}
		}
		while(Time.time - startTime < rotationStopTime);

		rigidbody.angularVelocity = 0.0f;
		stoppingRotation = false;
	}

	public override void ToggleRenderer(bool activateRenderer)
	{
		foreach(MeshRenderer renderer in gameObject.GetComponentsInChildren<MeshRenderer>())
		{
			renderer.enabled = activateRenderer;
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
		return thrusting;
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

	public Dictionary<Vector2Int, Module> GetModules()
	{
		return modules;
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
			if(!recursivelyDockedSpacecraft.Contains(dockedSpacecraft))
			{
				recursivelyDockedSpacecraft.Add(dockedSpacecraft);
				dockedSpacecraft.GetDockedSpacecraftRecursively(recursivelyDockedSpacecraft);
			}
		}

		return recursivelyDockedSpacecraft;
	}

	public InventoryController GetInventoryController()
	{
		return inventoryController;
	}

	public void SetThrusting(bool thrusting)
	{
		this.thrusting = thrusting;
	}

	public void AddModule(Vector2Int position, Module module)
	{
		modules[position] = module;
		calculateCollider = true;
	}

	public void RemoveModule(Vector2Int position)
	{
		modules.Remove(position);
		calculateCollider = true;
	}

	public void AddUpdateListener(IUpdateListener listener)
	{
		updateListeners.Add(listener);
	}

	public void RemoveUpdateListener(IUpdateListener listener)
	{
		updateListeners.Remove(listener);
	}

	public void AddFixedUpdateListener(IFixedUpdateListener listener)
	{
		fixedUpdateListeners.Add(listener);
	}

	public void RemoveFixedUpdateListener(IFixedUpdateListener listener)
	{
		fixedUpdateListeners.Remove(listener);
	}

	// Call this for all Thrusters when building the Ship
	public void AddThruster(Thruster thruster)
	{
		thrusters[(int)ThrusterGroup.all].Add(thruster);

		// Add linear Thrust Group
		Vector2 thrust = thruster.GetThrustVector().normalized;
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