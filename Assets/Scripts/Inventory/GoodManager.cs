﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoodManager : MonoBehaviour
{
	public enum State { solid, fluid };
	public enum ComponentType { undefined,
		BoardComputer, CrewCabin, Radiator,
		AccessControlUnit, HackingUnit,
		CargoRacks, TankSystem,
		Capacitor, PowerCells, EmergencyPowerSupply,
		SolarPanel,
		Breech, Barrel, ForceFieldGenerator,
		IonEngine, FuelPump, HydrogenEngine,
		Teleporter, ConstructionUnit, AssemblerUnit };
	public enum ComponentQuality { crude = 0, basic = 1, good = 2, excellent = 3, legendary = 4 };
	public enum ComponentAttributeScaleMode { IncreaseFull, IncreaseHalf, DecreaseFull, DecreaseHalf, AddOne };

	[Serializable]
	public class Good
	{
		public string goodName = "Unnamed Good";
		[TextArea(3, 5)] public string description = "No Description available";
		[Tooltip("State of Matter of this Good.")]
		public State state = State.solid;
		[Tooltip("Mass in Tons per Unit, 1 Unit is 1m^3 for Goods and 1 Piece for Items.")]
		public float mass = 1.0f;
		[Tooltip("The Room in m^3 this Item takes up in Storage.")]
		public int volume = 1;
		[Tooltip("Factor by which this Cargo contributes to Fires in its Compartment.")]
		public float flammability = 0.0f;
		[Tooltip("The Amount of this Good which is used on Average per Station per Economy Update.")]
		public int consumption = 1;
		[Tooltip("Base Price of this Good.")]
		public int price = 1;
	}

	[Serializable]
	public class ComponentData : Good
	{
		public ComponentType type = ComponentType.undefined;
		public ComponentQuality quality = ComponentQuality.crude;
		public Load[] buildingCosts = { new Load("Steel", 0), new Load("Aluminium", 0),
			new Load("Copper", 0), new Load("Gold", 0), new Load("Silicon", 0) };
		public string[] attributeNames = { };
		public float[] attributeValues = { };
		public ComponentAttributeScaleMode[] attributeScaleModes = { };
	}

	[Serializable]
	public struct Load
	{
		public string goodName;
		public uint amount;

		public Load(string goodName, uint amount)
		{
			this.goodName = goodName;
			this.amount = amount;
		}
	}

	private static GoodManager instance = null;

	[SerializeField] private Good[] goods = { };
	[SerializeField] private ComponentData[] components = { };
	[Tooltip("How much more expensive do Components get with every Quality Level?")]
	[SerializeField] private float componentCostFactor = 2.5f;
	[Tooltip("How much more powerful do Components get with every Quality Level?")]
	[SerializeField] private float componentPowerFactor = 2.0f;
	[Tooltip("An additional Penalty for Module Component Prices, because Resources are dirt cheap due to vast Supply in the early Game.")]
	[SerializeField] private float moduleComponentPriceFactor = 0.2f;
	private Dictionary<string, Good> goodDictionary = null;
	private Dictionary<ComponentType, string> componentNames = null;

	public static GoodManager GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		/*int solidConsumptionSum = 0;
		int solidCount = 0;
		int fluidConsumptionSum = 0;
		int fluidCount = 0;*/

		goodDictionary = new Dictionary<string, Good>(goods.Length + components.Length);
		foreach(Good good in goods)
		{
			goodDictionary.Add(good.goodName, good);

			/*if(good.state == State.solid)
			{
				solidConsumptionSum += good.consumption;
				++solidCount;
			}
			else
			{
				fluidConsumptionSum += good.consumption;
				++fluidCount;
			}*/
		}

		/*Debug.Log("Average Solid Consumption: " + ((float) solidConsumptionSum / (float) solidCount) + ", " + solidCount + " Goods");
		Debug.Log("Average Fluid Consumption: " + ((float) fluidConsumptionSum / (float) fluidCount) + ", " + fluidCount + " Goods");*/

		componentNames = new Dictionary<ComponentType, string>();

		foreach(ComponentData component in components)
		{
			componentNames[component.type] = component.goodName;

			foreach(ComponentQuality quality in Enum.GetValues(typeof(ComponentQuality)))
			{
				ComponentData qualityComponent = new ComponentData();

				int componentCostFactor = Mathf.CeilToInt(Mathf.Pow(this.componentCostFactor, ((int) quality)));
	
				qualityComponent.goodName = component.goodName + " [" + quality.ToString() + "]";
				qualityComponent.description = component.description;
				qualityComponent.state = component.state;
				qualityComponent.mass = component.mass;
				qualityComponent.volume = component.volume;
				qualityComponent.flammability = component.flammability;
				qualityComponent.consumption = component.consumption;
				// TODO: Use Iridium Cost for Price Calculation to enable Moneymaking with Components at the Cost of Iridium
				float price = 0.0f;
				for(int i = 0; i < component.buildingCosts.Length; ++i)
				{
					price += GetGood(component.buildingCosts[i].goodName).price * component.buildingCosts[i].amount;
				}
				qualityComponent.price = Mathf.FloorToInt(price * componentCostFactor * moduleComponentPriceFactor);
				qualityComponent.type = component.type;
				qualityComponent.quality = quality;
				qualityComponent.buildingCosts = new Load[component.buildingCosts.Length];
				for(int i = 0; i < component.buildingCosts.Length; ++i)
				{
					qualityComponent.buildingCosts[i] = new Load(component.buildingCosts[i].goodName,
						(uint) Mathf.RoundToInt(component.buildingCosts[i].amount * componentCostFactor));
				}
				qualityComponent.attributeNames = new string[component.attributeNames.Length];
				qualityComponent.attributeValues = new float[component.attributeValues.Length];
				qualityComponent.attributeScaleModes = new ComponentAttributeScaleMode[component.attributeScaleModes.Length];
				for(int i = 0; i < component.attributeNames.Length; ++i)
				{
					qualityComponent.attributeNames[i] = component.attributeNames[i];
					qualityComponent.attributeScaleModes[i] = component.attributeScaleModes[i];

					if(qualityComponent.attributeScaleModes[i] == ComponentAttributeScaleMode.IncreaseFull)
					{
						qualityComponent.attributeValues[i] = component.attributeValues[i] * Mathf.Pow(this.componentPowerFactor, ((int) quality));
					}
					else if(qualityComponent.attributeScaleModes[i] == ComponentAttributeScaleMode.IncreaseHalf)
					{
						qualityComponent.attributeValues[i] = component.attributeValues[i] * Mathf.Pow(((this.componentPowerFactor - 1.0f) * 0.5f) + 1.0f, ((int) quality));
					}
					else if(qualityComponent.attributeScaleModes[i] == ComponentAttributeScaleMode.DecreaseFull)
					{
						qualityComponent.attributeValues[i] = component.attributeValues[i] / Mathf.Pow(this.componentPowerFactor, ((int) quality));
					}
					else if(qualityComponent.attributeScaleModes[i] == ComponentAttributeScaleMode.DecreaseHalf)
					{
						qualityComponent.attributeValues[i] = component.attributeValues[i] / Mathf.Pow(((this.componentPowerFactor - 1.0f) * 0.5f) + 1.0f, ((int) quality));
					}
					else if(qualityComponent.attributeScaleModes[i] == ComponentAttributeScaleMode.AddOne)
					{
						qualityComponent.attributeValues[i] = component.attributeValues[i] + ((int) quality);
					}
				}

				goodDictionary.Add(qualityComponent.goodName, qualityComponent);

				/*Debug.Log("Name " + qualityComponent.goodName);
				Debug.Log("Desc " + qualityComponent.description);
				Debug.Log("State " + qualityComponent.state);
				Debug.Log("Mass " + qualityComponent.mass);
				Debug.Log("Vol " + qualityComponent.volume);
				Debug.Log("Fire " + qualityComponent.flammability);
				Debug.Log("Cons " + qualityComponent.consumption);
				Debug.Log("$ " + qualityComponent.price);
				Debug.Log("Type " + qualityComponent.type);
				Debug.Log("Quality " + qualityComponent.quality);
				for(int i = 0; i < qualityComponent.buildingCosts.Length; ++i)
				{
					Debug.Log(qualityComponent.buildingCosts[i].goodName + " " + qualityComponent.buildingCosts[i].amount);
				}
				for(int i = 0; i < qualityComponent.attributeValues.Length; ++i)
				{
					Debug.Log(qualityComponent.attributeNames[i] + " " + qualityComponent.attributeValues[i]);
				}*/
			}
		}

		instance = this;
	}

	public Good GetGood(string goodName)
	{
		return goodDictionary[goodName];
	}

	public ComponentData GetComponentData(string componentName)
	{
		return goodDictionary[componentName] as ComponentData;;
	}

	public string GetComponentName(ComponentType type)
	{
		return componentNames[type];
	}

	public string GetRandomComponentName(ComponentQuality quality)
	{
		return components[UnityEngine.Random.Range(0, components.Length)].goodName + " [" + quality.ToString() + "]";
	}

	public Dictionary<string, Good> GetGoodDictionary()
	{
		return goodDictionary;
	}
}
