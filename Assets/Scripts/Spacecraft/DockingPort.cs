using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DockingPort : HotkeyModule
{
	private ParticleSystem magnetParticles = null;
	private bool active = false;

	protected override void Awake()
	{
		base.Awake();

		magnetParticles = gameObject.GetComponentInChildren<ParticleSystem>();
	}

    public override void HotkeyPressed()
	{
		active = !active;

		if(active)
		{
			magnetParticles.Play();
			magnetParticles.Simulate(8.0f);
			magnetParticles.Play();
		}
		else
		{
			magnetParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		}
	}


}
