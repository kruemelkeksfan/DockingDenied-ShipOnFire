using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Constructor : Module
{
	private static readonly WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();

	[SerializeField] private float constructionRange = 0.4f;
	[SerializeField] private float doorOpeningSpeed = 1.0f;
	[SerializeField] private Transform constructionAreaIndicator = null;
	[SerializeField] private Transform hatchUpper = null;
	[SerializeField] private Transform hatchLower = null;
	[SerializeField] private ParticleSystem nanobotParticles = null;
	private ParticleSystem.Particle[] particles = null;
	private InventoryController inventoryController = null;
	private SpaceStationController spaceStationController = null;

	protected override void Start()
	{
		base.Start();

		constructionAreaIndicator.localScale = new Vector3(constructionRange * 2.0f, constructionRange * 2.0f, constructionAreaIndicator.localScale.z);
		particles = new ParticleSystem.Particle[nanobotParticles.emission.GetBurst(0).maxCount];

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
		float angle = 0.0f;
		while(angle < 90.0f)
		{
			angle += doorOpeningSpeed * 90.0f * Time.deltaTime;
			if(angle > 90.0f)
			{
				angle = 90.0f;
			}
			hatchUpper.localRotation = Quaternion.Euler(angle, 0.0f, 0.0f);
			hatchLower.localRotation = Quaternion.Euler(-angle, 0.0f, 0.0f);
			yield return waitForEndOfFrame;
		}

		nanobotParticles.Clear();
		nanobotParticles.Play();
		yield return waitForEndOfFrame;
		nanobotParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
		yield return new WaitForSeconds(1.0f);
		int particleCount = nanobotParticles.GetParticles(particles);
		Vector3 velocity = (position - transform.position) * 0.2f;
		velocity.z = -0.002f;
		for(int i = 0; i < particleCount; ++i)
		{
			particles[i].velocity = velocity;
		}
		nanobotParticles.SetParticles(particles);

		while(angle > 0.0f)
		{
			angle -= doorOpeningSpeed * 90.0f * Time.deltaTime;
			if(angle < 0.0f)
			{
				angle = 0.0f;
			}
			hatchUpper.localRotation = Quaternion.Euler(angle, 0.0f, 0.0f);
			hatchLower.localRotation = Quaternion.Euler(-angle, 0.0f, 0.0f);
			yield return waitForEndOfFrame;
		}
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
