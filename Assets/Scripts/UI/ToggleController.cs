using System;
using System.Collections.Generic;
using UnityEngine;

public class ToggleController : MonoBehaviour
{
	[Serializable]
	private struct InitToggleObject
	{
		public string groupName;
		public GameObject toggleObject;

		public InitToggleObject(string groupName, GameObject toggleObject)
		{
			this.groupName = groupName;
			this.toggleObject = toggleObject;
		}
	}

	private static ToggleController instance = null;

	[SerializeField] private InitToggleObject[] initToggleObjects = { };
	private Dictionary<string, List<GameObject>> toggleObjects = null;

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

		instance = this;
	}

	public void ToggleGroup(string groupName)
	{
		foreach(GameObject toggleObject in toggleObjects[groupName])
		{
			toggleObject.SetActive(!toggleObject.activeSelf);
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
