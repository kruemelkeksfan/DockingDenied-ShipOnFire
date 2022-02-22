using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoodManager : MonoBehaviour
{
	// Strings instead of Enums for Types and Attributes for easier Modability (could be defined in JSON for example)
	public enum State { solid, fluid };

	[Serializable]
    public struct Good
	{
		public string goodName;
		[TextArea(3, 5)] public string description;
		[Tooltip("State of Matter of this Good.")]
		public State state;
		[Tooltip("Mass in Tons per Unit, 1 Unit is 1m^3 for Goods and 1 Piece for Items.")]
		public float mass;
		[Tooltip("The Room in m^3 this Item takes up in Storage.")]
		public int volume;
		[Tooltip("Factor by which this Cargo contributes to Fires in its Compartment.")]
		public float flammability;
		[Tooltip("The Amount of this Good which is used on Average per Station per Economy Update.")]
		public int consumption;
		[Tooltip("Base Price of this Good.")]
		public int price;
		[Tooltip("Associated Item Component, should have an empty Type Field for Goods which are no Items.")]
		public Item item;
	}

	[Serializable]
    public struct Item
	{
		public string type;
		public Attribute[] attributes;
	}

	[Serializable]
    public struct Attribute
	{
		public string attributeName;
		public float value;
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
	private Dictionary<string, Good> goodDictionary = null;

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

		goodDictionary = new Dictionary<string, Good>();
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

		instance = this;
	}

	public Good GetGood(string goodName)
	{
		return goodDictionary[goodName];
	}

	public Dictionary<string, Good> GetGoodDictionary()
	{
		return goodDictionary;
	}
}
