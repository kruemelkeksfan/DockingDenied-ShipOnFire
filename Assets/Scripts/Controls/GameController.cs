using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour, IUpdateListener
{
	private static GameController instance = null;
	private static string deathMessage = null;
	
	private UpdateController updateController = null;
	private MenuController menuController = null;
	private ToggleController toggleController = null;
	private bool killScene = false;
	private bool sceneDead = false;

	public static GameController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		instance = this;
	}

	private void Start()
	{
		updateController = UpdateController.GetInstance();
		menuController = MenuController.GetInstance();

		updateController.AddUpdateListener(this);
	}

	private void OnDestroy()
	{
		updateController?.RemoveUpdateListener(this);
	}

	public void UpdateNotify()
	{
		if(toggleController == null)
		{
			toggleController = ToggleController.GetInstance();
		}

		if(Input.GetButtonUp("Main Menu"))
		{
			menuController.ToggleMainMenu();
		}

		if(killScene && !sceneDead)
		{
			sceneDead = true;
			toggleController = null;
			SceneManager.sceneLoaded += DisplayDeathMessage;
			SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);
		}
	}

	private void DisplayDeathMessage(Scene scene, LoadSceneMode mode)
	{
		if(deathMessage != null)
		{
			InfoController.GetInstance().AddMessage(deathMessage);
			deathMessage = null;
		}
	}

	public void Restart(string message = null)
	{
		if(!string.IsNullOrEmpty(message))
		{
			deathMessage = message;
		}

		killScene = true;
	}

	public void Quit()
	{
		Application.Quit();
	}
}
