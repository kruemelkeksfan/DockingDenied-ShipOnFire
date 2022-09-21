using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ContainerMenuController : MonoBehaviour
{
    private Container container = null;

    public Container GetContainer()
	{
        return container;
	}

    public void SetContainer(Container container)
	{
        this.container = container;
	}

    public void SetTransferAmount(int transferAmount)
	{
        container.SetTransferAmount(transferAmount);
	}

	public void SetCustomTradeAmount()
	{
		container.SetCustomTradeAmount();
	}

    public void SetHighlightedAmountButton(int id)
	{
		container.SetHighlightedAmountButton(id);
	}
}
