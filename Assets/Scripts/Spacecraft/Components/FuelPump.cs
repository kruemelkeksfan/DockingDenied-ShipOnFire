using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FuelPump : ModuleComponent
{
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