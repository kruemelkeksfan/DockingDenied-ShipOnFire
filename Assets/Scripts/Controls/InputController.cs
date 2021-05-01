using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputController : MonoBehaviour, IUpdateListener
{
	protected static int flightControls = 0;

	[SerializeField] protected int hotkeyCount = 10;
	protected Dictionary<int, HashSet<IHotkeyListener>> hotkeys = null;
	protected Spacecraft spacecraft = null;

	public static void SetFlightControls(bool flightControls)
	{
		if(flightControls)
		{
			++InputController.flightControls;
		}
		else
		{
			--InputController.flightControls;
		}
	}

	protected virtual void Awake()
	{
		hotkeys = new Dictionary<int, HashSet<IHotkeyListener>>(hotkeyCount);
		for(int i = 0; i < hotkeyCount; ++i)
		{
			hotkeys[i] = new HashSet<IHotkeyListener>();
		}
	}

    protected virtual void Start()
	{
		spacecraft = GetComponent<Spacecraft>();
		spacecraft.AddUpdateListener(this);
	}

	public virtual void UpdateNotify()
	{
		
	}

	public virtual void AddHotkey(int hotkey, IHotkeyListener listener, string actionName)
	{
		if(hotkeys.ContainsKey(hotkey))
		{
			hotkeys[hotkey].Add(listener);
		}
		else
		{
			Debug.LogWarning("Trying to register invalid Hotkey " + hotkey + " with Action " + actionName + "!");
		}
	}

	public virtual void RemoveHotkey(int hotkey, IHotkeyListener listener)
	{
		hotkeys[hotkey].Remove(listener);
	}
}
