using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Battery : Module
{
	private EnergyStorage powerCells = null;

	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position, moduleMenu != null, false);

		powerCells = new EnergyStorage();
		AddComponentSlot(GoodManager.ComponentType.PowerCells, powerCells);
		inventoryController.AddBattery(powerCells);

		if(moduleMenu != null)
		{
			// Status
			AddStatusField("Battery Charge", (powerCells.GetCharge().ToString("F2") + "/" + powerCells.GetCapacity().ToString("F2") + " kWh"));
		}
	}

	public override void Deconstruct()
	{
		inventoryController.RemoveBattery(powerCells);

		base.Deconstruct();
	}

	public override void UpdateNotify()
	{
		base.UpdateNotify();

		// Status
		UpdateStatusField("Battery Charge", (powerCells.GetCharge().ToString("F2") + "/" + powerCells.GetCapacity().ToString("F2") + " kWh"));
	}
}
