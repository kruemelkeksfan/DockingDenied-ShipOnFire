using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

	[SerializeField] private Module commandModulePrefab = null;
	[SerializeField] private Transform centerOfMassIndicator = null;
	private Dictionary<Vector2Int, Module> modules = null;
	private HashSet<IUpdateListener> updateListeners = null;
	private HashSet<IFixedUpdateListener> fixedUpdateListeners = null;
	private new Transform transform = null;
	private InventoryController inventoryController = null;
	private new Rigidbody2D rigidbody = null;
	private HashSet<Thruster>[] thrusters = null;

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

		rigidbody.centerOfMass = Vector2.zero;
	}

	private void Start()
	{
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
		GravityWellController.GetInstance().RemoveGravityObject(GetComponent<Rigidbody2D>());
		ToggleController.GetInstance().RemoveToggleObject("COMIndicators", centerOfMassIndicator.gameObject);

		// TODO: Switch to other Player Spacecraft if available
		if(this == SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft())
		{
			GameController.GetInstance().Restart("Game Over...just one more Round...");
		}
	}

	private void Update()
	{
		foreach(IUpdateListener listener in updateListeners)
		{
			listener.UpdateNotify();
		}
	}

	private void FixedUpdate()
	{
		foreach(IFixedUpdateListener listener in fixedUpdateListeners)
		{
			listener.FixedUpdateNotify();
		}
	}

	public void SetThrottles(float horizontal, float vertical, float rotationSpeed)
	{
		HashSet<Thruster> inactiveThrusters = new HashSet<Thruster>(thrusters[(int)ThrusterGroup.all]);

		// TODO: Remove Else to enable activating opposing Thrusters at the same Time
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

		foreach(Thruster thruster in inactiveThrusters)
		{
			thruster.SetThrottle(0.0f);
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

	public bool PositionsAvailable(Vector2Int[] positions, bool HasAttachableReservePositions, bool HasOverlappingReservePositions)
	{
		bool neighbour = false;
		for(int i = 0; i < positions.Length; ++i)
		{
			if(modules.ContainsKey(positions[i])                                                                                                            // Position is already in Use
				&& (i == 0 || positions[i] == modules[positions[i]].GetPosition()                                                                           // Either Requester or current Position User have their Main Position on this Position
				|| !HasOverlappingReservePositions || !modules[positions[i]].HasOverlappingReservePositions()))                                             // Either Requester or current Position User do not allow overlapping Reserve Positions
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
	}

	public bool RemoveModule(Vector2Int position)
	{
		return modules.Remove(position);
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

	public void RemoveThruster(Thruster thruster)
	{
		foreach(HashSet<Thruster> thrusterGroup in thrusters)
		{
			thrusterGroup.Remove(thruster);
		}
	}
}
