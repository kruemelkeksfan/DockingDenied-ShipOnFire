using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Capacitor
{
	[Tooltip("Energy Capacity in kWs, 1m^2 of SciFi Solar Panel in this Game is suppossed to produce 0.4kW, the 400m^2 of one Module therefore produce 160kW or 160kWs per Second.")]
	public float capacity = 320.0f;
	[Tooltip("The current Charge of this Capacitor.")]
	public float charge = 0.0f;

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
}
