using System;
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

	[SerializeField] private Module commandModulePrefab = null;
	[SerializeField] private Transform centerOfMassIndicator = null;
	[Tooltip("Minimum Fraction of Force which must be exerted by a Thruster in a Direction to add it to the corresponding Direction Thruster Group")]
	[SerializeField] private float directionalForceThreshold = 0.2f;
	private Dictionary<Vector2Int, Module> modules = null;
	private HashSet<IUpdateListener> updateListeners = null;
	private HashSet<IFixedUpdateListener> fixedUpdateListeners = null;
	private BuildingMenu buildingMenu = null;
	private InventoryController inventoryController = null;
	private Vector2 internalCenterOfMass = Vector2.zero;
	private float foreignMass = 0.0f;
	private HashSet<Thruster>[] thrusters = null;
	private HashSet<Thruster> inactiveThrusters = null;
	private bool calculateCollider = false;
	private float halfGridSize = 0.0f;
	private PolygonCollider2D spacecraftCollider = null;
	private Dictionary<SpacecraftController, Transform> dockedSpacecraft = null;
	private bool thrusting = false;

	protected override void Awake()
	{
		base.Awake();

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

		// If no Blueprint was loaded during Awake()
		if(modules.Count <= 0)
		{
			GameObject.Instantiate<Module>(commandModulePrefab, transform).Build(Vector2Int.zero);
		}

		ToggleController.GetInstance().AddToggleObject("COMIndicators", centerOfMassIndicator.gameObject);
		gravityWellController.AddGravityObject(this);
	}

	private void OnDestroy()
	{
		if(rigidbody != null)
		{
			gravityWellController?.RemoveGravityObject(rigidbody);
		}
		ToggleController.GetInstance()?.RemoveToggleObject("COMIndicators", centerOfMassIndicator.gameObject);
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
	}

	public void Docked(DockingPort port, DockingPort otherPort)
	{
		SpacecraftController otherSpacecraft = otherPort.GetSpacecraft();
		if(!dockedSpacecraft.ContainsKey(otherSpacecraft))                                                                      // First Caller manages both Spacecraft
		{
			dockedSpacecraft.Add(otherSpacecraft, otherSpacecraft.transform);
			otherSpacecraft.dockedSpacecraft.Add(this, transform);

			otherSpacecraft.UpdateForeignMass(transform, internalCenterOfMass, rigidbody.mass);
			UpdateForeignMass(otherSpacecraft.transform, otherSpacecraft.internalCenterOfMass, otherSpacecraft.rigidbody.mass);
		}
	}

	public void Undocked(DockingPort port, DockingPort otherPort)
	{
		SpacecraftController otherSpacecraft = otherPort.GetSpacecraft();
		if(dockedSpacecraft.Remove(otherPort.GetSpacecraft()))                                                                  // First Caller manages both Spacecraft // TODO: What if attached via multiple Ports? (maybe check every other port in a foreach loop)
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
		if(!Mathf.Approximately(vertical, 0.0f) || !Mathf.Approximately(vertical, 0.0f) || !Mathf.Approximately(vertical, 0.0f))
		{
			thrusting = true;
			// This Loop interferes with resetting objectRecord.thrusting here (Race Condition), so instead reset it in GravityWellController
			foreach(SpacecraftController spacecraft in dockedSpacecraft.Keys)
			{
				spacecraft.thrusting = true;
			}
		}

		// TODO: Do we need inactiveThrusters? Or can simply all Thruster be set to 0 Throttle in the Beginning instead of at the End?
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
	// TODO: Unity.Rigidbody Max Mass is 1000000, so assure that this is not exceeded
	public void UpdateModuleMass(Vector2 position = new Vector2(), float massDifference = 0.0f)
	{
		if(massDifference != 0.0f)
		{
			if(rigidbody.mass < 0.0002f)                                                                                            // Set Rigidbody Mass when updating for the first Time
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

			foreach(SpacecraftController spacecraft in dockedSpacecraft.Keys)
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
	// TODO: Have 2 CoM-Indicators on Player Spacecraft, 1 for local CoM and 1 for docked CoM
	public void UpdateForeignMass(Transform otherTransform, Vector2 position, float massDifference)
	{
		foreignMass += massDifference;

		if(dockedSpacecraft.Count > 0)
		{
			rigidbody.centerOfMass += ((Vector2)transform.InverseTransformPoint(otherTransform.TransformPoint(position)) - rigidbody.centerOfMass) * (massDifference / (rigidbody.mass + foreignMass));
		}
		// Docking Ports are usually not perfectly aligned while Docking, therefore a random Error creeps into the Calculation, the following resets this Error
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
