using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SpaceStationController : MonoBehaviour, IUpdateListener, IDockingListener
{
	[SerializeField] private RectTransform uiTransform = null;
	[SerializeField] private RectTransform mapMarker = null;
	[SerializeField] private Text mapMarkerName = null;
	[SerializeField] private Text mapMarkerDistance = null;
	[Tooltip("Distance up from which no more digital Digits will be displayed")]
	[SerializeField] private float decimalDigitThreshold = 100.0f;
	[SerializeField] private GameObject stationMenu = null;
	[SerializeField] private Text menuName = null;
	private Spacecraft spacecraft = null;
	private Transform spacecraftTransform = null;
	private Transform playerSpacecraftTransform = null;
	private new Camera camera = null;

	private void Start()
	{
		foreach(DockingPort port in GetComponentsInChildren<DockingPort>())
		{
			port.HotkeyDown();
			port.AddDockingListener(this);
		}

		ToggleController.GetInstance().AddToggleObject("StationMarkers", mapMarker.gameObject);

		spacecraft = GetComponent<Spacecraft>();
		spacecraftTransform = spacecraft.GetTransform();
		camera = Camera.main;

		spacecraft.AddUpdateListener(this);
	}

	private void OnDestroy()
	{
		ToggleController.GetInstance().RemoveToggleObject("StationMarkers", mapMarker.gameObject);
	}

	public void UpdateNotify()
	{
		Vector2 screenPoint;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(uiTransform, camera.WorldToScreenPoint(spacecraftTransform.position), null, out screenPoint);
		mapMarker.anchoredPosition = screenPoint;

		float distance = (spacecraftTransform.position - playerSpacecraftTransform.position).magnitude;
		if(distance > decimalDigitThreshold)
		{
			mapMarkerDistance.text = distance.ToString("F0") + "km";
		}
		else
		{
			mapMarkerDistance.text = distance.ToString("F2") + "km";
		}
	}

	public void Docked(DockingPort port)
	{

	}

	public void Undocked(DockingPort port)
	{
		port.HotkeyDown();
		stationMenu.SetActive(false);
	}

	public void ToggleStationMenu()							// ToggleController would need to know the Name of the Station and therefore a Method here would be necessary anyways
	{
		stationMenu.SetActive(!stationMenu.activeSelf);
	}

	public void SetStationName(string stationName)
	{
		mapMarkerName.text = stationName;
		menuName.text = stationName;
	}

	public void SetPlayerSpacecraft(Transform playerSpacecraftTransform)
	{
		this.playerSpacecraftTransform = playerSpacecraftTransform;
	}
}
