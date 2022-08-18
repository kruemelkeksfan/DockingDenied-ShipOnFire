using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SolarModule : Module
{
	private EnergyProducer energyProducer = null;

	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position, listenUpdates, listenFixedUpdates);

		energyProducer = new EnergyProducer();
		energyProducer.SetProductionModifier(1.0f);									// TODO: Adjust based on Sun Angle
		AddComponentSlot(GoodManager.ComponentType.SolarPanel, energyProducer);
		inventoryController.AddEnergyProducer(energyProducer);
	}

	public override void Deconstruct()
	{
		inventoryController.RemoveEnergyProducer(energyProducer);

		base.Deconstruct();
	}
}
