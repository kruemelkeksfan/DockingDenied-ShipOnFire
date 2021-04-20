using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ContainerSingle : Container
{
	public override bool Deposit(string goodName, uint amount)
	{
		if(loads.Count < 1 || loads.ContainsKey(goodName))
		{
			return base.Deposit(goodName, amount);
		}
		else
		{
			return false;
		}
	}
}
