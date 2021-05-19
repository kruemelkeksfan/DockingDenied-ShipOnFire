using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SolarModule : Module
{
	[SerializeField] private EnergyProducer energyProducer = null;
	private InventoryController inventoryController = null;

	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position, listenUpdates, listenFixedUpdates);

		inventoryController = spacecraft.GetInventoryController();
		inventoryController.AddEnergyProducer(energyProducer);
	}

	public override void Deconstruct()
	{
		inventoryController.RemoveEnergyProducer(energyProducer);

		base.Deconstruct();
	}
}
