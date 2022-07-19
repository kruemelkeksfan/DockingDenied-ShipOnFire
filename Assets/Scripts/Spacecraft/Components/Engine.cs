using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Engine : ModuleComponent
{
	private float primaryFuelConsumption = 0.0f;
	private float secondaryFuelConsumption = 0.0f;
	private float thrust = 0.0f;

	public override bool UpdateComponentData(string componentName)
	{
		base.UpdateComponentData(componentName);

		if(componentName != null)
		{
			primaryFuelConsumption = GetAttribute("Primary Fuel Consumption");
			secondaryFuelConsumption = GetAttribute("Secondary Fuel Consumption");
			thrust = GetAttribute("Thrust");
		}
		else
		{
			primaryFuelConsumption = 0.0f;
			secondaryFuelConsumption = 0.0f;
			thrust = 0.0f;
		}

		return true;
	}

	public float GetPrimaryFuelConsumption()
	{
		return primaryFuelConsumption;
	}

	public float GetSecondaryFuelConsumption()
	{
		return secondaryFuelConsumption;
	}

	public float GetThrust()
	{
		return thrust;
	}
}