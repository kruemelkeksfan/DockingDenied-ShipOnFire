﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputController : MonoBehaviour, IUpdateListener
{
	[SerializeField] protected int hotkeyCount = 10;
	protected TimeController timeController = null;
	protected Dictionary<int, HashSet<IHotkeyListener>> hotkeys = null;
	protected SpacecraftController spacecraft = null;
	protected bool flightControls = true;

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
		timeController = TimeController.GetInstance();

		spacecraft = GetComponent<SpacecraftController>();

		timeController.AddUpdateListener(this);
	}

	protected virtual void OnDestroy()
	{
		timeController?.RemoveUpdateListener(this);
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
		if(hotkeys.ContainsKey(hotkey))
		{
			hotkeys[hotkey].Remove(listener);
		}
	}

	public void SetFlightControls(bool flightControls)
	{
		this.flightControls = flightControls;
	}
}
