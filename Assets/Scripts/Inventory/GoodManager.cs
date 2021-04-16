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
		[TextArea(3, 5)] public string decription;
		[Tooltip("State of Matter of this Good.")]
		public State state;
		[Tooltip("Mass in Tons per Unit, 1 Unit is 1m^3 for Goods and 1 Piece for Items.")]
		public float mass;
		[Tooltip("The Room in m^3 this Item takes up in Storage.")]
		public int volume;
		[Tooltip("Factor by which this Cargo contributes to Fires in its Compartment.")]
		public float flammability;
		[Tooltip("Associated Item Component, should have an empty Type Field for Goods which are no Items.")]
		public Item item;
	}

	[Serializable]
    public struct Item
	{
		public string type;
		public string[] attributeNames;
		public float[] attributeValues;
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
		goodDictionary = new Dictionary<string, Good>();
		foreach(Good good in goods)
		{
			goodDictionary.Add(good.goodName, good);
		}

		instance = this;
	}

	public Good GetGood(string goodName)
	{
		return goodDictionary[goodName];
	}
}
