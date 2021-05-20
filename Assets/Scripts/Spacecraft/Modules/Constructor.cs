using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Constructor : Module
{
	private static readonly WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();

	[SerializeField] private float constructionRange = 0.4f;
	[SerializeField] private LineRenderer constructionBeamPrefab = null;
	[SerializeField] private float constructionBeamDuration = 5.0f;
	[SerializeField] private Transform constructionAreaIndicator = null;
	[SerializeField] private Transform beamOrigin = null;
	private InventoryController inventoryController = null;
	private SpaceStationController spaceStationController = null;
	private Color startColor = Color.white;
	private Color endColor = Color.white;

	protected override void Start()
	{
		base.Start();

		startColor = constructionBeamPrefab.startColor;
		endColor = constructionBeamPrefab.endColor;
		constructionAreaIndicator.localScale = new Vector3(constructionRange * 2.0f, constructionRange * 2.0f, constructionAreaIndicator.localScale.z);

		ToggleController.GetInstance().AddToggleObject("BuildAreaIndicators", constructionAreaIndicator.gameObject);
	}

	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position, listenUpdates, listenFixedUpdates);

		inventoryController = spacecraft.GetInventoryController();
		spaceStationController = GetComponentInParent<SpaceStationController>();

		SpacecraftManager.GetInstance().AddConstructor(this);
	}

	public override void Deconstruct()
	{
		ToggleController.GetInstance().RemoveToggleObject("BuildAreaIndicators", constructionAreaIndicator.gameObject);
		SpacecraftManager.GetInstance().AddConstructor(this);

		base.Deconstruct();
	}

	public bool PositionInRange(Vector2 position)
	{
		Vector2 direction = (Vector2)transform.InverseTransformPoint(position) - (Vector2)transform.localPosition;
		return direction.x >= -constructionRange && direction.x <= constructionRange && direction.y >= -constructionRange && direction.y <= constructionRange;
	}

	public void StartConstruction(Vector2 position)
	{
		StartCoroutine(Construction(position));
	}

	private IEnumerator Construction(Vector3 position)
	{
		// TODO: Object Pooling
		LineRenderer constructionBeam = GameObject.Instantiate<LineRenderer>(constructionBeamPrefab, beamOrigin.position, Quaternion.identity, transform);
		constructionBeam.SetPositions(new Vector3[]{Vector3.zero, transform.InverseTransformPoint(position)});

		float startTime = Time.time;
		while(Time.time < startTime + constructionBeamDuration)
		{
			float progress = ((Time.time - startTime) / constructionBeamDuration) * 2.0f;
			if(progress > 1.0f)
			{
				progress = 2.0f - progress;
			}
			constructionBeam.startColor = new Color(startColor.r, startColor.g, startColor.b, startColor.a * progress);
			constructionBeam.endColor = new Color(endColor.r, endColor.g, endColor.b, endColor.a * progress);

			yield return waitForEndOfFrame;
		}
		
		GameObject.Destroy(constructionBeam);
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
