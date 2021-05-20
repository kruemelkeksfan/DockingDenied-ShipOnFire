using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
	private ToggleController toggleController = null;

	private void Start()
	{
		toggleController = ToggleController.GetInstance();
	}

	private void Update()
	{
		if(Input.GetButtonUp("Main Menu"))
		{
			toggleController.ToggleGroup("MainMenu");
		}
	}

	public void Restart()
	{
		SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);
	}

	public void Quit()
	{
		Application.Quit();
	}
}
