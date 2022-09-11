using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CommandModule : Module
{
	[SerializeField] private Transform radarDish = null;
	[SerializeField] private float radarDishSpeed = 1.0f;
	private EnergyStorage emergencyPowerSupply = null;

	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position, true, false);

		radarDish.Rotate(0.0f, 0.0f, Random.value * 360.0f);

		emergencyPowerSupply = new EnergyStorage();
		AddComponentSlot(GoodManager.ComponentType.EmergencyPowerSupply, emergencyPowerSupply);
		inventoryController.AddBattery(emergencyPowerSupply);
	}

	public override void Deconstruct()
	{
		inventoryController.RemoveBattery(emergencyPowerSupply);

		base.Deconstruct();
	}

	public override void UpdateNotify()
	{
		base.UpdateNotify();

		radarDish.Rotate(0.0f, 0.0f, radarDishSpeed * timeController.GetDeltaTime());
	}
}
