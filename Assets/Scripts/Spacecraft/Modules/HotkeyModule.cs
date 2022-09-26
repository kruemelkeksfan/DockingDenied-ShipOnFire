using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HotkeyModule : Module, IHotkeyListener
{
	private InputController inputController = null;
	private Dropdown hotkeySelection = null;
	private int hotkey = 0;
	private bool active = false;

	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position, listenUpdates, listenFixedUpdates);

		inputController = spacecraft.gameObject.GetComponent<InputController>();

		if(moduleMenu != null)
		{
			hotkeySelection = settingPanel.GetComponentInChildren<Dropdown>();
			SetHotkey(hotkeySelection.value);

			settingPanel.GetComponentInChildren<Dropdown>().onValueChanged.AddListener(delegate
			{
				SetHotkey(hotkeySelection.value);
			});
		}
		else
		{
			SetHotkey(0);
		}
	}

	public override void Deconstruct()
	{
		inputController?.RemoveHotkey(hotkey, this);

		base.Deconstruct();
	}

	public virtual void HotkeyDown()
	{
		active = !active;
		SetCustomModuleName(customModuleName);
	}

	public virtual void HotkeyUp()
	{
		
	}

	public int GetHotkey()
	{
		return hotkey;
	}

	public bool IsActive()
	{
		return active;
	}

	public override void SetCustomModuleName(string customModuleName)
	{
		base.SetCustomModuleName(customModuleName);

		(inputController as KeyboardInputController)?.UpdateActionName(hotkey, this, customModuleName + " " + (active ? "[on]" : "[off]"));
	}

	public void SetHotkey(int hotkey)
	{
		// TODO: Clean up the ?.-Operators by figuring out a Software Architecture that does not need Hotkeys when no KeyboardInputController is present
		inputController?.RemoveHotkey(this.hotkey, this);
		this.hotkey = hotkey;
		inputController?.AddHotkey(hotkey, this, customModuleName + " " + (active ? "[on]" : "[off]"));
		if(moduleMenu != null)
		{
			hotkeySelection.value = hotkey;
		}
	}
}
