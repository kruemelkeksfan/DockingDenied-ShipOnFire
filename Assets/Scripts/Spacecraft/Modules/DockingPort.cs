using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DockingPort : HotkeyModule
{
	[SerializeField] private Transform dockingLocation = null;
	[SerializeField] private float dockingRotationThreshold = 10.0f;
	[SerializeField] private Text portNameField = null;
	[SerializeField] private float jointFrequency = 2.0f;
	[SerializeField] private float jointDamping = 1.0f;
	[SerializeField] private Collider2D dockingTriggerCollider = null;
	[SerializeField] private AudioClip dockingAudio = null;
	[SerializeField] private AudioClip dockingSuccessAudio = null;
	private Transform spacecraftTransform = null;
	private ParticleSystem magnetParticles = null;
	private bool active = false;
	private DockingPort connectedPort = null;
	private FixedJoint2D joint = null;
	private List<IDockingListener> dockingListeners = null;
	private new Rigidbody2D rigidbody = null;

	protected override void Awake()
	{
		base.Awake();

		magnetParticles = gameObject.GetComponentInChildren<ParticleSystem>();
		dockingListeners = new List<IDockingListener>(2);
		rigidbody = gameObject.GetComponentInParent<Rigidbody2D>();
	}

	protected override void OnDestroy()
	{
		if(!IsFree())
		{
			HotkeyDown();
		}

		ToggleController.GetInstance()?.RemoveToggleObject("PortNameplates", portNameField.gameObject);

		base.OnDestroy();
	}

	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position, listenUpdates, listenFixedUpdates);
		spacecraftTransform = spacecraft.GetTransform();
		ToggleController.GetInstance().AddToggleObject("PortNameplates", portNameField.gameObject);
		portNameField.text = customModuleName;
		AddDockingListener(spacecraft);
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		Dock(other);
	}

	private void OnTriggerStay2D(Collider2D other)
	{
		Dock(other);
	}

	public override void HotkeyDown()
	{
		if(!active)
		{
			audioController.LoopAudioStart(dockingAudio, spacecraft.gameObject);
		}
		else if(connectedPort == null)
		{
			audioController.LoopAudioStop(dockingAudio, spacecraft.gameObject);
		}
		active = !active;

		if(connectedPort != null)
		{
			DockingPort otherPort = connectedPort;

			dockingTriggerCollider.enabled = true;
			connectedPort.dockingTriggerCollider.enabled = true;

			connectedPort = null;
			otherPort.connectedPort = null;

			Component.Destroy(joint);
			joint = null;

			foreach(IDockingListener listener in dockingListeners)
			{
				listener.Undocked(this, otherPort);
			}
			foreach(IDockingListener listener in otherPort.dockingListeners)
			{
				listener.Undocked(otherPort, this);
			}

			otherPort.ToggleParticles();
		}

		ToggleParticles();
	}

	private void ToggleParticles()
	{
		if(active && connectedPort == null)
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

	private void Dock(Collider2D other)
	{
		if(active && connectedPort == null && other.isTrigger)
		{
			DockingPort otherPort = other.GetComponent<DockingPort>();
			if(otherPort != null && otherPort.active && otherPort.connectedPort == null)
			{
				Rigidbody2D otherRigidbody = other.GetComponentInParent<Rigidbody2D>();
				if(rigidbody.mass <= otherRigidbody.mass)
				{
					float dRotation = Mathf.DeltaAngle(otherPort.dockingLocation.rotation.eulerAngles.z, (dockingLocation.rotation.eulerAngles.z + 180.0f));
					if(dRotation < dockingRotationThreshold)
					{
						// Use rigidbody.rotation instead of transform.rotation, because else the Physics System is not flushed and will fuck up the Joint
						rigidbody.rotation += (float) System.Math.IEEERemainder(
							otherPort.dockingLocation.rotation.eulerAngles.z - (dockingLocation.rotation.eulerAngles.z + 180.0f), 360.0f);

						joint = spacecraft.gameObject.AddComponent<FixedJoint2D>();
						joint.frequency = jointFrequency;
						joint.dampingRatio = jointDamping;
						joint.connectedBody = otherRigidbody;
						joint.autoConfigureConnectedAnchor = false;
						joint.anchor = spacecraftTransform.InverseTransformPoint(dockingLocation.position);
						joint.connectedAnchor = otherPort.spacecraftTransform.InverseTransformPoint(otherPort.dockingLocation.position);
						joint.enableCollision = false;

						connectedPort = otherPort;
						otherPort.connectedPort = this;
						otherPort.joint = joint;

						dockingTriggerCollider.enabled = false;
						connectedPort.dockingTriggerCollider.enabled = false;

						foreach(IDockingListener listener in dockingListeners)
						{
							listener.Docked(this, otherPort);
						}
						foreach(IDockingListener listener in otherPort.dockingListeners)
						{
							listener.Docked(otherPort, this);
						}
						ToggleParticles();
						otherPort.ToggleParticles();

						audioController.LoopAudioStop(dockingAudio, spacecraft.gameObject);
						otherPort.audioController.LoopAudioStop(dockingAudio, otherRigidbody.gameObject);
						audioController.PlayAudio(dockingSuccessAudio, spacecraft.gameObject);
						otherPort.audioController.PlayAudio(dockingSuccessAudio, otherRigidbody.gameObject);
					}
				}
			}
		}
	}

	public void AddDockingListener(IDockingListener listener)
	{
		dockingListeners.Add(listener);
	}

	public bool IsActive()
	{
		return active;
	}

	public bool IsFree()
	{
		return connectedPort == null;
	}

	public override void SetCustomModuleName(string customModuleName)
	{
		base.SetCustomModuleName(customModuleName);

		portNameField.text = customModuleName;
	}
}
