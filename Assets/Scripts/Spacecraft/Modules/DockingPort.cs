using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DockingPort : HotkeyModule
{
	private static readonly WaitForFixedUpdate WAIT_FOR_FIXED_UPDATE = new WaitForFixedUpdate();

	[SerializeField] private Transform dockingLocation = null;
	[SerializeField] private float dockingPositionThreshold = 1.0f;
	[SerializeField] private float dockingRotationThreshold = 10.0f;
	[SerializeField] private Text portNameField = null;
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

		// Square to avoid Sqrt later on
		dockingPositionThreshold *= dockingPositionThreshold;

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

		ToggleController.GetInstance()?.RemoveToggleObject(ToggleController.GroupNames.PortNameplates, portNameField.gameObject);

		base.OnDestroy();
	}

	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position, listenUpdates, listenFixedUpdates);
		spacecraftTransform = spacecraft.GetTransform();
		ToggleController.GetInstance().AddToggleObject(ToggleController.GroupNames.PortNameplates, portNameField.gameObject);
		portNameField.text = GetActionName();
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
		active = !active;

		if(connectedPort != null)
		{
			DockingPort otherPort = connectedPort;

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

	public override void SetActionName(string actionName)
	{
		base.SetActionName(actionName);
		portNameField.text = actionName;
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
					Vector2 dPosition = (Vector2)(otherPort.dockingLocation.position - dockingLocation.position);

					if(dRotation < 20.0f && dPosition.magnitude < 2.0f)
					{
						// Use rigidbody.position/rigidbody.rotation instead of transform.position/transform.rotation, because else the Physics System is not flushed and will fuck up the Joint
						rigidbody.rotation += otherPort.dockingLocation.rotation.eulerAngles.z - (dockingLocation.rotation.eulerAngles.z + 180.0f);
						rigidbody.position += dPosition;

						joint = spacecraft.gameObject.AddComponent<FixedJoint2D>();
						joint.connectedBody = otherRigidbody;
						joint.autoConfigureConnectedAnchor = false;
						joint.anchor = spacecraftTransform.InverseTransformPoint(dockingLocation.position);
						joint.connectedAnchor = otherPort.spacecraftTransform.InverseTransformPoint(otherPort.dockingLocation.position);
						joint.enableCollision = false;

						connectedPort = otherPort;
						otherPort.connectedPort = this;
						otherPort.joint = joint;

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
}
