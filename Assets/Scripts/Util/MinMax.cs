using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct MinMax
{
	[SerializeField] private float min;
	public float Min
	{
		get
		{
			return min;
		}
		private set
		{
			min = value;
		}
	}
	[SerializeField] private float max;
	public float Max
	{
		get
		{
			return max;
		}
		private set
		{
			max = value;
		}
	}

	public MinMax(float min, float max)
	{
		if(min < max)
		{
			this.min = min;
			this.max = max;
		}
		else
		{
			Debug.LogWarning("Invalid MinMax Initialization with min " + min + " and max " + max + "!");

			this.min = 0.0f;
			this.max = 1.0f;
		}
	}
}
