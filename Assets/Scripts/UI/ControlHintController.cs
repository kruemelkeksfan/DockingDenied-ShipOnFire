using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class ControlHintController : MonoBehaviour
{
    private static ControlHintController instance = null;

	[SerializeField] private Text controlHint = null;

	private void Awake()
	{
		instance = this;
	}

	public static ControlHintController GetInstance()
	{
		return instance;
	}

	public void UpdateControlHint(Dictionary<string, string[]> keyBindings)
	{
		StringBuilder hint = new StringBuilder(256);
		foreach(string key in keyBindings.Keys)
		{
			if(keyBindings[key].Length > 0)
			{
				hint.Append(key + " -\t");
			}
			
			bool first = true;
			foreach(string action in keyBindings[key])
			{
				if(!first)
				{
					hint.Append("\t\t");
				}
				hint.Append(action + "\n");
				first = false;
			}
		}

		controlHint.text = hint.ToString();
	}
}
