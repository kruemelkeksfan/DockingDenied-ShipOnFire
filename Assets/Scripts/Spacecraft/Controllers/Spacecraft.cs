using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// TODO: What happens to attached Modules when link is severed? Maybe implement Shift LeftClick to make Selection and select the severed Modules automatically for easy Movement/Reattachment

public class Spacecraft : MonoBehaviour, IDockingListener
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

	[SerializeField] private Module commandModulePrefab = null;
	[SerializeField] private Transform centerOfMassIndicator = null;
	[Tooltip("Minimum Turning Force which must be exerted by a Thruster to consider it a turning Thruster")]
	[SerializeField] private float turningForceThreshold = 0.002f;
	private Dictionary<Vector2Int, Module> modules = null;
	private HashSet<IUpdateListener> updateListeners = null;
	private HashSet<IFixedUpdateListener> fixedUpdateListeners = null;
	private BuildingMenu buildingMenu = null;
	private new Transform transform = null;
	private InventoryController inventoryController = null;
	private new Rigidbody2D rigidbody = null;
	private Vector2 internalCenterOfMass = Vector2.zero;
	private float foreignMass = 0.0f;
	private HashSet<Thruster>[] thrusters = null;
	private HashSet<Thruster> inactiveThrusters = null;
	private bool calculateCollider = false;
	private float halfGridSize = 0.0f;
	private PolygonCollider2D spacecraftCollider = null;
	private Dictionary<Spacecraft, Transform> dockedSpacecraft = null;

	private void Awake()
	{
		modules = new Dictionary<Vector2Int, Module>();
		updateListeners = new HashSet<IUpdateListener>();
		fixedUpdateListeners = new HashSet<IFixedUpdateListener>();
		transform = gameObject.GetComponent<Transform>();
		inventoryController = gameObject.GetComponent<InventoryController>();
		rigidbody = gameObject.GetComponentInChildren<Rigidbody2D>();

		thrusters = new HashSet<Thruster>[Enum.GetValues(typeof(ThrusterGroup)).Length];
		for(int i = 0; i < thrusters.Length; ++i)
		{
			thrusters[i] = new HashSet<Thruster>();
		}
		inactiveThrusters = new HashSet<Thruster>();

		dockedSpacecraft = new Dictionary<Spacecraft, Transform>(2);

		rigidbody.centerOfMass = Vector2.zero;
	}

	private void Start()
	{
		buildingMenu = BuildingMenu.GetInstance();
		halfGridSize = buildingMenu.GetGridSize() * 0.5f;
		spacecraftCollider = GetComponent<PolygonCollider2D>();

		if(modules.Count <= 0)                                                                      // If no Blueprint was loaded during Awake()
		{
			GameObject.Instantiate<Module>(commandModulePrefab, transform).Build(Vector2Int.zero);
		}

		GetComponent<GravityController>().SetOptimalOrbitalVelocity();

		ToggleController.GetInstance().AddToggleObject("COMIndicators", centerOfMassIndicator.gameObject);
		GravityWellController.GetInstance().AddGravityObject(GetComponent<Rigidbody2D>());
	}

	private void OnDestroy()
	{
		Rigidbody2D rigidbody = GetComponent<Rigidbody2D>();
		if(rigidbody != null)
		{
			GravityWellController.GetInstance()?.RemoveGravityObject(rigidbody);
		}
		ToggleController.GetInstance()?.RemoveToggleObject("COMIndicators", centerOfMassIndicator.gameObject);
	}

	private void Update()
	{
		foreach(IUpdateListener listener in updateListeners)
		{
			listener.UpdateNotify();
		}

		if(calculateCollider)                                           // Do this in Update instead of FixedUpdate(), since it might take some Time
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
	}

	public void Docked(DockingPort port, DockingPort otherPort)
	{
		Spacecraft otherSpacecraft = otherPort.GetSpacecraft();
		if(!dockedSpacecraft.ContainsKey(otherSpacecraft))																		// First Caller manages both Spacecraft
		{
			dockedSpacecraft.Add(otherSpacecraft, otherSpacecraft.transform);
			otherSpacecraft.dockedSpacecraft.Add(this, transform);

			otherSpacecraft.UpdateForeignMass(transform, internalCenterOfMass, rigidbody.mass);
			UpdateForeignMass(otherSpacecraft.transform, otherSpacecraft.internalCenterOfMass, otherSpacecraft.rigidbody.mass);
		}
	}

	public void Undocked(DockingPort port, DockingPort otherPort)
	{
		Spacecraft otherSpacecraft = otherPort.GetSpacecraft();
		if(dockedSpacecraft.Remove(otherPort.GetSpacecraft()))																	// First Caller manages both Spacecraft
		{
			otherSpacecraft.dockedSpacecraft.Remove(this);

			otherSpacecraft.UpdateForeignMass(transform, internalCenterOfMass, -rigidbody.mass);
			UpdateForeignMass(otherSpacecraft.transform, otherSpacecraft.internalCenterOfMass, -otherSpacecraft.rigidbody.mass);
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

	// TODO: Floating Point Errors will sum up in this Method, should regularily (e.g. every 100 Calls && dockedSpacecraft.Count <= 0) reset/recalculate all Values
	public void UpdateModuleMass(Vector2 position = new Vector2(), float massDifference = 0.0f)
	{
		if(massDifference != 0.0f)
		{
			if(rigidbody.mass < 0.0002f)																							// Set Rigidbody Mass when updating for the first Time
			{
				rigidbody.centerOfMass = position;
				internalCenterOfMass = position;
				rigidbody.mass = massDifference;
			}
			else
			{
				rigidbody.mass += massDifference;
				rigidbody.centerOfMass += (position - rigidbody.centerOfMass) * (massDifference / (rigidbody.mass + foreignMass));
				internalCenterOfMass += (position - internalCenterOfMass) * (massDifference / rigidbody.mass);
			}

			foreach(Spacecraft spacecraft in dockedSpacecraft.Keys)
			{
				spacecraft.UpdateForeignMass(transform, position, massDifference);
			}
		}
		
		thrusters[(int)ThrusterGroup.turnLeft].Clear();
		thrusters[(int)ThrusterGroup.turnRight].Clear();
		foreach(Thruster thruster in thrusters[(int)ThrusterGroup.all])
		{
			CalculateThrusterTurningGroup(thruster);
		}

		centerOfMassIndicator.localPosition = rigidbody.centerOfMass;
	}

	// TODO: Enable Stacking (recursive Calls for other Spacecraft which are docked to the foreign Spacecraft)
	public void UpdateForeignMass(Transform otherTransform, Vector2 position, float massDifference)
	{
		foreignMass += massDifference;

		if(dockedSpacecraft.Count > 0)
		{
			rigidbody.centerOfMass += ((Vector2)transform.InverseTransformPoint(otherTransform.TransformPoint(position)) - rigidbody.centerOfMass) * (massDifference / (rigidbody.mass + foreignMass));
		}
		// Docking Ports are usually not perfectly aligned while Docking, therefore a relatively random Error creeps into the Calculation, the following resets this Error
		else
		{
			rigidbody.centerOfMass = internalCenterOfMass;
		}

		UpdateModuleMass();
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

	private void CalculateThrusterTurningGroup(Thruster thruster)
	{
		// Add rotational Thrust Group
		// M = r x F
		// M - Torque
		// r - Lever
		// F - Thrust
		Vector2 lever = (Vector2)thruster.transform.localPosition - rigidbody.centerOfMass;
		float torque = Vector3.Cross(lever, thruster.GetThrustVector()).z;
		if(torque < -turningForceThreshold)
		{
			thrusters[(int)ThrusterGroup.turnLeft].Add(thruster);
		}
		else if(torque > turningForceThreshold)
		{
			thrusters[(int)ThrusterGroup.turnRight].Add(thruster);
		}
	}

	public bool IsDockedToStation()
	{
		foreach(KeyValuePair<Spacecraft, Transform> dockedSpacecraft in dockedSpacecraft)
		{
			if(dockedSpacecraft.Key.GetComponent<SpaceStationController>() != null)
			{
				return true;
			}
		}

		return false;
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

	public Transform GetTransform()
	{
		return transform;
	}

	public InventoryController GetInventoryController()
	{
		return inventoryController;
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
