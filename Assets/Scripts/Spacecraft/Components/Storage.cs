using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Storage : ModuleComponent
{
	// Capacity in m^3
	private uint capacity = 0;
	// Current Load in m^3
	private uint load = 0;

	public override void UpdateComponentData(string componentName)
	{
		base.UpdateComponentData(componentName);

		if(componentName != null)
		{
			capacity = (uint) Mathf.RoundToInt(GetAttribute("Capacity"));
		}
		else
		{
			capacity = 0;
		}

		load = 0;
	}

	public bool Deposit(uint volume)
	{
		if(load + volume <= capacity)
		{
			load += volume;
			return true;
		}
		
		return false;
	}

	public bool Withdraw(uint volume)
	{
		if(volume <= load)
		{
			load -= volume;
			return true;
		}
		
		return false;
	}

	public uint GetFreeCapacity()
	{
		return capacity - load;
	}

	public uint GetCapacity()
	{
		return capacity;
	}

	public uint GetLoad()
	{
		return load;
	}
}