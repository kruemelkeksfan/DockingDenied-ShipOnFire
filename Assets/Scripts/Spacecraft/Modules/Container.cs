﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Container : Module
{
	[SerializeField] protected GoodManager.State state = GoodManager.State.solid;
	[Tooltip("Cargo Racks or Tank System?")]
	[SerializeField] private GoodManager.ComponentType storageComponentType = GoodManager.ComponentType.CargoRacks;
	protected Storage storage = null;
	protected Dictionary<string, uint> loads = null;
	private float cargoMass = 0.0f;
	private RectTransform volumeBar = null;
	private RectTransform massBar = null;
	private RectTransform inventoryContentPane = null;
	private RectTransform inventoryEntryPrefab = null;
	private GameObject emptyListIndicator = null;

	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position, listenUpdates, listenFixedUpdates);

		storage = new Storage();
		AddComponentSlot(storageComponentType, storage);
		inventoryController.AddContainer(this);

		loads = new Dictionary<string, uint>();

		if(moduleMenu != null)
		{
			RectTransform moduleMenuContent = moduleMenu.GetComponentInChildren<VerticalLayoutGroup>().GetComponent<RectTransform>();
			inventoryContentPane = (RectTransform)moduleMenuContent.GetChild(3);
			inventoryEntryPrefab = InventoryScreenController.GetInstance().GetInventoryEntryPrefab();
			emptyListIndicator = inventoryContentPane.GetChild(0).gameObject;

			moduleMenuContent.GetChild(2).gameObject.SetActive(true);
			inventoryContentPane.gameObject.SetActive(true);
		}

		UpdateModuleMenuButtonText();
	}

	public override void Deconstruct()
	{
		// Dump Contents manually to update their Module Mass in Spacecraft correctly
		List<string> loadKeys = new List<string>(loads.Keys);
		foreach(string loadName in loadKeys)
		{
			Withdraw(loadName, loads[loadName]);
		}

		inventoryController.RemoveContainer(this);
		base.Deconstruct();
	}

	public virtual bool Deposit(string goodName, uint amount)
	{
		GoodManager.Good good = goodManager.GetGood(goodName);
		uint volume = (uint)Mathf.CeilToInt(good.volume) * amount;

		if(volume <= 0)
		{
			return true;
		}

		if(volume <= storage.GetFreeCapacity())
		{
			if(!loads.ContainsKey(goodName))
			{
				loads[goodName] = amount;
			}
			else
			{
				loads[goodName] += amount;
			}

			if(!storage.Deposit(volume))
			{
				Debug.LogWarning("Depositing " + amount + " " + goodName + " in " + storageComponentType + " failed!");
			}

			float goodMass = good.mass * amount;
			cargoMass += goodMass;
			mass += goodMass;
			spacecraft.UpdateMass();

			return true;
		}
		else
		{
			return false;
		}
	}

	public bool Withdraw(string goodName, uint amount)
	{
		GoodManager.Good good = goodManager.GetGood(goodName);
		uint volume = (uint)Mathf.CeilToInt(good.volume) * amount;

		if(volume <= 0)
		{
			return true;
		}

		if(loads.ContainsKey(goodName) && loads[goodName] >= amount)
		{
			loads[goodName] -= amount;
			if(!storage.Withdraw(volume))
			{
				Debug.LogWarning("Withdrawing " + amount + " " + goodName + " from " + storageComponentType + " failed!");
			}

			if(loads[goodName] <= 0)
			{
				loads.Remove(goodName);
			}

			float goodMass = good.mass * amount;
			cargoMass -= goodMass;
			mass -= goodMass;
			spacecraft.UpdateMass();

			return true;
		}
		else
		{
			return false;
		}
	}

	public override void ToggleModuleMenu()
	{
		base.ToggleModuleMenu();

		UpdateModuleMenuInventory();
	}

	public override void UpdateModuleMenuButtonText()
	{
		if(moduleMenu != null && storage != null && inventoryController != null)
		{
			base.UpdateModuleMenuButtonText();

			float capacity = storage.GetCapacity();
			float load = (capacity > MathUtil.EPSILON) ? (storage.GetLoad() / capacity) : 1.0f;
			float cargoMass = GetCargoMass();
			float totalCargoMass = Mathf.Max(inventoryController.GetHeaviestCargoMass(), MathUtil.EPSILON);

			if(volumeBar == null || massBar == null)
			{
				volumeBar = moduleManager.InstantiateStatusBar("Load", moduleManager.GetVolumeColor(), load, statusBarParent);
				massBar = moduleManager.InstantiateStatusBar("Mass", moduleManager.GetMassColor(), (cargoMass / totalCargoMass), statusBarParent);
			}
			else
			{
				moduleManager.UpdateStatusBar(volumeBar, load);
				moduleManager.UpdateStatusBar(massBar, (cargoMass / totalCargoMass));
			}
		}
	}

	public void UpdateModuleMenuInventory()
	{
		if(moduleMenu != null && moduleMenu.activeSelf)
		{
			for(int i = 1; i < inventoryContentPane.childCount; ++i)
			{
				GameObject.Destroy(inventoryContentPane.GetChild(i).gameObject);
			}

			float listHeight = 0.0f;
			bool odd = true;
			foreach(string goodName in loads.Keys)
			{
				GoodManager.Good good = goodManager.GetGood(goodName);

				RectTransform inventoryEntryRectTransform = GameObject.Instantiate<RectTransform>(inventoryEntryPrefab, inventoryContentPane);
				listHeight += inventoryEntryRectTransform.sizeDelta.y;
				inventoryEntryRectTransform.GetChild(0).GetComponent<Text>().text = goodName;
				inventoryEntryRectTransform.GetChild(1).GetComponent<Text>().text = loads[goodName].ToString();
				inventoryEntryRectTransform.GetChild(2).GetComponent<Text>().text = (good.volume * loads[goodName]) + " m3";
				inventoryEntryRectTransform.GetChild(3).GetComponent<Text>().text = (good.mass * loads[goodName]) + " t";

				if(!odd)
				{
					inventoryEntryRectTransform.GetComponent<Image>().enabled = false;
				}
				odd = !odd;
			}

			if(inventoryContentPane.childCount <= 1)
			{
				listHeight += emptyListIndicator.GetComponent<RectTransform>().sizeDelta.y;
				emptyListIndicator.SetActive(true);
			}
			else
			{
				emptyListIndicator.SetActive(false);
			}

			inventoryContentPane.sizeDelta = new Vector2(inventoryContentPane.sizeDelta.x, listHeight);
		}
	}

	public GoodManager.State GetState()
	{
		return state;
	}

	public virtual uint GetFreeCapacity(string goodName)
	{
		return storage.GetFreeCapacity();
	}

	public uint GetGoodAmount(string goodName)
	{
		if(loads.ContainsKey(goodName))
		{
			return loads[goodName];
		}
		else
		{
			return 0;
		}
	}

	public Dictionary<string, uint> GetLoads()
	{
		return loads;
	}

	public float GetCargoMass()
	{
		return cargoMass;
	}
}
