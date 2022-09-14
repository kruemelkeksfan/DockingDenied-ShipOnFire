using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConstructionUnit : ModuleComponent
{
	private float constructionEnergyCost = 1.0f;
	private float energyCostReduction = 1.0f;
	private float lastEnergyCost = 0.0f;

	public override bool UpdateComponentData(string componentName)
	{
		base.UpdateComponentData(componentName);

		constructionEnergyCost = ComponentManager.GetInstance().GetConstructionEnergyCost();

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

	public int Construct(Vector2 constructorPosition, Vector2 targetPosition, GoodManager.Load[] constructionCosts,
		Teleporter constructorTeleporter, EnergyStorage constructorCapacitor)
	{
		// TODO: Limit buildable Blueprints by Construction Unit Quality?

		if(!constructorTeleporter.IsSet())
		{
			lastEnergyCost = 0.0f;
			return 1;
		}
		if(!constructorCapacitor.IsSet())
		{
			lastEnergyCost = 0.0f;
			return 2;
		}
		if(!IsSet())
		{
			lastEnergyCost = 0.0f;
			return 3;
		}

		GoodManager goodManager = GoodManager.GetInstance();
		float mass = 0.0f;
		foreach(GoodManager.Load cost in constructionCosts)
		{
			mass += goodManager.GetGood(cost.goodName).mass * cost.amount;
		}

		float energyCost = constructorTeleporter.CalculateTeleportationEnergyCost(constructorPosition, targetPosition, mass);
		energyCost += (constructionEnergyCost * mass) / energyCostReduction;

		float charge = constructorCapacitor.GetCharge();
		if(charge >= energyCost)
		{
			lastEnergyCost = energyCost;
			if(constructorTeleporter.Teleport(constructorPosition, targetPosition, mass, constructorCapacitor)
				&& constructorCapacitor.Discharge(constructionEnergyCost / energyCostReduction))
			{
				return 0;
			}
			else
			{
				Debug.LogWarning("Capacitor does not have " + energyCost + " kWh for Construction, although it should have " + charge + " kWh!");
				return 4;
			}
		}
		else
		{
			InfoController.GetInstance().AddMessage(energyCost.ToString("F2") + " kWh needed for Construction, but only "
				+ charge.ToString("F2") + " are available in the Capacitor!", false);
			lastEnergyCost = 0.0f;
			return 4;
		}
	}

	public void Rollback(EnergyStorage constructionCapacitor)
	{
		constructionCapacitor.Charge(lastEnergyCost);
	}
}