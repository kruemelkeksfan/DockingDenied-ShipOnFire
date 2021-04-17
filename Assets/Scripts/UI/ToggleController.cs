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

	private static ToggleController instance = null;

	[SerializeField] private InitToggleObject[] initToggleObjects = { };
	[SerializeField] private InitToggleText[] initToggleTexts = { };
	private Dictionary<string, List<GameObject>> toggleObjects = null;
	private Dictionary<string, Text> toggleTexts = null;

	public static ToggleController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		toggleObjects = new Dictionary<string, List<GameObject>>();
		foreach(InitToggleObject toggleObject in initToggleObjects)
		{
			AddToggleObject(toggleObject.groupName, toggleObject.toggleObject);
		}

		toggleTexts = new Dictionary<string, Text>(initToggleTexts.Length);
		foreach(InitToggleText initToggleText in initToggleTexts)
		{
			toggleTexts.Add(initToggleText.groupName, initToggleText.text);
		}

		instance = this;
	}

	public void ToggleGroup(string groupName)
	{
		if(toggleObjects[groupName].Count > 0)
		{
			bool activate = !toggleObjects[groupName][0].activeSelf;

			foreach(GameObject toggleObject in toggleObjects[groupName])
			{
				toggleObject.SetActive(activate);
			}

			if(toggleTexts.ContainsKey(groupName))
			{
				if(activate)
				{
					toggleTexts[groupName].text = toggleTexts[groupName].text.Replace("Show", "Hide");
				}
				else
				{
					toggleTexts[groupName].text = toggleTexts[groupName].text.Replace("Hide", "Show");
				}
			}
		}
	}

	public void AddToggleObject(string groupName, GameObject toggleObject)
	{
		if(!toggleObjects.ContainsKey(groupName))
		{
			toggleObjects.Add(groupName, new List<GameObject>());
		}
		toggleObjects[groupName].Add(toggleObject);
	}

	public void RemoveToggleObject(string groupName, GameObject toggleObject)
	{
		if(toggleObjects.ContainsKey(groupName))
		{
			toggleObjects[groupName].Remove(toggleObject);
		}
	}
}
