using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
	private static GameController instance = null;
	private ToggleController toggleController = null;
	private string deathMessage = null;
	private bool killScene = false;
	private bool sceneDead = false;

	public static GameController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		if(instance != null)
		{
			GameObject.Destroy(gameObject);
		}
		else
		{
			GameObject.DontDestroyOnLoad(this.gameObject);
			instance = this;
		}
	}

	private void Update()
	{
		if(toggleController == null)
		{
			toggleController = ToggleController.GetInstance();
		}

		if(Input.GetButtonUp("Main Menu"))
		{
			toggleController.ToggleGroup("MainMenu");
		}

		if(killScene && ! sceneDead)
		{
			sceneDead = true;
			toggleController = null;
			SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);
		}
	}

	public void Restart(string message = null)
	{
		if(!string.IsNullOrEmpty(message))
		{
			deathMessage = message;
			SceneManager.sceneLoaded += DisplayDeathMessage;
		}

		killScene = true;
	}

	private void DisplayDeathMessage(Scene scene, LoadSceneMode mode)
	{
		InfoController.GetInstance().AddMessage(deathMessage);
		deathMessage = null;
	}

	public void Quit()
	{
		Application.Quit();
	}
}
