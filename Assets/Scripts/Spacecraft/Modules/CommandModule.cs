using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CommandModule : Module
{
	[SerializeField] private Transform radarDish = null;
	[SerializeField] private float radarDishSpeed = 1.0f;
	private EnergyStorage emergencyPowerSupply = null;
	private EnergyStorage capacitor = null;
	private Teleporter teleporter = null;

	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position, true, false);

		radarDish.Rotate(0.0f, 0.0f, Random.value * 360.0f);

		emergencyPowerSupply = new EnergyStorage();
		AddComponentSlot(GoodManager.ComponentType.EmergencyPowerSupply, emergencyPowerSupply);
		inventoryController.AddBattery(emergencyPowerSupply);

		capacitor = new EnergyStorage();
		AddComponentSlot(GoodManager.ComponentType.Capacitor, capacitor);
		inventoryController.AddEnergyConsumer(capacitor);

		teleporter = new Teleporter();
		AddComponentSlot(GoodManager.ComponentType.Teleporter, teleporter);
		teleporter.SetCapacitor(capacitor);

		if(moduleMenu != null)
		{
			// Status
			AddStatusField("Battery Charge", emergencyPowerSupply.GetChargeString());
			AddStatusField("Capacitor Charge", capacitor.GetChargeString());
		}
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

		if(moduleMenu != null)
		{
			// Status
			UpdateStatusField("Battery Charge", emergencyPowerSupply.GetChargeString());
			UpdateStatusField("Capacitor Charge", capacitor.GetChargeString());
		}
	}

	public override void ComponentSlotClick(int componentSlotIndex, bool useTeleporter = true)
	{
		// Remove Teleporters and Capacitors by Crew instead of trying to let them dismantle themselves
		GoodManager.ComponentType slotType = GetComponentType(componentSlotIndex);
		base.ComponentSlotClick(componentSlotIndex, !(slotType == GoodManager.ComponentType.Teleporter || slotType == GoodManager.ComponentType.Capacitor));
	}

	public Teleporter GetTeleporter()
	{
		return teleporter;
	}
}
