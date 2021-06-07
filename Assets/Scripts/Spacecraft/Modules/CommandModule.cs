using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CommandModule : Module
{
	[SerializeField] private Capacitor capacitor = null;
	private InventoryController inventoryController = null;

	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position, listenUpdates, listenFixedUpdates);

		inventoryController = spacecraft.GetInventoryController();
		inventoryController.AddBattery(capacitor);
	}

	public override void Deconstruct()
	{
		inventoryController.RemoveBattery(capacitor);

		base.Deconstruct();
	}
}
