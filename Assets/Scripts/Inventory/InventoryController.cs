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
	private float storedEnergy = 0.0f;
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
		resourceDisplayController = spacecraftManager.GetLocalPlayerMainSpacecraft() == GetComponent<Spacecraft>() ? InfoController.GetInstance() : null;
		spacecraftManager.AddSpacecraftChangeListener(this);

		StartCoroutine(UpdateEnergy());

		resourceDisplayController?.UpdateResourceDisplays();
	}

	public void Notify()
	{
		resourceDisplayController = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft() == GetComponent<Spacecraft>() ? InfoController.GetInstance() : null;
		resourceDisplayController?.UpdateResourceDisplays();
	}

	public bool TransferMoney(int money)
	{
		if(this.money + money >= 0)
		{
			this.money += money;

			resourceDisplayController?.UpdateResourceDisplays();
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
				resourceDisplayController?.UpdateResourceDisplays();
				return true;
			}
			else
			{
				freeCapacity += container.GetFreeCapacity();
			}
		}

		if(freeCapacity >= amount * (uint)Mathf.CeilToInt(goodManager.GetGood(goodName).volume))
		{
			foreach(Container container in containers[state])
			{
				uint partialAmount = (uint)Mathf.Min((int)container.GetFreeCapacity(), (int)(amount * (uint)Mathf.CeilToInt(goodManager.GetGood(goodName).volume)));
				if(partialAmount > 0 && container.Deposit(goodName, partialAmount))
				{
					amount -= partialAmount;
				}

				if(amount <= 0)
				{
					resourceDisplayController?.UpdateResourceDisplays();
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

	public bool DepositBulk(GoodManager.Load[] goods)
	{
		uint solidSum = 0;
		uint fluidSum = 0;
		foreach(GoodManager.Load good in goods)
		{
			if(goodManager.GetGood(good.goodName).state == GoodManager.State.solid)
			{
				solidSum += good.amount;
			}
			else
			{
				fluidSum += good.amount;
			}
		}
		if(solidSum > GetFreeCapacity(GoodManager.State.solid))
		{
			InfoController.GetInstance().AddMessage("Not enough Solid Storage Capacity available in this Spacecrafts Inventory!");
			return false;
		}
		else if(fluidSum > GetFreeCapacity(GoodManager.State.fluid))
		{
			InfoController.GetInstance().AddMessage("Not enough Fluid Storage Capacity available in this Spacecrafts Inventory!");
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
				resourceDisplayController?.UpdateResourceDisplays();
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
					resourceDisplayController?.UpdateResourceDisplays();
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

			float energy = 0.0f;
			float deltaTime = Time.time - lastUpdate;
			lastUpdate = Time.time;
			foreach(EnergyProducer producer in energyProducers)
			{
				energy += producer.production * deltaTime;
			}

			int consumerCounter = 0;
			while(energy > 0.0f && consumerCounter < energyConsumers.Count)
			{
				energy = energyConsumers[consumerIndex].Charge(energy);
				if(energy <= 0.0f)
				{
					foreach(Capacitor battery in batteries)
					{
						energy += battery.DischargeAll();
					}
					energy = energyConsumers[consumerIndex].Charge(energy);
				}

				++consumerCounter;
				consumerIndex = (consumerIndex + 1) % energyConsumers.Count;
			}

			storedEnergy = 0.0f;
			if(energy > 0.0f)
			{
				foreach(Capacitor battery in batteries)
				{
					if(energy <= 0.0f)
					{
						break;
					}

					energy = battery.Charge(energy);
					storedEnergy += battery.charge;
				}
			}

			resourceDisplayController?.UpdateResourceDisplays();
		}
	}

	public float GetEnergy()
	{
		return storedEnergy;
	}

	public int GetMoney()
	{
		return money;
	}

	public uint GetFreeCapacity(GoodManager.State state)
	{
		uint capacity = 0;
		foreach(Container container in containers[state])
		{
			capacity += container.GetFreeCapacity();
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
		resourceDisplayController?.UpdateResourceDisplays();
	}

	public void RemoveContainer(Container container)
	{
		containers[container.GetState()].Remove(container);
		resourceDisplayController?.UpdateResourceDisplays();
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
