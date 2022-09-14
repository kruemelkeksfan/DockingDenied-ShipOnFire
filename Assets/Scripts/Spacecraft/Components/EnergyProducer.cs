using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnergyProducer : ModuleComponent
{
	private float production = 0.0f;
	private float productionModifier = 1.0f;

	public override bool UpdateComponentData(string componentName)
	{
		base.UpdateComponentData(componentName);

		if(componentName != null)
		{
			production = GetAttribute("Energy Production");
		}
		else
		{
			production = 0.0f;
		}

		return true;
	}

	public float GetProduction()
	{
		return production * productionModifier;
	}

	public void SetProductionModifier(float productionModifier)
	{
		this.productionModifier = productionModifier;
	}
}
