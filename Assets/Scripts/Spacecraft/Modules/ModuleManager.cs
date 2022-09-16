using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class ModuleManager : MonoBehaviour
{
	private static ModuleManager instance = null;

	[Tooltip("Number of Segments for Status Bars.")]
	[SerializeField] private int barSegments = 10;
	[Tooltip("Temperature of newly spawned Objects.")]
	[SerializeField] private float defaultTemperature = 120.0f;
	[Tooltip("Maximum Temperature which is bearable for longer Durations for Crew Members.")]
	[SerializeField] private float comfortableTemperature = 310.0f;
	[Tooltip("Temperature at which pressurized Modules will catch Fire.")]
	[SerializeField] private float ignitionTemperature = 550.0f;
	[Tooltip("Threshold for the 'Low Maintenance' Module State.")]
	[SerializeField] private float lowMaintenaceThreshold = 0.6f;
	[Tooltip("Threshold for the 'CONDITION CRIT' Module State.")]
	[SerializeField] private float criticalMaintenanceThreshold = 0.2f;
	[Tooltip("Threshold for the 'Low HP' Module State.")]
	[SerializeField] private float lowHpThreshold = 0.6f;
	[Tooltip("Threshold for the 'Critical HP' Module State.")]
	[SerializeField] private float criticalHpThreshold = 0.2f;
	[Tooltip("Default Font Color for Module Buttons.")]
	[SerializeField] private Color normalColor = Color.white;
	[Tooltip("Font Color for Modules in bad Condition.")]
	[SerializeField] private Color badColor = Color.yellow;
	[Tooltip("Font Color for Modules in critical Condition.")]
	[SerializeField] private Color criticalColor = Color.red;
	[Tooltip("Font Color for Modules in bad Condition in Colorblind Mode.")]
	[SerializeField] private Color colorblindBadColor = Color.yellow;
	[Tooltip("Font Color for Modules in critical Condition in Colorblind Mode.")]
	[SerializeField] private Color colorblindCriticalColor = Color.yellow;
	[SerializeField] private string hpColor = "#FF0000";
	[SerializeField] private string volumeColor = "#0000FF";
	[SerializeField] private string massColor = "#884400";
	private InfoController infoController = null;

	public static ModuleManager GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		instance = this;
	}

	private void Start()
	{
		infoController = InfoController.GetInstance();
	}

	public string GetBarString(float value)
	{
		StringBuilder barString = new StringBuilder();
		int bars = Mathf.FloorToInt(value * barSegments);
		barString.Append("[");
		for(int i = 0; i < bars; ++i)
		{
			barString.Append("#");
		}
		for(int i = bars; i < barSegments; ++i)
		{
			barString.Append("....");
		}
		barString.Append("]");

		return barString.ToString();
	}

	public float GetDefaultTemperature()
	{
		return defaultTemperature;
	}

	public float GetComfortableTemperature()
	{
		return comfortableTemperature;
	}

	public float GetIgnitionTemperature()
	{
		return ignitionTemperature;
	}

	public float GetLowMaintenanceThreshold()
	{
		return lowMaintenaceThreshold;
	}

	public float GetCriticalMaintenanceThreshold()
	{
		return criticalMaintenanceThreshold;
	}

	public float GetLowHpThreshold()
	{
		return lowHpThreshold;
	}

	public float GetCriticalHpThreshold()
	{
		return criticalHpThreshold;
	}

	public Color GetNormalColor()
	{
		return normalColor;
	}

	public Color GetBadColor()
	{
		return (infoController == null || !infoController.IsColorblindModeActivated()) ? badColor : colorblindBadColor;
	}

	public Color GetCriticalColor()
	{
		return (infoController == null || !infoController.IsColorblindModeActivated()) ? criticalColor : colorblindCriticalColor;
	}

	public string GetHpColor()
	{
		return hpColor;
	}

	public string GetVolumeColor()
	{
		return volumeColor;
	}

	public string GetMassColor()
	{
		return massColor;
	}
}
