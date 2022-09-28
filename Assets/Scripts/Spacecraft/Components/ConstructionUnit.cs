using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConstructionUnit : ModuleComponent
{
	private float energyCost = float.MaxValue;
	private EnergyStorage capacitor = null;
	private Teleporter teleporter = null;
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

	public int Construct(Vector2 constructorPosition, Vector2 targetPosition, GoodManager.Load[] constructionCosts)
	{
		// TODO: Limit buildable Blueprints by Construction Unit Quality?

		if(!teleporter.IsSet())
		{
			lastEnergyCost = 0.0f;
			return 1;
		}
		if(!capacitor.IsSet())
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

		float energyCost = this.energyCost * mass;
		float charge = capacitor.GetCharge();
		if(charge >= energyCost)
		{
			if(!teleporter.Teleport(constructorPosition, targetPosition, mass))
			{
				return 4;
			}

			lastEnergyCost = energyCost;
			if(capacitor.Discharge(energyCost))
			{
				return 0;
			}
			else
			{
				Debug.LogWarning("Capacitor does not have " + energyCost + " kWh for Construction, although it should have " + charge + " kWh!");
				teleporter.Rollback();
				return 5;
			}
		}
		else
		{
			InfoController.GetInstance().AddMessage(energyCost.ToString("F2") + " kWh needed for Construction, but only "
				+ charge.ToString("F2") + " are available in the Capacitor!", false);
			lastEnergyCost = 0.0f;
			return 5;
		}
	}

	public void Rollback(EnergyStorage constructionCapacitor)
	{
		constructionCapacitor.Charge(lastEnergyCost);
	}

	public void SetComponents(EnergyStorage capacitor, Teleporter teleporter)
	{
		this.capacitor = capacitor;
		this.teleporter = teleporter;
	}
}