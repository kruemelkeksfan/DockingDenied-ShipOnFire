﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ContainerSingle : Container
{
	public override bool Deposit(string goodName, uint amount, Vector2? teleportationSource)
	{
		if(loads.Count <= 0 || loads.ContainsKey(goodName))
		{
			return base.Deposit(goodName, amount, teleportationSource);
		}
		else
		{
			return false;
		}
	}

	public override uint GetFreeCapacity(string goodName)
	{
		if(loads.Count <= 0 || loads.ContainsKey(goodName))
		{
			return storage.GetFreeCapacity();
		}
		else
		{
			return 0;
		}
	}
}
