using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CommandModule : Module
{
	protected override void Start()
	{
		base.Start();

		spacecraft.UpdateModuleMass(transform.localPosition, mass);
	}

	public override void Deconstruct()
	{
		// CommandModule can not be deconstructed
	}
}
