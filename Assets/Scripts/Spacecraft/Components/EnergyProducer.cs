using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnergyProducer : ModuleComponent
{
	[Tooltip("Energy Production in kW, 1m^2 of SciFi Solar Panel in this Game is suppossed to produce 0.4kW, the 400m^2 of one Module therefore produce 160kW")]
	public float production = 160.0f;

	public override bool UpdateComponentData(string componentName)
	{
		base.UpdateComponentData(componentName);

		if(componentName != null)
		{
			// capacity = GetAttribute("Capacity");
		}
		else
		{
			// capacity = 0.0f;
			// charge = 0.0f;
		}

		return true;
	}
}
