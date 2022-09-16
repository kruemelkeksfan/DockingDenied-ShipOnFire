using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Container : Module
{
	[SerializeField] protected GoodManager.State state = GoodManager.State.solid;
	[Tooltip("Cargo Racks or Tank System?")]
	[SerializeField] private GoodManager.ComponentType storageComponentType = GoodManager.ComponentType.CargoRacks;
	protected Storage storage = null;
	protected Dictionary<string, uint> loads = null;
	private float cargoMass = 0.0f;

	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position, listenUpdates, listenFixedUpdates);

		storage = new Storage();
		AddComponentSlot(storageComponentType, storage);
		inventoryController.AddContainer(this);

		loads = new Dictionary<string, uint>();

		UpdateModuleMenuButtonText();
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
		uint volume = (uint)Mathf.CeilToInt(good.volume) * amount;

		if(volume <= 0)
		{
			return true;
		}

		if(volume <= storage.GetFreeCapacity())
		{
			if(!loads.ContainsKey(goodName))
			{
				loads[goodName] = amount;
			}
			else
			{
				loads[goodName] += amount;
			}

			if(!storage.Deposit(volume))
			{
				Debug.LogWarning("Depositing " + amount + " " + goodName + " in " + storageComponentType + " failed!");
			}

			float goodMass = good.mass * amount;
			cargoMass += goodMass;
			mass += goodMass;
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
		uint volume = (uint)Mathf.CeilToInt(good.volume) * amount;

		if(volume <= 0)
		{
			return true;
		}

		if(loads.ContainsKey(goodName) && loads[goodName] >= amount)
		{
			loads[goodName] -= amount;
			if(!storage.Withdraw(volume))
			{
				Debug.LogWarning("Withdrawing " + amount + " " + goodName + " from " + storageComponentType + " failed!");
			}

			if(loads[goodName] <= 0)
			{
				loads.Remove(goodName);
			}

			float goodMass = good.mass * amount;
			cargoMass -= goodMass;
			mass -= goodMass;
			spacecraft.UpdateMass();

			return true;
		}
		else
		{
			return false;
		}
	}

	public override Text UpdateModuleMenuButtonText()
	{
		Text barText = base.UpdateModuleMenuButtonText();

		if(barText != null && storage != null && inventoryController != null)
		{
			float capacity = storage.GetCapacity();
			float load = capacity > MathUtil.EPSILON ? storage.GetLoad() / capacity : 1.0f;
			float cargoMass = GetCargoMass();
			float totalCargoMass = Mathf.Max(inventoryController.GetHeaviestCargoMass(), MathUtil.EPSILON);

			barText.text += "\n<color=" + moduleManager.GetVolumeColor() + ">Vol " + moduleManager.GetBarString(load)
				+ "</color>\n<color=" + moduleManager.GetMassColor() + ">Mass " + moduleManager.GetBarString(cargoMass / totalCargoMass) + "</color>";
		}

		return barText;
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
			return loads[goodName];
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

	public float GetCargoMass()
	{
		return cargoMass;
	}
}
