using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

	private Dictionary<Vector2Int, Module> modules = null;
	private HashSet<Module> updateListeners = null;
	private HashSet<Module> fixedUpdateListeners = null;
	private new Rigidbody2D rigidbody = null;
	private HashSet<ThrusterModule>[] thrusters = null;

	private void Awake()
	{
		modules = new Dictionary<Vector2Int, Module>();
		modules.Add(Vector2Int.zero, gameObject.GetComponentInChildren<Module>());
		updateListeners = new HashSet<Module>();
		fixedUpdateListeners = new HashSet<Module>();
		rigidbody = gameObject.GetComponentInChildren<Rigidbody2D>();
		thrusters = new HashSet<ThrusterModule>[Enum.GetValues(typeof(ThrusterGroup)).Length];
		for(int i = 0; i < thrusters.Length; ++i)
		{
			thrusters[i] = new HashSet<ThrusterModule>();
		}
		rigidbody.centerOfMass = Vector2.zero;
	}

	private void Update()
	{
		foreach(Module module in updateListeners)
		{
			module.UpdateNotify();
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
		Vector2 lever = (Vector2) thruster.transform.localPosition - rigidbody.centerOfMass;
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
}
