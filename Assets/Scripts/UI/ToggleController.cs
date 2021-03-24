using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToggleController : MonoBehaviour
{
	[Serializable]
	private struct InitToggleObject
	{
		public int groupIndex;
		public GameObject toggleObject;

		public InitToggleObject(int groupIndex, GameObject toggleObject)
		{
			this.groupIndex = groupIndex;
			this.toggleObject = toggleObject;
		}
	}

	private static ToggleController instance = null;

    [SerializeField] private int groupCount = 1;
	[SerializeField] private InitToggleObject[] initToggleObjects = { };
    private List<GameObject>[] toggleObjects = null;

	private void Awake()
	{
		toggleObjects = new List<GameObject>[groupCount];
		for(int i = 0; i < toggleObjects.Length; ++i)
		{
			toggleObjects[i] = new List<GameObject>(1);
		}

		foreach(InitToggleObject toggleObject in initToggleObjects)
		{
			toggleObjects[toggleObject.groupIndex].Add(toggleObject.toggleObject);
		}

		instance = this;
	}

	public static ToggleController GetInstance()
	{
		return instance;
	}

	public void ToggleGroup(int groupIndex)
	{
		foreach(GameObject toggleObject in toggleObjects[groupIndex])
		{
			toggleObject.SetActive(!toggleObject.activeSelf);
		}
	}

	public void AddToggleObject(int groupIndex, GameObject toggleObject)
	{
		toggleObjects[groupIndex].Add(toggleObject);
	}
}
