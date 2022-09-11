using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ComponentManager : MonoBehaviour
{
	private static ComponentManager instance = null;

    [SerializeField] private float teleportationEnergyCost = 1.0f;
	[SerializeField] private float constructionEnergyCost = 10.0f;

	public static ComponentManager GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		instance = this;
	}

	public float GetTeleportationEnergyCost()
	{
		return teleportationEnergyCost;
	}

	public float GetConstructionEnergyCost()
	{
		return constructionEnergyCost;
	}
}
