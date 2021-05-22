using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeyboardInputController : InputController
{
	private struct HotkeyKey
	{
		public int hotkey;
		public IHotkeyListener listener;

		public HotkeyKey(int hotkey, IHotkeyListener listener)
		{
			this.hotkey = hotkey;
			this.listener = listener;
		}
	}

	private InfoController infoController = null;
	private Dictionary<HotkeyKey, string> actionNames = null;

	protected override void Awake()
	{
		base.Awake();

		actionNames = new Dictionary<HotkeyKey, string>();
	}

	protected override void Start()
	{
		base.Start();

		infoController = InfoController.GetInstance();
	}

	public override void UpdateNotify()
	{
		base.UpdateNotify();

		if(flightControls >= 0 && !Input.GetButton("Rotate Camera"))
		{
			spacecraft.SetThrottles(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), Input.GetAxis("Rotate"));

			for(int i = 0; i < hotkeyCount; ++i)
			{
				if(Input.GetButtonDown("Hotkey " + (i + 1)))
				{
					foreach(IHotkeyListener listener in hotkeys[i])
					{
						listener.HotkeyDown();
					}
				}
				if(Input.GetButtonUp("Hotkey " + (i + 1)))
				{
					foreach(IHotkeyListener listener in hotkeys[i])
					{
						listener.HotkeyUp();
					}
				}
			}
		}
	}

	public void UpdateActionName(int hotkey, IHotkeyListener listener, string actionName)
	{
		actionNames[new HotkeyKey(hotkey, listener)] = actionName;
		GenerateControlHint();
	}

	public override void AddHotkey(int hotkey, IHotkeyListener listener, string actionName)
	{
		base.AddHotkey(hotkey, listener, actionName);

		actionNames.Add(new HotkeyKey(hotkey, listener), actionName);
		GenerateControlHint();
	}

	public override void RemoveHotkey(int hotkey, IHotkeyListener listener)
	{
		base.RemoveHotkey(hotkey, listener);
		actionNames.Remove(new HotkeyKey(hotkey, listener));
		GenerateControlHint();
	}

	private void GenerateControlHint()
	{
		Dictionary<string, string[]> keyBindings = new Dictionary<string, string[]>();
		foreach(int hotkey in hotkeys.Keys)
		{
			string[] actions = new string[hotkeys[hotkey].Count];
			int i = 0;
			foreach(IHotkeyListener listener in hotkeys[hotkey])
			{
				actions[i] = actionNames[new HotkeyKey(hotkey, listener)];
				++i;
			}
			keyBindings.Add((hotkey + 1).ToString(), actions);
		}
		
		infoController.UpdateControlHint(keyBindings);
	}
}
