using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnergyProducer
{
	[Tooltip("Energy Production in kW, 1m^2 of SciFi Solar Panel in this Game is suppossed to produce 0.4kW, the 400m^2 of one Module therefore produce 160kW or 0.044kWs.")]
	public float production = 160.0f;
}
