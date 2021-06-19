using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DockingPort : HotkeyModule
{
	private static readonly WaitForFixedUpdate WAIT_FOR_FIXED_UPDATE = new WaitForFixedUpdate();

	[SerializeField] private Transform dockingLocation = null;
	[SerializeField] private float dockingSpeed = 2.0f;
	[SerializeField] private float dockingRotationSpeed = 0.8f;
	[SerializeField] private float dockingPositionThreshold = 0.00002f;
	[SerializeField] private float dockingRotationThreshold = 0.2f;
	[SerializeField] private Text portNameField = null;
	private ParticleSystem magnetParticles = null;
	private bool active = false;
	private bool docking = false;
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
		ToggleController.GetInstance().AddToggleObject("PortNameplates", portNameField.gameObject);
		portNameField.text = GetActionName();
		AddDockingListener(spacecraft);
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if(active && !docking && connectedPort == null && other.isTrigger)
		{
			StartCoroutine(Dock(other));
		}
	}

	private void OnTriggerStay2D(Collider2D other)
	{
		if(active && !docking && connectedPort == null && other.isTrigger)
		{
			StartCoroutine(Dock(other));
		}
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

	private IEnumerator Dock(Collider2D other)
	{
		DockingPort otherPort = other.GetComponent<DockingPort>();
		if(otherPort != null && !otherPort.docking)
		{
			Rigidbody2D otherRigidbody = other.GetComponentInParent<Rigidbody2D>();
			if(rigidbody.mass <= otherRigidbody.mass)
			{
				while(active && connectedPort == null && otherPort.active && otherPort.connectedPort == null)
				{
					docking = true;

					yield return WAIT_FOR_FIXED_UPDATE;

					Vector2 dPosition = otherPort.dockingLocation.position - dockingLocation.position;
					float dRotation = ((otherPort.dockingLocation.rotation.eulerAngles.z + 180.0f) - dockingLocation.rotation.eulerAngles.z) % 360.0f;
					if(dRotation < 0.0f)
					{
						dRotation += 360.0f;
					}
					if(dRotation > 180.0f)
					{
						dRotation -= 360.0f;
					}

					rigidbody.velocity = otherRigidbody.velocity;
					rigidbody.angularVelocity = otherRigidbody.angularVelocity;

					if(dPosition.sqrMagnitude < dockingPositionThreshold && Mathf.Abs(dRotation) < dockingRotationThreshold)
					{
						rigidbody.position += dPosition;
						rigidbody.rotation += dRotation;

						joint = spacecraft.gameObject.AddComponent<FixedJoint2D>();
						joint.connectedBody = otherRigidbody;
						joint.autoConfigureConnectedAnchor = false;
						joint.anchor = spacecraft.GetTransform().InverseTransformPoint(dockingLocation.position);
						joint.connectedAnchor = otherPort.spacecraft.GetTransform().InverseTransformPoint(otherPort.dockingLocation.position);
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

						break;                                                                                                                                  // Break in case the Connection gets seperated again by a DockingListener
					}
					else
					{
						rigidbody.position += dPosition * dockingSpeed * Time.fixedDeltaTime;
						rigidbody.rotation += dRotation * dockingRotationSpeed * Time.fixedDeltaTime;
					}
				}

				docking = false;
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
		return !docking && connectedPort == null;
	}
}
