using System;
using UnityEngine;

[Serializable]
// Don't implement ChargeRates, because the Code would be a Mess and the Feature not worth the Effort (think e.g. of large Energy Transfers between Ships during Jump-Start)
public class EnergyStorage : ModuleComponent
{
	private float capacity = 0.0f;
	private float charge = 0.0f;

	public override bool UpdateComponentData(string componentName)
	{
		base.UpdateComponentData(componentName);

		if(componentName != null)
		{
			capacity = GetAttribute("Capacity");
		}
		else
		{
			capacity = 0.0f;
		}

		charge = 0.0f;

		return true;
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

	public float DischargePartially(float amount)
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
