using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class ModuleComponent
{
	private GoodManager goodManager = null;
	private bool isSet = false;
	private string name = "undefined";
	private string quality = "undefined";
	// Attributes are private instead of protected to keep them read-only
	private Dictionary<string, float> attributes = null;

	public static string GetAttributeList(GoodManager.ComponentData componentData)
	{
		StringBuilder attributeList = new StringBuilder();

		for(int i = 0; i < componentData.attributeNames.Length && i < componentData.attributeValues.Length; ++i)
		{
			if(i > 0)
			{
				attributeList.Append("\n");
			}
			attributeList.Append(componentData.attributeNames[i]);
			attributeList.Append(" ");
			if(componentData.attributeValues[i] > 0.0f)
			{
				attributeList.Append("+");
			}
			attributeList.Append(componentData.attributeValues[i]);
		}

		return attributeList.ToString();
	}

	public ModuleComponent()
	{
		goodManager = GoodManager.GetInstance();

		attributes = new Dictionary<string, float>();
	}

	public virtual bool UpdateComponentData(string componentName)
	{
		attributes.Clear();

		if(componentName != null)
		{
			GoodManager.ComponentData componentData = goodManager.GetComponentData(componentName);

			name = componentName;
			quality = componentData.quality.ToString();
			for(int i = 0; i < componentData.attributeNames.Length && i < componentData.attributeValues.Length; ++i)
			{
				attributes.Add(componentData.attributeNames[i], componentData.attributeValues[i]);
			}

			isSet = true;
		}
		else
		{
			name = "undefined";
			quality = "undefined";
			isSet = false;
		}

		// Return false when no Module will be removed for special Cases, e.g. Storage-Component
		return true;
	}

	public bool IsSet()
	{
		return isSet;
	}

	public float GetAttribute(string attributeName)
	{
		if(isSet)
		{
			return attributes[attributeName];
		}
		else
		{
			return 0.0f;
		}
	}

	public string GetName()
	{
		return name;
	}

	public string GetQuality()
	{
		return quality;
	}

	public string GetAttributeList()
	{
		StringBuilder attributeList = new StringBuilder();

		bool first = true;
		foreach(string attributeName in attributes.Keys)
		{
			if(!first)
			{
				attributeList.Append("\n");
			}
			attributeList.Append(attributeName);
			attributeList.Append(" ");
			if(attributes[attributeName] > 0.0f)
			{
				attributeList.Append("+");
			}
			attributeList.Append(attributes[attributeName]);

			first = false;
		}

		return attributeList.ToString();
	}
}
