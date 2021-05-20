using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Container : Module
{
    [SerializeField] protected GoodManager.State state = GoodManager.State.solid;
    [Tooltip("Capacity in m^3.")]
    [SerializeField] protected uint capacity = 200;
	protected Dictionary<string, uint> loads = null;
	protected uint freeCapacity = 0;
	protected GoodManager goodManager = null;
	protected InventoryController inventoryController = null;

	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position, listenUpdates, listenFixedUpdates);

		loads = new Dictionary<string, uint>(1);
		freeCapacity = capacity;

		goodManager = GoodManager.GetInstance();
		inventoryController = GetComponentInParent<InventoryController>();
		inventoryController.AddContainer(this);
	}

	public override void Deconstruct()
	{
		// Dump Contents manually to update their Module Mass in Spacecraft correctly
		foreach(string loadName in loads.Keys)
		{
			Withdraw(loadName, loads[loadName]);
		}

		inventoryController.RemoveContainer(this);
		base.Deconstruct();
	}

	public virtual bool Deposit(string goodName, uint amount)
	{
		GoodManager.Good good = goodManager.GetGood(goodName);
		uint volume = (uint) Mathf.CeilToInt(good.volume) * amount;

		if(volume <= 0)
		{
			return true;
		}

		if(volume <= freeCapacity)
		{
			if(!loads.ContainsKey(goodName))
			{
				loads[goodName] = volume;
			}
			else
			{
				loads[goodName] += volume;
			}

			freeCapacity -= volume;

			spacecraft.UpdateModuleMass(transform.localPosition, good.mass * amount);

			return true;
		}
		else
		{
			return false;
		}
	}

	public bool Withdraw(string goodName, uint amount)
	{
		GoodManager.Good good = goodManager.GetGood(goodName);
		uint volume = (uint) Mathf.CeilToInt(good.volume) * amount;

		if(volume <= 0)
		{
			return true;
		}

		if(loads.ContainsKey(goodName) && loads[goodName] >= volume)
		{
			loads[goodName] -= volume;
			freeCapacity += volume;

			if(loads[goodName] <= 0)
			{
				loads.Remove(goodName);
			}

			spacecraft.UpdateModuleMass(transform.localPosition, -good.mass * amount);

			return true;
		}
		else
		{
			return false;
		}
	}

	public GoodManager.State GetState()
	{
		return state;
	}

	public uint GetFreeCapacity()
	{
		return freeCapacity;
	}

	public uint GetGoodAmount(string goodName)
	{
		if(loads.ContainsKey(goodName))
		{
			return loads[goodName] / (uint) Mathf.CeilToInt(goodManager.GetGood(goodName).volume);
		}
		else
		{
			return 0;
		}
	}

	public Dictionary<string, uint> GetLoads()
	{
		return loads;
	}
}
