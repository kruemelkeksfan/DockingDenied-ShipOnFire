using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CrewCabin : ModuleComponent
{
	private int crewCapacity = 0;
	// TODO: Enable actual Crew
	// private List<CrewMember> crew = null;

	public override bool UpdateComponentData(string componentName)
	{
		/*if(crew.Count > 0)
		{
			return false;
		}*/

		base.UpdateComponentData(componentName);

		if(componentName != null)
		{
			crewCapacity = Mathf.RoundToInt(GetAttribute("Crew Capacity"));
		}
		else
		{
			crewCapacity = 0;
		}

		// crew.Clear();

		return true;
	}
}
