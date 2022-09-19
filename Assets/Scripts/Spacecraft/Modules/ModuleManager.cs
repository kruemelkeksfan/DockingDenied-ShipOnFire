using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class ModuleManager : MonoBehaviour
{
	private static ModuleManager instance = null;

	[SerializeField] private int maxModuleMenuButtonCharacters = 24;
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
	[SerializeField] private Color hpColor = Color.red;
	[SerializeField] private Color volumeColor = Color.cyan;
	[SerializeField] private Color massColor = Color.gray;
	[SerializeField] private GameObject statusBarPrefab = null;
	private InfoController infoController = null;
	private float barFillingImageStartWidth = 0.0f;

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

	public RectTransform InstantiateStatusBar(string title, Color color, float value, RectTransform parent)
	{
		GameObject statusBar = GameObject.Instantiate<GameObject>(statusBarPrefab, parent);

		statusBar.GetComponentInChildren<Text>().text = title;

		Image barFillingImage = statusBar.GetComponentsInChildren<Image>()[1];
		barFillingImage.color = color;
		RectTransform barFillingTransform = barFillingImage.GetComponent<RectTransform>();
		if(Mathf.Approximately(barFillingImageStartWidth, 0.0f))
		{
			barFillingImageStartWidth = barFillingTransform.sizeDelta.x;
		}
		UpdateStatusBar(barFillingTransform, value);

		return barFillingTransform;
	}

	public void UpdateStatusBar(RectTransform barFillingTransform, float value)
	{
		barFillingTransform.sizeDelta = new Vector2(barFillingImageStartWidth * value, barFillingTransform.sizeDelta.y);
	}

	public int GetMaxModuleMenuButtonCharacters()
	{
		return maxModuleMenuButtonCharacters;
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

	public Color GetHpColor()
	{
		return hpColor;
	}

	public Color GetVolumeColor()
	{
		return volumeColor;
	}

	public Color GetMassColor()
	{
		return massColor;
	}
}
