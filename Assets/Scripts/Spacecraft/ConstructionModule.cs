using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConstructionModule : Module
{
	private static readonly WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();

	[SerializeField] private float constructionRange = 0.1f;
	[SerializeField] private float doorOpeningSpeed = 1.0f;
	[SerializeField] private Transform constructionAreaIndicator = null;
	[SerializeField] private Transform hatchUpper = null;
	[SerializeField] private Transform hatchLower = null;
	[SerializeField] private ParticleSystem nanobotParticles = null;
	private ParticleSystem.Particle[] particles = null;

	protected override void Start()
	{
		base.Start();

		constructionAreaIndicator.localScale = new Vector3(constructionRange, constructionRange, constructionAreaIndicator.localScale.z);
		particles = new ParticleSystem.Particle[nanobotParticles.emission.GetBurst(0).maxCount];
	}

	public bool PositionInRange(Vector2 position)
	{
		Vector2 direction = position - (Vector2) transform.localPosition;
		return (direction.x + direction.y) <= constructionRange;
	}

	public void StartConstruction(Vector2 position)
	{
		StartCoroutine(Construction(position));
	}

	private IEnumerator Construction(Vector2 position)
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
		Vector3 velocity = ((Vector3) position - transform.localPosition) * 0.2f;
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
}
