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
	private bool autoThrottle = false;
	private Vector3 autoThrottleSetting = Vector3.zero;

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

		if((flightControls || autoThrottleSetting != Vector3.zero) && !Input.GetButton("Rotate Camera"))
		{
			if(Input.GetButtonDown("AutoThrottle"))
			{
				autoThrottle = !autoThrottle;
			}
			else if(autoThrottle
				&& ((Input.GetAxis("Horizontal") < 0.0f && autoThrottleSetting.x > 0.0f)
				|| (Input.GetAxis("Horizontal") > 0.0f && autoThrottleSetting.x < 0.0f)
				|| (Input.GetAxis("Vertical") > 0.0f && autoThrottleSetting.y < 0.0f)
				|| (Input.GetAxis("Vertical") < 0.0f && autoThrottleSetting.y > 0.0f)
				|| (Input.GetAxis("Rotate") > 0.0f && autoThrottleSetting.z < 0.0f)
				|| (Input.GetAxis("Rotate") < 0.0f && autoThrottleSetting.z > 0.0f)
				|| Mathf.Abs(Input.GetAxis("Horizontal")) > Mathf.Abs(autoThrottleSetting.x)
				|| Mathf.Abs(Input.GetAxis("Vertical")) > Mathf.Abs(autoThrottleSetting.y)
				|| Mathf.Abs(Input.GetAxis("Rotate")) > Mathf.Abs(autoThrottleSetting.z)))
			{
				autoThrottle = false;
			}

			if(!autoThrottle)
			{
				autoThrottleSetting = new Vector3(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), Input.GetAxis("Rotate"));
			}

			spacecraft.SetThrottles(autoThrottleSetting.x, autoThrottleSetting.y, autoThrottleSetting.z);

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
