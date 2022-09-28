using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Teleporter : ModuleComponent
{
	private float energyCost = 1.0f;
	private EnergyStorage capacitor = null;
	private float lastEnergyCost = 0.0f;

	public override bool UpdateComponentData(string componentName)
	{
		base.UpdateComponentData(componentName);

		if(componentName != null)
		{
			energyCost = GetAttribute("Energy Cost");
		}
		else
		{
			energyCost = float.MaxValue;
		}

		return true;
	}

	// Calculate Teleportation Energy Cost per Kilometer
	public float CalculateTeleportationEnergyCost(Vector2 source, Vector2 destination, float mass)
	{
		return (Mathf.Ceil((destination - source).magnitude / 1000.0f) * mass * energyCost);
	}

	public bool Teleport(Vector2 source, Vector2 destination, float mass)
	{
		float energyCost = CalculateTeleportationEnergyCost(source, destination, mass);

		if(IsSet() && capacitor.Discharge(energyCost))
		{
			lastEnergyCost = energyCost;
			return true;
		}
		else
		{
			lastEnergyCost = 0.0f;
			return false;
		}
	}

	public void Rollback()
	{
		capacitor.Charge(lastEnergyCost);
	}

	public EnergyStorage GetCapacitor()
	{
		return capacitor;
	}

	public void SetCapacitor(EnergyStorage capacitor)
	{
		this.capacitor = capacitor;
	}
}