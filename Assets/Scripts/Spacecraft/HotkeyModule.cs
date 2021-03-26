using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HotkeyModule : Module
{
    [SerializeField] private GameObject moduleSettingMenu = null;
	[SerializeField] private Text actionName = null;
	[SerializeField] private Dropdown hotkeySelection = null;
    private InputController inputController = null;
    private int hotkey = 0;
    private int hotkeyID = -1;

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

    public void ActionNameChanged()
	{
        (inputController as KeyboardInputController)?.UpdateActionName(hotkey, hotkeyID, actionName.text);
	}

	public void HotkeyChanged()
	{
        if(hotkeyID >= 0)
		{
            inputController.RemoveHotkey(hotkey, hotkeyID);
		}
        hotkey = hotkeySelection.value;
        hotkeyID = inputController.AddHotkey(hotkey, delegate { HotkeyPressed(); }, (actionName.text != string.Empty ? actionName.text : "Docking Port") );
	}

    public virtual void HotkeyPressed()
	{

	}
}
