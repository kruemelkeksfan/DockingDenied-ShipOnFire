using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeyboardInputController : InputController
{
	[SerializeField] private GameObject buildingMenu = null;
	private ControlHintController controlHintController = null;
	private Dictionary<Vector2Int, string> actionNames = null;

	protected override void Awake()
	{
		base.Awake();

		actionNames = new Dictionary<Vector2Int, string>();
	}

	protected override void Start()
	{
		base.Start();

		controlHintController = ControlHintController.GetInstance();
	}

	public override void UpdateNotify()
	{
		base.UpdateNotify();

		if(!buildingMenu.activeSelf)
		{
			spacecraft.SetThrottles(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), Input.GetAxis("Rotate"));

			for(int i = 0; i < hotkeyCount; ++i)
			{
				if(Input.GetButtonDown("Hotkey " + (i + 1)))
				{
					foreach(Action action in hotkeys[i].Values)
					{
						action();
					}
				}
			}
		}
	}

	public void UpdateActionName(int hotkey, int actionID, string actionName)
	{
		actionNames[new Vector2Int(hotkey, actionID)] = actionName;
		GenerateControlHint();
	}

	public override int AddHotkey(int hotkey, Action action, string actionName)
	{
		int hotkeyID = base.AddHotkey(hotkey, action, actionName);
		if(hotkeyID >= 0)
		{
			actionNames.Add(new Vector2Int(hotkey, hotkeyID), actionName);
			GenerateControlHint();
		}
		return hotkeyID;
	}

	public override void RemoveHotkey(int hotkey, int actionID)
	{
		base.RemoveHotkey(hotkey, actionID);
		actionNames.Remove(new Vector2Int(hotkey, actionID));
		GenerateControlHint();
	}

	private void GenerateControlHint()
	{
		Dictionary<string, string[]> keyBindings = new Dictionary<string, string[]>();
		foreach(int hotkey in hotkeys.Keys)
		{
			string[] actions = new string[hotkeys[hotkey].Count];
			int i = 0;
			foreach(int actionID in hotkeys[hotkey].Keys)
			{
				actions[i] = actionNames[new Vector2Int(hotkey, actionID)];
				++i;
			}
			keyBindings.Add((hotkey + 1).ToString(), actions);
		}
		
		controlHintController.UpdateControlHint(keyBindings);
	}
}
