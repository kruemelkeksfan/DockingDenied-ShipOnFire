using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class InventoryEntryController : DraggableEntry
{
	private Container sourceContainer = null;
	private string goodName = "undefined";
	private uint amount = 0;

	protected override bool Transfer(List<RaycastResult> hoveredGameObjectResults)
	{
		foreach(RaycastResult hoveredObject in hoveredGameObjectResults)
		{
			if(hoveredObject.gameObject.activeInHierarchy)
			{
				Container destinationContainer = hoveredObject.gameObject.GetComponent<ContainerMenuController>()?.GetContainer();
				if(destinationContainer != null)
				{
					amount = (uint)Mathf.Min((int)amount, (int)destinationContainer.GetFreeCapacity(goodName));

					if(sourceContainer.Withdraw(goodName, amount))
					{
						if(destinationContainer.Deposit(goodName, amount))
						{
							sourceContainer.UpdateModuleStatus();
							sourceContainer.UpdateModuleMenuInventory();
							destinationContainer.UpdateModuleStatus();
							destinationContainer.UpdateModuleMenuInventory();
							return true;
						}
						else
						{
							if(!sourceContainer.Deposit(goodName, amount))
							{
								Debug.LogWarning("Inventory Transfer of " + amount + " " + goodName
									+ " from " + sourceContainer.GetCustomModuleName() + " to " + destinationContainer.GetCustomModuleName() + " could not be rolled back correctly!");
							}

							Debug.LogWarning("Only " + destinationContainer.GetFreeCapacity(goodName) + "/" + amount + " " + goodName
							+ " can be stored in " + destinationContainer.GetCustomModuleName() + "!");

							sourceContainer.UpdateModuleStatus();
							sourceContainer.UpdateModuleMenuInventory();
							return false;
						}
					}
					else
					{
						Debug.LogWarning("Only " + sourceContainer.GetGoodAmount(goodName) + "/" + amount + " " + goodName
							+ " available in " + sourceContainer.GetCustomModuleName() + "!");

						sourceContainer.UpdateModuleStatus();
						sourceContainer.UpdateModuleMenuInventory();
						return false;
					}
				}
			}
		}

		sourceContainer.UpdateModuleStatus();
		sourceContainer.UpdateModuleMenuInventory();
		Dump();
		return false;
	}

	private void Dump()
	{
		string localGoodName = goodName;
		uint localAmount = amount;
		infoController.ActivateConfirmationPanel("Do you want to dump " + amount + " " + goodName + "?",
			delegate
		{
			if(!sourceContainer.Withdraw(localGoodName, localAmount))
			{
				infoController.AddMessage("Only " + sourceContainer.GetGoodAmount(goodName) + "/" + amount + " " + goodName
					   + " available to dump in " + sourceContainer.GetCustomModuleName() + "!", true);
			}
			sourceContainer.UpdateModuleMenuInventory();
		});
	}

	public void SetContents(Container sourceContainer, string goodName, uint amount)
	{
		this.sourceContainer = sourceContainer;
		this.goodName = goodName;
		this.amount = (uint)Mathf.Min((int)amount, (int)sourceContainer.GetGoodAmount(goodName));

		armed = true;
	}
}
