using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryScreenController : MonoBehaviour, IListener
{
	private static InventoryScreenController instance = null;

	[SerializeField] private RectTransform inventoryEntryPrefab = null;
	[SerializeField] private RectTransform inventoryContentPane = null;
	[SerializeField] private GameObject emptyListIndicator = null;
	private GoodManager goodManager = null;
	private MenuController menuController = null;
	private InventoryController localPlayerMainInventory = null;
	private int currentCategory = 0;

	public static InventoryScreenController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		instance = this;
	}

	private void Start()
	{
		goodManager = GoodManager.GetInstance();
		menuController = MenuController.GetInstance();

		SpacecraftManager.GetInstance().AddSpacecraftChangeListener(this);
		Notify();

		gameObject.SetActive(false);
	}

	public void Notify()
	{
		localPlayerMainInventory = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft().GetComponent<InventoryController>();
	}

	public void ToggleInventoryMenu()
	{
		gameObject.SetActive(!gameObject.activeSelf);
		menuController.UpdateFlightControls();
		if(gameObject.activeSelf)
		{
			UpdateInventory();
		}
	}

	public void UpdateInventory()
	{
		if(goodManager == null)
		{
			goodManager = GoodManager.GetInstance();
		}

		for(int i = 1; i < inventoryContentPane.childCount; ++i)
		{
			GameObject.Destroy(inventoryContentPane.GetChild(i).gameObject);
		}

		Dictionary<string, uint> inventoryContents = localPlayerMainInventory.GetInventoryContents();
		bool odd = true;
		foreach(string goodName in inventoryContents.Keys)
		{
			GoodManager.Good good = goodManager.GetGood(goodName);

			if((currentCategory == 0 && good.state == GoodManager.State.solid && !(good is GoodManager.ComponentData))
				|| (currentCategory == 1 && good.state == GoodManager.State.fluid)
				|| (currentCategory == 2 && (good is GoodManager.ComponentData)))
			{
				RectTransform inventoryEntryRectTransform = GameObject.Instantiate<RectTransform>(inventoryEntryPrefab, inventoryContentPane);
				inventoryEntryRectTransform.GetChild(0).GetComponent<Text>().text = goodName;
				inventoryEntryRectTransform.GetChild(1).GetComponent<Text>().text = inventoryContents[goodName].ToString();
				inventoryEntryRectTransform.GetChild(2).GetComponent<Text>().text = (good.volume * inventoryContents[goodName]) + " m3";
				inventoryEntryRectTransform.GetChild(3).GetComponent<Text>().text = (good.mass * inventoryContents[goodName]) + " t";

				if(!odd)
				{
					inventoryEntryRectTransform.GetComponent<Image>().enabled = false;
				}
				odd = !odd;
			}
		}

		if(inventoryContentPane.childCount <= 1)
		{
			emptyListIndicator.SetActive(true);
		}
		else
		{
			emptyListIndicator.SetActive(false);
		}
	}

	public RectTransform GetInventoryEntryPrefab()
	{
		return inventoryEntryPrefab;
	}

	public void SetCategory(int categoryId)
	{
		currentCategory = categoryId;
		UpdateInventory();
	}
}
