using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ToggleController : MonoBehaviour
{
	[Serializable]
	private struct InitToggleObject
	{
		public string groupName;
		public GameObject toggleObject;
	}

	[Serializable]
	private struct InitToggleText
	{
		public string groupName;
		public Text text;
	}

	private class ToggleGroup
	{
		public HashSet<GameObject> toggleObjects;
		public Text text;
		public bool active;

		public ToggleGroup(Text text)
		{
			toggleObjects = new HashSet<GameObject>();
			this.text = text;
			active = text.text.Contains("Hide");
		}

		public ToggleGroup(GameObject toggleObject)
		{
			toggleObjects = new HashSet<GameObject>();
			text = null;
			active = text != null && text.text.Contains("Hide");

			toggleObjects.Add(toggleObject);
		}

		public void Toggle()
		{
			active = !active;
		}
	}

	private static ToggleController instance = null;

	[SerializeField] private InitToggleObject[] initToggleObjects = { };
	[SerializeField] private InitToggleText[] initToggleTexts = { };
	private Dictionary<string, ToggleGroup> toggleGroups = null;

	public static ToggleController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		toggleGroups = new Dictionary<string, ToggleGroup>();

		foreach(InitToggleText initToggleText in initToggleTexts)
		{
			toggleGroups.Add(initToggleText.groupName, new ToggleGroup(initToggleText.text));
		}

		foreach(InitToggleObject toggleObject in initToggleObjects)
		{
			AddToggleObject(toggleObject.groupName, toggleObject.toggleObject);
		}

		instance = this;
	}

	public void Toggle(string groupName)
	{
		toggleGroups[groupName].Toggle();

		foreach(GameObject toggleObject in toggleGroups[groupName].toggleObjects)
		{
			toggleObject.SetActive(toggleGroups[groupName].active);
		}

		if(toggleGroups[groupName].text != null)
		{
			if(toggleGroups[groupName].active)
			{
				toggleGroups[groupName].text.text = toggleGroups[groupName].text.text.Replace("Show", "Hide");
			}
			else
			{
				toggleGroups[groupName].text.text = toggleGroups[groupName].text.text.Replace("Hide", "Show");
			}
		}
	}

	public void AddToggleObject(string groupName, GameObject toggleObject)
	{
		if(!toggleGroups.ContainsKey(groupName))
		{
			toggleGroups.Add(groupName, new ToggleGroup(toggleObject));
		}
		else
		{
			toggleGroups[groupName].toggleObjects.Add(toggleObject);
		}
	}

	public void RemoveToggleObject(string groupName, GameObject toggleObject)
	{
		toggleGroups[groupName].toggleObjects.Remove(toggleObject);
	}

	public bool IsGroupToggled(string groupName)
	{
		return toggleGroups[groupName].active;
	}
}
