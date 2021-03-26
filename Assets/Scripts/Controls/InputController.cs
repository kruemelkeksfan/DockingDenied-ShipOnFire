using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputController : MonoBehaviour, IUpdateListener
{
	public delegate void Action();

	[SerializeField] protected int hotkeyCount = 10;
	protected Dictionary<int, Dictionary<int, Action>> hotkeys = null;
	private int actionCounter = 0;
	protected Spacecraft spacecraft = null;

	protected virtual void Awake()
	{
		hotkeys = new Dictionary<int, Dictionary<int, Action>>(hotkeyCount);
		for(int i = 0; i < hotkeyCount; ++i)
		{
			hotkeys[i] = new Dictionary<int, Action>();
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

	public virtual int AddHotkey(int hotkey, Action action, string actionName)
	{
		if(hotkeys.ContainsKey(hotkey))
		{
			++actionCounter;
			hotkeys[hotkey].Add(actionCounter, action);
			return actionCounter;
		}
		else
		{
			Debug.LogWarning("Trying to register invalid Hotkey " + hotkey + " with Action " + action + "!");
			return -1;
		}
	}

	public virtual void RemoveHotkey(int hotkey, int actionID)
	{
		hotkeys[hotkey].Remove(actionID);
	}
}
