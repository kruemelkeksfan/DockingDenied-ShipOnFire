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

	public static string[] GetAttributeList(GoodManager.ComponentData componentData)
	{
		StringBuilder attributeNames = new StringBuilder();
		StringBuilder attributeValues = new StringBuilder();

		for(int i = 0; i < componentData.attributeNames.Length && i < componentData.attributeValues.Length; ++i)
		{
			if(i > 0)
			{
				attributeNames.Append("\n");
				attributeValues.Append("\n");
			}
			attributeNames.Append(componentData.attributeNames[i]);
			attributeNames.Append(":");

			// Cut Value after 6 decimal Digits (including Separator and leading 0)
			string valueString = componentData.attributeValues[i].ToString();
			int integerDigits = Mathf.FloorToInt(componentData.attributeValues[i]).ToString().Length;
			valueString = valueString.Substring(0, Mathf.Min(Mathf.Max(integerDigits, 6), valueString.Length));
			valueString = valueString.TrimEnd(',', '.', ' ');

			attributeValues.Append(valueString);
		}

		return new string[] { attributeNames.ToString(), attributeValues.ToString() };
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

	public string[] GetAttributeList()
	{
		StringBuilder attributeNames = new StringBuilder();
		StringBuilder attributeValues = new StringBuilder();

		bool first = true;
		foreach(string attributeName in attributes.Keys)
		{
			if(!first)
			{
				attributeNames.Append("\n");
				attributeValues.Append("\n");
			}
			attributeNames.Append(attributeName);
			attributeNames.Append(":");

			// Cut Value after 6 decimal Digits (including Separator and leading 0)
			string valueString = attributes[attributeName].ToString();
			int integerDigits = Mathf.FloorToInt(attributes[attributeName]).ToString().Length;
			valueString = valueString.Substring(0, Mathf.Min(Mathf.Max(integerDigits, 6), valueString.Length));
			valueString = valueString.TrimEnd(',', '.', ' ');

			attributeValues.Append(valueString);

			first = false;
		}

		return new string[] { attributeNames.ToString(), attributeValues.ToString() };
	}
}
