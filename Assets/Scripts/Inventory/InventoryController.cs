﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryController : MonoBehaviour, IListener
{
	[SerializeField] private float energyUpdateInterval = 0.05f;
	[SerializeField] private int startingMoney = 200;
	private TimeController timeController = null;
	private HashSet<EnergyProducer> energyProducers = null;
	private List<EnergyStorage> energyConsumers = null;
	private HashSet<EnergyStorage> batteries = null;
	private float transferEnergy = 0.0f;
	private double storedEnergy = 0.0f;
	private double energyCapacity = 0.0f;
	private int money = 0;
	private Dictionary<GoodManager.State, List<Container>> containers = null;
	private InfoController resourceDisplayController = null;
	private GoodManager goodManager = null;
	private new Rigidbody2D rigidbody = null;

	private void Awake()
	{
		energyProducers = new HashSet<EnergyProducer>();
		energyConsumers = new List<EnergyStorage>();
		batteries = new HashSet<EnergyStorage>();

		money = startingMoney;

		containers = new Dictionary<GoodManager.State, List<Container>>(2);
		containers[GoodManager.State.solid] = new List<Container>();
		containers[GoodManager.State.fluid] = new List<Container>();

		rigidbody = GetComponent<Rigidbody2D>();
	}

	private void Start()
	{
		timeController = TimeController.GetInstance();
		goodManager = GoodManager.GetInstance();

		SpacecraftManager spacecraftManager = SpacecraftManager.GetInstance();
		resourceDisplayController = spacecraftManager.GetLocalPlayerMainSpacecraft() == GetComponent<SpacecraftController>() ? InfoController.GetInstance() : null;
		spacecraftManager.AddSpacecraftChangeListener(this);

		timeController.StartCoroutine(UpdateEnergy(), false);
	}

	public void Notify()
	{
		resourceDisplayController = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft() == GetComponent<SpacecraftController>() ? InfoController.GetInstance() : null;
		resourceDisplayController?.UpdateResourceDisplay();
		resourceDisplayController?.UpdateBuildingResourceDisplay();
	}

	public bool TransferEnergy(float energy)
	{
		if(storedEnergy + energy >= 0.0f)
		{
			transferEnergy = energy;
			return true;
		}
		else
		{
			float production = 0.0f;
			foreach(EnergyProducer producer in energyProducers)
			{
				production += producer.GetProduction();
			}

			if(storedEnergy + production + energy >= 0.0f)
			{
				transferEnergy = energy;
				return true;
			}
			else
			{
				return false;
			}
		}
	}

	public bool TransferMoney(int money)
	{
		if(this.money + money >= 0)
		{
			this.money += money;

			resourceDisplayController?.UpdateResourceDisplay();
			return true;
		}
		else
		{
			return false;
		}
	}

	public bool Deposit(string goodName, uint amount)
	{
		if(amount == 0)
		{
			return true;
		}

		if(goodManager == null)
		{
			goodManager = GoodManager.GetInstance();
		}

		GoodManager.State state = goodManager.GetGood(goodName).state;
		uint freeCapacity = 0;

		foreach(Container container in containers[state])
		{
			if(container.Deposit(goodName, amount))
			{
				resourceDisplayController?.UpdateResourceDisplay();
				resourceDisplayController?.UpdateBuildingResourceDisplay();
				return true;
			}
			else
			{
				freeCapacity += container.GetFreeCapacity(goodName);
			}
		}

		if(freeCapacity >= amount * (uint)Mathf.CeilToInt(goodManager.GetGood(goodName).volume))
		{
			foreach(Container container in containers[state])
			{
				uint partialAmount = (uint)Mathf.Min((int)(container.GetFreeCapacity(goodName) / goodManager.GetGood(goodName).volume), (int)amount);
				if(partialAmount > 0 && container.Deposit(goodName, partialAmount))
				{
					amount -= partialAmount;
				}

				if(amount <= 0)
				{
					resourceDisplayController?.UpdateResourceDisplay();
					resourceDisplayController?.UpdateBuildingResourceDisplay();
					return true;
				}
			}

			Debug.LogError("Could not completely deposit a Load of " + goodName + " in Inventory of " + gameObject + ", although enough Space should have been available!");
			return false;       // Some Cargo would have been stored already, so avoid storing only a Part but subtracting full Costs for something
		}
		else
		{
			return false;
		}
	}

	// Works only for Solids
	public bool DepositBulk(GoodManager.Load[] goods)
	{
		if(goods.Length <= 0)
		{
			return true;
		}

		uint sum = 0;
		foreach(GoodManager.Load good in goods)
		{
			if(goodManager.GetGood(good.goodName).state == GoodManager.State.solid)
			{
				sum += good.amount;
			}
			else
			{
				return false;
			}
		}
		if(sum > GetFreeCapacity(goodManager.GetGood(goods[0].goodName)))
		{
			InfoController.GetInstance().AddMessage("Not enough Storage Capacity available in this Spacecrafts Inventory!", true);
			return false;
		}

		foreach(GoodManager.Load good in goods)
		{
			if(!Deposit(good.goodName, good.amount))
			{
				Debug.LogWarning("Goods could not be deposited in Inventory, although they should be!");
				return false;
			}
		}

		return true;
	}

	public bool Withdraw(string goodName, uint amount)
	{
		if(amount == 0)
		{
			return true;
		}

		if(goodManager == null)
		{
			goodManager = GoodManager.GetInstance();
		}

		GoodManager.State state = goodManager.GetGood(goodName).state;
		uint availableAmount = 0;

		foreach(Container container in containers[state])
		{
			if(container.Withdraw(goodName, amount))
			{
				resourceDisplayController?.UpdateResourceDisplay();
				resourceDisplayController?.UpdateBuildingResourceDisplay();
				return true;
			}
			else
			{
				availableAmount += container.GetGoodAmount(goodName);
			}
		}

		if(availableAmount >= amount)
		{
			foreach(Container container in containers[state])
			{
				uint partialAmount = (uint)Mathf.Min((int)container.GetGoodAmount(goodName), (int)amount);
				if(amount > 0 && container.Withdraw(goodName, partialAmount))
				{
					amount -= partialAmount;
				}

				if(amount <= 0)
				{
					resourceDisplayController?.UpdateResourceDisplay();
					resourceDisplayController?.UpdateBuildingResourceDisplay();
					return true;
				}
			}

			Debug.LogError("Could not completely withdraw a Load of " + goodName + " from Inventory of " + gameObject + ", although enough Cargo should have been available!");
			return true;        // Some Cargo would have been deleted already, so avoid deleting partial Costs of something and then give nothing in return
		}
		else
		{
			return false;
		}
	}

	public bool WithdrawBulk(GoodManager.Load[] goods)
	{
		foreach(GoodManager.Load good in goods)
		{
			if(GetGoodAmount(good.goodName) < good.amount)
			{
				InfoController.GetInstance().AddMessage("Not enough " + good.goodName + " available in this Spacecrafts Inventory!", false);
				return false;
			}
		}

		foreach(GoodManager.Load good in goods)
		{
			if(!Withdraw(good.goodName, good.amount))
			{
				Debug.LogWarning("Goods could not be withdrawn from Inventory, although they should be!");
				return false;
			}
		}

		return true;
	}

	private IEnumerator<float> UpdateEnergy()
	{
		double lastUpdate = 0.0f;
		int consumerIndex = 0;
		while(true)
		{
			yield return energyUpdateInterval;

			float energy = transferEnergy;
			double deltaTimeHours = (timeController.GetTime() - lastUpdate) / 3600.0;
			lastUpdate = timeController.GetTime();
			foreach(EnergyProducer producer in energyProducers)
			{
				energy += (float)(producer.GetProduction() * deltaTimeHours);
			}

			foreach(EnergyStorage battery in batteries)
			{
				energy += battery.DischargeAll();
			}

			int consumerCounter = 0;
			while(energy > 0.0f && consumerCounter < energyConsumers.Count)
			{
				consumerIndex = (consumerIndex + 1) % energyConsumers.Count;
				energy = energyConsumers[consumerIndex].Charge(energy);

				++consumerCounter;
			}

			storedEnergy = 0.0f;
			energyCapacity = 0.0f;
			foreach(EnergyStorage battery in batteries)
			{
				if(energy > 0.0f)
				{
					energy = battery.Charge(energy);
				}

				storedEnergy += battery.GetCharge();
				energyCapacity += battery.GetCapacity();
			}

			if(energy < 0.0f)
			{
				// TODO: This could happen because Energy Production changed between TransferEnergy()-Call and here
				Debug.LogWarning("Negative Energy " + energy + " at the End of Energy Distribution Cycle of " + gameObject.name + "!");
			}

			transferEnergy = 0.0f;

			resourceDisplayController?.UpdateResourceDisplay();
		}
	}

	public double GetEnergy()
	{
		return storedEnergy;
	}

	public string GetEnergyString(bool showTotal = false)
	{
		return storedEnergy.ToString("F2") + (showTotal ? ("/" + energyCapacity.ToString("F2")) : "") + " kWh";
	}

	public int GetMoney()
	{
		return money;
	}

	public uint GetFreeCapacity(GoodManager.Good good)
	{
		uint capacity = 0;
		foreach(Container container in containers[good.state])
		{
			capacity += container.GetFreeCapacity(good.goodName);
		}
		return capacity;
	}

	public uint GetGoodAmount(string goodName)
	{
		if(goodManager == null)
		{
			goodManager = GoodManager.GetInstance();
		}

		GoodManager.State state = goodManager.GetGood(goodName).state;
		uint sum = 0;
		foreach(Container container in containers[state])
		{
			sum += container.GetGoodAmount(goodName);
		}

		return sum;
	}

	public void AddEnergyProducer(EnergyProducer producer)
	{
		energyProducers.Add(producer);
	}

	public void RemoveEnergyProducer(EnergyProducer producer)
	{
		energyProducers.Remove(producer);
	}

	public void AddEnergyConsumer(EnergyStorage consumer)
	{
		energyConsumers.Add(consumer);
	}

	public void RemoveEnergyConsumer(EnergyStorage consumer)
	{
		energyConsumers.Remove(consumer);
	}

	public void AddBattery(EnergyStorage battery)
	{
		batteries.Add(battery);
	}

	public void RemoveBattery(EnergyStorage battery)
	{
		batteries.Remove(battery);
	}

	public void AddContainer(Container container)
	{
		containers[container.GetState()].Add(container);
		containers[container.GetState()].Sort(delegate (Container x, Container y)
		{
			return (((Vector2)x.GetTransform().localPosition) - rigidbody.centerOfMass).sqrMagnitude - (((Vector2)y.GetTransform().localPosition) - rigidbody.centerOfMass).sqrMagnitude >= 0 ? 1 : -1;
		});
		resourceDisplayController?.UpdateResourceDisplay();
		resourceDisplayController?.UpdateBuildingResourceDisplay();
	}

	public void RemoveContainer(Container container)
	{
		containers[container.GetState()].Remove(container);
		resourceDisplayController?.UpdateResourceDisplay();
		resourceDisplayController?.UpdateBuildingResourceDisplay();
	}

	public Dictionary<string, uint> GetInventoryContents()
	{
		Dictionary<string, uint> inventoryContents = new Dictionary<string, uint>();
		foreach(List<Container> containerList in containers.Values)
		{
			foreach(Container container in containerList)
			{
				foreach(KeyValuePair<string, uint> goodAmount in container.GetLoads())
				{
					if(!inventoryContents.ContainsKey(goodAmount.Key))
					{
						inventoryContents[goodAmount.Key] = goodAmount.Value;
					}
					else
					{
						inventoryContents[goodAmount.Key] += goodAmount.Value;
					}
				}
			}
		}

		return inventoryContents;
	}

	public List<GoodManager.ComponentData> GetModuleComponentsInInventory(GoodManager.ComponentType componentType)
	{
		List<GoodManager.ComponentData> components = new List<GoodManager.ComponentData>();

		Dictionary<string, uint> inventoryContents = GetInventoryContents();
		foreach(string goodName in inventoryContents.Keys)
		{
			GoodManager.Good good = goodManager.GetGood(goodName);
			GoodManager.ComponentData componentData = null;
			if((componentData = good as GoodManager.ComponentData) != null && componentData.type == componentType)
			{
				for(int i = 0; i < inventoryContents[goodName]; ++i)
				{
					components.Add(componentData);
				}
			}
		}

		return components;
	}
}
