﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Constructor : Module
{
	[SerializeField] private float constructionRange = 0.4f;
	[SerializeField] private LineRenderer constructionBeamPrefab = null;
	[SerializeField] private float constructionBeamDuration = 5.0f;
	[SerializeField] private Transform constructionAreaIndicator = null;
	[SerializeField] private Transform beamOrigin = null;
	[SerializeField] private AudioClip constructionAudio = null;
	private SpaceStationController spaceStationController = null;
	private Color startColor = Color.white;
	private Color endColor = Color.white;
	private EnergyStorage capacitor = null;
	private Teleporter teleporter = null;
	private ConstructionUnit constructionUnit = null;
	private bool componentsSet = false;

	protected override void Start()
	{
		base.Start();

		startColor = constructionBeamPrefab.startColor;
		endColor = constructionBeamPrefab.endColor;
		constructionAreaIndicator.localScale = new Vector3(constructionRange * 2.0f, constructionRange * 2.0f, constructionAreaIndicator.localScale.z);
	}

	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position, moduleMenu != null, false);

		spaceStationController = GetComponentInParent<SpaceStationController>();

		capacitor = new EnergyStorage();
		AddComponentSlot(GoodManager.ComponentType.Capacitor, capacitor);
		inventoryController.AddEnergyConsumer(capacitor);

		constructionUnit = new ConstructionUnit();
		AddComponentSlot(GoodManager.ComponentType.ConstructionUnit, constructionUnit);

		if(moduleMenu != null)
		{
			// Status
			AddStatusField("Capacitor Charge", capacitor.GetChargeString());
		}

		SpacecraftManager.GetInstance().AddConstructor(this);
		ToggleController.GetInstance().AddToggleObject("BuildAreaIndicators", constructionAreaIndicator.gameObject);
	}

	public override void Deconstruct()
	{
		ToggleController.GetInstance().RemoveToggleObject("BuildAreaIndicators", constructionAreaIndicator.gameObject);
		SpacecraftManager.GetInstance().RemoveConstructor(this);

		base.Deconstruct();
	}

	public override void UpdateNotify()
	{
		base.UpdateNotify();

		// Status
		UpdateStatusField("Capacitor Charge", capacitor.GetChargeString());
	}

	public bool PositionInRange(Vector2 position)
	{
		// TODO: Why InverseTransformPosition in the 1st Part instead of using transform.position in the 2nd Part?
		Vector2 direction = (Vector2)transform.InverseTransformPoint(position) - (Vector2)transform.localPosition;
		return direction.x >= -constructionRange && direction.x <= constructionRange && direction.y >= -constructionRange && direction.y <= constructionRange;
	}

	public int TryConstruction(Vector2 targetPosition, GoodManager.Load[] constructionCosts)
	{
		if(teleporter == null)
		{
			teleporter = spacecraft.GetTeleporter();
		}

		if(!componentsSet)
		{
			constructionUnit.SetComponents(capacitor, teleporter);
			componentsSet = true;
		}
		
		return constructionUnit.Construct(transform.position, targetPosition, constructionCosts);
	}

	public void RollbackConstruction()
	{
		constructionUnit.Rollback(capacitor);
	}

	public void StartConstruction(Vector2 position)
	{
		timeController.StartCoroutine(Construction(position), false);
		audioController.PlayAudio(constructionAudio, spacecraft.gameObject);
	}

	private IEnumerator<float> Construction(Vector3 position)
	{
		LineRenderer constructionBeam = GameObject.Instantiate<LineRenderer>(constructionBeamPrefab, beamOrigin.position, beamOrigin.rotation, transform);
		constructionBeam.SetPositions(new Vector3[]{Vector3.zero, transform.InverseTransformPoint(position)});

		double startTime = timeController.GetTime();
		while(timeController.GetTime() < startTime + constructionBeamDuration)
		{
			float progress = (float)((timeController.GetTime() - startTime) / constructionBeamDuration) * 2.0f;
			if(progress > 1.0f)
			{
				progress = 2.0f - progress;
			}
			constructionBeam.startColor = new Color(startColor.r, startColor.g, startColor.b, startColor.a * progress);
			constructionBeam.endColor = new Color(endColor.r, endColor.g, endColor.b, endColor.a * progress);

			yield return -1.0f;
		}
		
		GameObject.Destroy(constructionBeam.gameObject);
	}

	public InventoryController GetInventoryController()
	{
		return inventoryController;
	}

	public SpaceStationController GetSpaceStationController()
	{
		return spaceStationController;
	}
}
