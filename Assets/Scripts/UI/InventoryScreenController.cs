using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryScreenController : MonoBehaviour, IListener
{
	[SerializeField] private RectTransform inventoryEntryPrefab = null;
	[SerializeField] private RectTransform inventoryContentPane = null;
	[SerializeField] private GameObject emptyListIndicator = null;
	private MenuController menuController = null;
	private InventoryController localPlayerMainInventory = null;

	private void Start()
	{
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
		Dictionary<string, string> amountSettings = new Dictionary<string, string>(inventoryContentPane.childCount - 1);
		for(int i = 1; i < inventoryContentPane.childCount; ++i)
		{
			Transform child = inventoryContentPane.GetChild(i);
			amountSettings[child.GetChild(0).GetComponent<Text>().text] = child.GetChild(2).GetComponent<InputField>().text;
			GameObject.Destroy(child.gameObject);
		}

		Dictionary<string, uint> inventoryContents = localPlayerMainInventory.GetInventoryContents();
		foreach(string goodName in inventoryContents.Keys)
		{
			RectTransform inventoryEntryRectTransform = GameObject.Instantiate<RectTransform>(inventoryEntryPrefab, inventoryContentPane);
			inventoryEntryRectTransform.GetChild(0).GetComponent<Text>().text = goodName;
			inventoryEntryRectTransform.GetChild(1).GetComponent<Text>().text = inventoryContents[goodName].ToString();
			string localGoodName = goodName;
			InputField localAmountField = inventoryEntryRectTransform.GetChild(2).GetComponent<InputField>();
			if(amountSettings.ContainsKey(goodName))
			{
				localAmountField.text = amountSettings[goodName];
			}
			inventoryEntryRectTransform.GetChild(3).GetComponent<Button>().onClick.AddListener(delegate
			{
				Dump(localGoodName, localAmountField);
			});
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

	public void Dump(string goodName, InputField amountField)
	{
		if(amountField.text.StartsWith("-"))
		{
			amountField.text = amountField.text.Remove(0, 1);
		}
		uint dumpAmount = uint.Parse(amountField.text);
		uint availableAmount = localPlayerMainInventory.GetGoodAmount(goodName);
		if(dumpAmount > availableAmount)
		{
			amountField.text = availableAmount.ToString();
		}
		else
		{
			localPlayerMainInventory.Withdraw(goodName, dumpAmount);
			UpdateInventory();
		}
	}
}
