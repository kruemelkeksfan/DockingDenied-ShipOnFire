﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SpaceStationController : MonoBehaviour, IUpdateListener, IDockingListener, IListener
{
	[SerializeField] private RectTransform uiTransform = null;
	[SerializeField] private RectTransform mapMarker = null;
	[SerializeField] private Text mapMarkerName = null;
	[SerializeField] private Text mapMarkerDistance = null;
	[Tooltip("Distance up from which no more digital Digits will be displayed")]
	[SerializeField] private float decimalDigitThreshold = 100.0f;
	[Tooltip("Maximum Distance from which a Docking Permission can be granted")]
	[SerializeField] private float maxApproachDistance = 1.0f;
	[Tooltip("Maximum Time in Seconds before Docking Permission expires")]
	[SerializeField] private float dockingTimeout = 600.0f;
	[SerializeField] private GameObject stationMenu = null;
	[SerializeField] private Text menuName = null;
	private Spacecraft spacecraft = null;
	private new Transform transform = null;
	private Transform localPlayerSpacecraftTransform = null;
	private new Camera camera = null;
	private DockingPort[] dockingPorts = null;
	private Dictionary<DockingPort, Spacecraft> expectedDockings = null;
	private HashSet<Spacecraft> dockedSpacecraft = null;
	private WaitForSeconds dockingTimeoutWaitForSeconds = null;

	private void Start()
	{
		ToggleController.GetInstance().AddToggleObject("StationMarkers", mapMarker.gameObject);

		maxApproachDistance *= maxApproachDistance;
		spacecraft = GetComponent<Spacecraft>();
		transform = spacecraft.GetTransform();
		camera = Camera.main;
		dockingPorts = GetComponentsInChildren<DockingPort>();
		foreach(DockingPort port in dockingPorts)
		{
			port.AddDockingListener(this);
		}
		expectedDockings = new Dictionary<DockingPort, Spacecraft>();
		dockedSpacecraft = new HashSet<Spacecraft>();
		dockingTimeoutWaitForSeconds = new WaitForSeconds(dockingTimeout);

		SpacecraftManager spacecraftManager = SpacecraftManager.GetInstance();
		localPlayerSpacecraftTransform = spacecraftManager.GetLocalPlayerMainSpacecraft().GetTransform();
		spacecraftManager.AddSpacecraftChangeListener(this);

		spacecraft.AddUpdateListener(this);
	}

	private void OnDestroy()
	{
		ToggleController.GetInstance().RemoveToggleObject("StationMarkers", mapMarker.gameObject);
	}

	public void UpdateNotify()
	{
		Vector2 screenPoint;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(uiTransform, camera.WorldToScreenPoint(transform.position), null, out screenPoint);
		mapMarker.anchoredPosition = screenPoint;

		float distance = (transform.position - localPlayerSpacecraftTransform.position).magnitude;
		if(distance > decimalDigitThreshold)
		{
			mapMarkerDistance.text = distance.ToString("F0") + "km";
		}
		else
		{
			mapMarkerDistance.text = distance.ToString("F2") + "km";
		}
	}

	public void Docked(DockingPort port, DockingPort otherPort)
	{
		if(expectedDockings.ContainsKey(port))
		{
			Spacecraft otherSpacecraft = otherPort.GetComponentInParent<Spacecraft>();
			if(expectedDockings[port] == otherSpacecraft)
			{
				expectedDockings.Remove(port);
				dockedSpacecraft.Add(otherSpacecraft);
			}
			else
			{
				if(otherSpacecraft == SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft())
				{
					MessageController.GetInstance().AddMessage("You have no Docking Permission for this Docking Port!");
				}
				port.HotkeyDown();
			}
		}
	}

	public void Undocked(DockingPort port, DockingPort otherPort)
	{
		dockedSpacecraft.Remove(otherPort.GetComponentInParent<Spacecraft>());

		if(port.IsActive())
		{
			port.HotkeyDown();
		}
		if(otherPort.GetComponentInParent<Spacecraft>() == SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft())
		{
			stationMenu.SetActive(false);
			MessageController.GetInstance().AddMessage("Undocking successful, good Flight!");
		}
	}

	public void Notify()
	{
		localPlayerSpacecraftTransform = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft().GetTransform();
	}

	public void ToggleStationMenu()                         // ToggleController would need to know the Name of the Station and therefore a Method here would be necessary anyways
	{
		stationMenu.SetActive(!stationMenu.activeSelf);
	}

	public void RequestDocking()
	{
		// TODO: Check if Ship is on Fire etc.
		Spacecraft requester = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft();
		if(!dockedSpacecraft.Contains(requester))
		{
			if(!expectedDockings.ContainsValue(requester))
			{
				if((requester.GetTransform().position - transform.position).sqrMagnitude <= maxApproachDistance)
				{
					float maxAngle = float.MinValue;
					DockingPort alignedPort = null;
					foreach(DockingPort port in dockingPorts)
					{
						if(!port.IsActive() && port.IsFree())
						{
							Vector2 approachVector = (requester.GetTransform().position - port.GetTransform().position).normalized;
							float dot = Vector2.Dot(port.GetTransform().up, approachVector);
							if(dot > maxAngle)
							{
								maxAngle = dot;
								alignedPort = port;
							}
						}
					}

					if(alignedPort != null)
					{
						expectedDockings.Add(alignedPort, requester);
						alignedPort.HotkeyDown();
						StartCoroutine(DockingTimeout(alignedPort, requester));

						if(requester == SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft())
						{
							MessageController.GetInstance().AddMessage("Docking Permission granted for Docking Port " + alignedPort.GetActionName() + "!");
						}
					}
					else if(requester == SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft())
					{
						MessageController.GetInstance().AddMessage("No free Docking Ports available!");
					}
				}
				else if(requester == SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft())
				{
					MessageController.GetInstance().AddMessage("You are too far away to request Docking Permission!");
				}
			}
			else
			{
				MessageController.GetInstance().AddMessage("You already have an active Docking Permission for this Station!");
			}
		}
		else
		{
			MessageController.GetInstance().AddMessage("You are already docked at this Station!");
		}
	}

	private IEnumerator DockingTimeout(DockingPort port, Spacecraft requester)
	{
		yield return dockingTimeoutWaitForSeconds;

		if(port.IsActive() && port.IsFree())
		{
			port.HotkeyDown();
			expectedDockings.Remove(port);

			if(requester == SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft())
			{
				MessageController.GetInstance().AddMessage("Docking Permission expired!");
			}
		}
	}

	public void SetStationName(string stationName)
	{
		mapMarkerName.text = stationName;
		menuName.text = stationName;
	}
}