using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// TODO: Somehow implement Dump Confirmation with Enter if Input Field was focussed (so no need to enter amount and then click button, but only enter amount and press enter)

public class InventoryScreenController : MonoBehaviour, IListener
{
	[SerializeField] private GameObject inventoryEntryPrefab = null;
	[SerializeField] private RectTransform inventoryContentPane = null;
	[SerializeField] private GameObject emptyListIndicator = null;
	private InventoryController localPlayerMainInventory = null;

	public void Notify()
	{
		localPlayerMainInventory = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft().GetComponent<InventoryController>();
	}

	public void ToggleInventoryMenu()
	{
		UpdateInventory();
		gameObject.SetActive(!gameObject.activeSelf);
		InputController.SetFlightControls(!gameObject.activeSelf);
	}

	public void UpdateInventory()
	{
		if(localPlayerMainInventory == null)
		{
			SpacecraftManager spacecraftManager = SpacecraftManager.GetInstance();
			localPlayerMainInventory = spacecraftManager.GetLocalPlayerMainSpacecraft().GetComponent<InventoryController>();
			spacecraftManager.AddSpacecraftChangeListener(this);
		}

		Dictionary<string, string> amountSettings = new Dictionary<string, string>(inventoryContentPane.childCount - 1);
		for(int i = 2; i < inventoryContentPane.childCount; ++i)
		{
			Transform child = inventoryContentPane.GetChild(i);
			amountSettings[child.GetChild(0).GetComponent<Text>().text] = child.GetChild(2).GetComponent<InputField>().text;
			GameObject.Destroy(child.gameObject);
		}

		Dictionary<string, uint> inventoryContents = localPlayerMainInventory.GetInventoryContents();

		int j = 0;
		foreach(string goodName in inventoryContents.Keys)
		{
			GameObject inventoryEntry = GameObject.Instantiate<GameObject>(inventoryEntryPrefab, inventoryContentPane);
			RectTransform inventoryEntryRectTransform = inventoryEntry.GetComponent<RectTransform>();
			inventoryEntryRectTransform.anchoredPosition =
				new Vector3(inventoryEntryRectTransform.anchoredPosition.x, -(inventoryEntryRectTransform.rect.height * 0.5f + 20.0f + inventoryEntryRectTransform.rect.height * j));

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

			++j;
		}

		if(inventoryContentPane.childCount <= 2)
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
