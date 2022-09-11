using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Teleporter : ModuleComponent
{
	private float teleportationEnergyCost = 1.0f;
	private float energyCostReduction = 1.0f;

	public override bool UpdateComponentData(string componentName)
	{
		base.UpdateComponentData(componentName);

		teleportationEnergyCost = ComponentManager.GetInstance().GetTeleportationEnergyCost();

		if(componentName != null)
		{
			energyCostReduction = GetAttribute("Energy Cost Reduction");
		}
		else
		{
			energyCostReduction = 1.0f;
		}

		return true;
	}

	public float CalculateTeleportationEnergyCost(Vector2 source, Vector2 destination, float mass)
	{
		return ((destination - source).magnitude * mass * teleportationEnergyCost) / energyCostReduction;
	}

	public bool Teleport(Vector2 source, Vector2 destination, float mass, EnergyStorage capacitor)
	{
		if(IsSet() && capacitor.Discharge(CalculateTeleportationEnergyCost(source, destination, mass)))
		{
			return true;
		}
		else
		{
			return false;
		}
	}
}