using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryController : MonoBehaviour
{
	[SerializeField] private int startingMoney = 200;
	private int money = 0;
	private Dictionary<GoodManager.State, List<Container>> containers = null;
	private GoodManager goodManager = null;
	private new Rigidbody2D rigidbody = null;

	private void Awake()
	{
		money = startingMoney;

		containers = new Dictionary<GoodManager.State, List<Container>>();
		containers[GoodManager.State.solid] = new List<Container>();
		containers[GoodManager.State.fluid] = new List<Container>();

		rigidbody = GetComponent<Rigidbody2D>();
	}

	private void Start()
	{
		goodManager = GoodManager.GetInstance();
	}

	public bool Deposit(string goodName, uint amount)
	{
		GoodManager.State state = goodManager.GetGood(goodName).state;
		uint freeCapacity = 0;

		foreach(Container container in containers[state])
		{
			if(container.Deposit(goodName, amount))
			{
				return true;
			}
			else
			{
				freeCapacity += container.GetFreeCapacity();
			}
		}

		if(freeCapacity >= amount * (uint) Mathf.CeilToInt(goodManager.GetGood(goodName).volume))
		{
			foreach(Container container in containers[state])
			{
				uint partialAmount = (uint) Mathf.Min((int) container.GetFreeCapacity(), (int) (amount * (uint) Mathf.CeilToInt(goodManager.GetGood(goodName).volume)));
				if(partialAmount > 0 && container.Deposit(goodName, partialAmount))
				{
					amount -= partialAmount;
				}

				if(amount <= 0)
				{
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

	public bool Withdraw(string goodName, uint amount)
	{
		GoodManager.State state = goodManager.GetGood(goodName).state;
		uint availableAmount = 0;

		foreach(Container container in containers[state])
		{
			if(container.Withdraw(goodName, amount))
			{
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
				uint partialAmount = (uint) Mathf.Min((int) container.GetGoodAmount(goodName), (int) amount);
				if(amount > 0 && container.Withdraw(goodName, partialAmount))
				{
					amount -= partialAmount;
				}

				if(amount <= 0)
				{
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

	public uint GetGoodAmount(string goodName)
	{
		GoodManager.State state = goodManager.GetGood(goodName).state;
		uint sum = 0;
		foreach(Container container in containers[state])
		{
			sum += container.GetGoodAmount(goodName);
		}

		return sum;
	}

	public void AddContainer(Container container)
	{
		containers[container.GetState()].Add(container);
		containers[container.GetState()].Sort(delegate (Container x, Container y)
		{
			return (((Vector2)x.GetTransform().localPosition) - rigidbody.centerOfMass).sqrMagnitude - (((Vector2)y.GetTransform().localPosition) - rigidbody.centerOfMass).sqrMagnitude >= 0 ? 1 : -1;
		});
	}

	public void RemoveContainer(Container container)
	{
		containers[container.GetState()].Remove(container);
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
