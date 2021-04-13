using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HotkeyModule : Module, IHotkeyListener
{
	[SerializeField] private GameObject moduleSettingMenu = null;
	[SerializeField] private InputField actionNameField = null;
	[SerializeField] private Dropdown hotkeySelection = null;
	private InputController inputController = null;
	private string actionName = null;
	private int hotkey = 0;

	public void ToggleModuleSettingMenu(bool deactivate = false)
	{
		if(moduleSettingMenu != null)
		{
			if(deactivate)
			{
				moduleSettingMenu.SetActive(false);
			}
			else
			{
				moduleSettingMenu.SetActive(!moduleSettingMenu.activeSelf);
			}
		}
	}
	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position);

		inputController = spacecraft.gameObject.GetComponent<InputController>();
		ActionNameChanged();
		HotkeyChanged();
	}

	public override void Deconstruct()
	{
		inputController.RemoveHotkey(hotkey, this);

		base.Deconstruct();
	}

	public void ActionNameChanged()
	{
		SetActionName(actionNameField.text);
	}

	public void HotkeyChanged()
	{
		SetHotkey(hotkeySelection.value);
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
		inputController.RemoveHotkey(this.hotkey, this);
		this.hotkey = hotkey;
		inputController.AddHotkey(hotkey, this, actionName);
		hotkeySelection.value = hotkey;
	}
}
