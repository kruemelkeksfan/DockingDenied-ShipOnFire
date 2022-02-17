using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryController : MonoBehaviour, IListener
{
	private static WaitForSeconds waitForEnergyUpdateInterval = null;

	[SerializeField] private float energyUpdateInterval = 0.05f;
	[SerializeField] private int startingMoney = 200;
	private HashSet<EnergyProducer> energyProducers = null;
	private List<Capacitor> energyConsumers = null;
	private HashSet<Capacitor> batteries = null;
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
		if(waitForEnergyUpdateInterval == null)
		{
			waitForEnergyUpdateInterval = new WaitForSeconds(energyUpdateInterval);
		}

		energyProducers = new HashSet<EnergyProducer>();
		energyConsumers = new List<Capacitor>();
		batteries = new HashSet<Capacitor>();

		money = startingMoney;

		containers = new Dictionary<GoodManager.State, List<Container>>(2);
		containers[GoodManager.State.solid] = new List<Container>();
		containers[GoodManager.State.fluid] = new List<Container>();

		rigidbody = GetComponent<Rigidbody2D>();
	}

	private void Start()
	{
		goodManager = GoodManager.GetInstance();

		SpacecraftManager spacecraftManager = SpacecraftManager.GetInstance();
		resourceDisplayController = spacecraftManager.GetLocalPlayerMainSpacecraft() == GetComponent<SpacecraftController>() ? InfoController.GetInstance() : null;
		spacecraftManager.AddSpacecraftChangeListener(this);

		StartCoroutine(UpdateEnergy());
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
				production += producer.production;
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
				uint partialAmount = (uint)Mathf.Min((int)container.GetFreeCapacity(goodName), (int)(amount * (uint)Mathf.CeilToInt(goodManager.GetGood(goodName).volume)));
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
			InfoController.GetInstance().AddMessage("Not enough Storage Capacity available in this Spacecrafts Inventory!");
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
				InfoController.GetInstance().AddMessage("Not enough " + good.goodName + " available in this Spacecrafts Inventory!");
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

	private IEnumerator UpdateEnergy()
	{
		float lastUpdate = 0.0f;
		int consumerIndex = 0;
		while(true)
		{
			yield return waitForEnergyUpdateInterval;

			float energy = transferEnergy;
			float deltaTime = Time.time - lastUpdate;
			lastUpdate = Time.time;
			foreach(EnergyProducer producer in energyProducers)
			{
				energy += producer.production * deltaTime;
			}

			foreach(Capacitor battery in batteries)
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
			foreach(Capacitor battery in batteries)
			{
				if(energy > 0.0f)
				{
					energy = battery.Charge(energy);
				}

				storedEnergy += battery.charge;
				energyCapacity += battery.capacity;
			}

			if(energy < 0.0f)
			{
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

	public string GetEnergyKWH(bool showTotal = false)
	{
		return (storedEnergy * 0.00027777).ToString("F2") + (showTotal ? ("/" + (energyCapacity * 0.00027777).ToString("F2") + "kWh") : "");	// 0.00027777 is the approximate Conversion Factor from kWs to kWh, bc (1 / 60) / 60 == 1 * 0.00027777
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

	public void AddEnergyConsumer(Capacitor consumer)
	{
		energyConsumers.Add(consumer);
	}

	public void RemoveEnergyConsumer(Capacitor consumer)
	{
		energyConsumers.Remove(consumer);
	}

	public void AddBattery(Capacitor battery)
	{
		batteries.Add(battery);
	}

	public void RemoveBattery(Capacitor battery)
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
}
