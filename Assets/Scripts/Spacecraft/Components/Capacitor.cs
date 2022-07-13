using System;
using UnityEngine;

[Serializable]
public class Capacitor : ModuleComponent
{
	// TODO: Rename to EnergyStorage
	// TODO: Max Charge-/Discharge-Rate
	private float capacity = 0.0f;
	private float charge = 0.0f;

	public override void UpdateComponentData(string componentName)
	{
		base.UpdateComponentData(componentName);

		if(componentName != null)
		{
			capacity = GetAttribute("Capacity");
		}
		else
		{
			capacity = 0.0f;
			charge = 0.0f;
		}
	}

	public float Charge(float amount)
	{
		float newCharge = charge + amount;
		if(newCharge <= capacity)
		{
			charge = newCharge;
			return 0.0f;
		}
		else
		{
			charge = capacity;
			return newCharge - capacity;
		}
	}

	public bool Discharge(float amount)
	{
		if(amount <= charge)
		{
			charge -= amount;
			return true;
		}
		else
		{
			return false;
		}
	}

	public float DischargePartial(float amount)
	{
		if(amount <= charge)
		{
			charge -= amount;
			return 1.0f;
		}
		else
		{
			float percentage = charge / amount;
			charge = 0.0f;
			return percentage;
		}
	}

	public float DischargeAll()
	{
		float charge = this.charge;
		this.charge = 0.0f;
		return charge;
	}

	public float GetCapacity()
	{
		return capacity;
	}

	public float GetCharge()
	{
		return charge;
	}
}
