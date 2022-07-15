using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CommandModule : Module
{
	[SerializeField] private EnergyStorage emergencyPowerSupply = null;
	private InventoryController inventoryController = null;

	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position, listenUpdates, listenFixedUpdates);

		inventoryController = spacecraft.GetInventoryController();
		inventoryController.AddBattery(emergencyPowerSupply);
	}

	public override void Deconstruct()
	{
		inventoryController.RemoveBattery(emergencyPowerSupply);

		base.Deconstruct();
	}
}
