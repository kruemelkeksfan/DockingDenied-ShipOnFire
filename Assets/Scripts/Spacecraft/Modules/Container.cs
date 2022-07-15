using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Container : Module
{
    [SerializeField] protected GoodManager.State state = GoodManager.State.solid;
	[Tooltip("Cargo Racks or Tank System?")]
	[SerializeField] private GoodManager.ComponentType storageComponentType = GoodManager.ComponentType.CargoRacks;
	protected InventoryController inventoryController = null;
	protected Storage storage = null;
	protected Dictionary<string, uint> loads = null;

	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position, listenUpdates, listenFixedUpdates);

		inventoryController = GetComponentInParent<InventoryController>();

		storage = new Storage();
		componentSlots.Add(storageComponentType, storage);
		inventoryController.AddContainer(this);

		loads = new Dictionary<string, uint>();
	}

	public override void Deconstruct()
	{
		// Dump Contents manually to update their Module Mass in Spacecraft correctly
		List<string> loadKeys = new List<string>(loads.Keys);
		foreach(string loadName in loadKeys)
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

		if(volume <= storage.GetFreeCapacity())
		{
			if(!loads.ContainsKey(goodName))
			{
				loads[goodName] = volume;
			}
			else
			{
				loads[goodName] += volume;
			}

			if(!storage.Deposit(volume))
			{
				Debug.LogWarning("Depositing " + amount + " " + goodName + " in " + storageComponentType + " failed!");
			}

			mass += good.mass * amount;
			spacecraft.UpdateMass();

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
			if(!storage.Withdraw(volume))
			{
				Debug.LogWarning("Withdrawing " + amount + " " + goodName + " from " + storageComponentType + " failed!");
			}

			if(loads[goodName] <= 0)
			{
				loads.Remove(goodName);
			}

			mass -= good.mass * amount;
			spacecraft.UpdateMass();

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

	public virtual uint GetFreeCapacity(string goodName)
	{
		return storage.GetFreeCapacity();
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
