using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HotkeyModule : Module, IHotkeyListener
{
	[SerializeField] private InputField actionNameField = null;
	[SerializeField] private Dropdown hotkeySelection = null;
	private InputController inputController = null;
	private string actionName = null;
	private int hotkey = 0;

	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position);

		inputController = spacecraft.gameObject.GetComponent<InputController>();
		SetActionName(actionNameField.text);
		SetHotkey(hotkeySelection.value);
	}

	public override void Deconstruct()
	{
		inputController.RemoveHotkey(hotkey, this);

		base.Deconstruct();
	}

	public virtual void HotkeyDown()
	{

	}

	public virtual void HotkeyUp()
	{

	}

	public string GetActionName()
	{
		return actionName;
	}

	public int GetHotkey()
	{
		return hotkey;
	}

	public virtual void SetActionName(string actionName)
	{
		this.actionName = (!string.IsNullOrEmpty(actionName) ? actionName : moduleName);
		(inputController as KeyboardInputController)?.UpdateActionName(hotkey, this, actionName);
		actionNameField.text = actionName;
		actionNameField.placeholder.enabled = false;
	}

	public void SetHotkey(int hotkey)
	{
		// TODO: Clean up the ?.-Operators by figuring out a Software Architecture that does not need Hotkeys when no KeyboardInputController is present
		inputController?.RemoveHotkey(this.hotkey, this);
		this.hotkey = hotkey;
		inputController?.AddHotkey(hotkey, this, actionName);
		hotkeySelection.value = hotkey;
	}
}
