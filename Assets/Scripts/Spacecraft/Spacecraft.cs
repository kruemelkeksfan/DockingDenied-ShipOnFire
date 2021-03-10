using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spacecraft : MonoBehaviour
{
	private Dictionary<Vector2Int, Module> modules = null;
	private List<Module> updateListeners = null;
	private List<Module> fixedUpdateListeners = null;

	private void Start()
	{
		modules = new Dictionary<Vector2Int, Module>();
		modules.Add(Vector2Int.zero, gameObject.GetComponentInChildren<Module>());
		updateListeners = new List<Module>();
		fixedUpdateListeners = new List<Module>();
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

	public void AddModule(Vector2Int position, Module module)
	{
		modules[position] = module;
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

	public bool RemoveModule(Vector2Int position)
	{
		return modules.Remove(position);
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
}
