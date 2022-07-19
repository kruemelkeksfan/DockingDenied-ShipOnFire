using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnergyProducer : ModuleComponent
{
	// Energy Production in kW, 1m^2 of SciFi Solar Panel in this Game is suppossed to produce 0.4kW, the 400m^2 of one Module therefore produce 160kW
	private float maxProduction = 0.0f;
	private float productionModifier = 1.0f;

	public override bool UpdateComponentData(string componentName)
	{
		base.UpdateComponentData(componentName);

		if(componentName != null)
		{
			maxProduction = GetAttribute("Maximum Production");
		}
		else
		{
			maxProduction = 0.0f;
		}

		return true;
	}

	public float GetProduction()
	{
		return maxProduction * productionModifier;
	}

	public void SetProductionModifier(float productionModifier)
	{
		this.productionModifier = productionModifier;
	}
}
