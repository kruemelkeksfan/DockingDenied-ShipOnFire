using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class ModuleComponent
{
	private GoodManager goodManager = null;
	private bool isSet = false;
	private string quality = "undefined";
	// Attributes are private instead of protected to keep them read-only
	private Dictionary<string, float> attributes = null;

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

			quality = componentData.quality.ToString();
			for(int i = 0; i < componentData.attributeNames.Length && i < componentData.attributeValues.Length; ++i)
			{
				attributes.Add(componentData.attributeNames[i], componentData.attributeValues[i]);
			}

			isSet = true;
		}
		else
		{
			isSet = false;
		}

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
