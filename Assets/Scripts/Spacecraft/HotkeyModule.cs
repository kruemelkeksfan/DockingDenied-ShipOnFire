using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HotkeyModule : Module, IHotkeyListener
{
    [SerializeField] private GameObject moduleSettingMenu = null;
	[SerializeField] private Text actionName = null;
	[SerializeField] private Dropdown hotkeySelection = null;
    private InputController inputController = null;
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
        HotkeyChanged();
    }

	public override void Deconstruct()
	{
		inputController.RemoveHotkey(hotkey, this);

		base.Deconstruct();
	}

    public void ActionNameChanged()
	{
        (inputController as KeyboardInputController)?.UpdateActionName(hotkey, this, actionName.text);
	}

	public void HotkeyChanged()
	{
        inputController.RemoveHotkey(hotkey, this);
        hotkey = hotkeySelection.value;
        inputController.AddHotkey(hotkey, this, (actionName.text != string.Empty ? actionName.text : moduleName) );
	}

    public virtual void HotkeyDown()
	{

	}

	public virtual void HotkeyUp()
	{

	}
}
