using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TutorialController : MonoBehaviour
{
	private static WaitForSecondsRealtime waitForTutorialUpdateInterval = null;
	private static WaitForSecondsRealtime waitForQuadTutorialUpdateInterval = null;
	private static bool skipped = false;

	[SerializeField] private float tutorialUpdateInterval = 1.0f;
	[SerializeField] private Text tutorialMessageField = null;
	[SerializeField] private GameObject buildingMenu = null;
	[SerializeField] private Text moduleControlDisplay = null;
	[SerializeField] private Text keyBindingDisplay = null;
	[SerializeField] private Button buildButton = null;
	[SerializeField] private Button buildAreaButton = null;
	[SerializeField] private Transform blueprintScrollPane = null;
	[SerializeField] private Button velocityButton = null;
	[SerializeField] private Color highlightColor = Color.red;
	[SerializeField] private GameObject nextButton = null;
	[SerializeField] private GameObject skipButton = null;
	private SpacecraftManager spacecraftManager = null;
	private QuestManager questManager = null;
	private Button highlightedButton = null;
	private ColorBlock oldColorBlock = new ColorBlock();
	private bool moduleControlHighlighted = false;
	private bool keyBindingHighlighted = false;
	private Color oldKeyBindingColor = Color.white;
	private bool next = false;
	private bool complete = false;

	private void Start()
	{
		if(waitForTutorialUpdateInterval == null || waitForQuadTutorialUpdateInterval == null)
		{
			waitForTutorialUpdateInterval = new WaitForSecondsRealtime(tutorialUpdateInterval);
			waitForQuadTutorialUpdateInterval = new WaitForSecondsRealtime(tutorialUpdateInterval * 4.0f);
		}

		spacecraftManager = SpacecraftManager.GetInstance();
		questManager = QuestManager.GetInstance();

		if(!skipped)
		{
			StartCoroutine(UpdateTutorial());
		}
		else
		{
			SkipTutorial();
		}
	}

	private IEnumerator UpdateTutorial()
	{
		yield return waitForQuadTutorialUpdateInterval;

		tutorialMessageField.gameObject.SetActive(true);
		skipButton.SetActive(true);

		HighlightButton(buildButton);
		tutorialMessageField.text = "Welcome to Space!\nIf you get stuck or go full retard, restart through the Main Menu\nStart by clicking 'Build'";
		do
		{
			yield return waitForTutorialUpdateInterval;
		}
		while(!buildingMenu.activeSelf);
		UnHighlightButton(buildButton);

		nextButton.SetActive(true);

		HighlightButton(buildAreaButton);
		tutorialMessageField.text = "For now you can only build in the Vicinity of Stations\nClick 'Show Build Area' to see the Range";
		do
		{
			yield return waitForTutorialUpdateInterval;
		}
		while(!next);
		next = false;
		UnHighlightButton(buildAreaButton);

		nextButton.SetActive(false);

		Button starterShipButton = null;
		for(int i = 0; i < blueprintScrollPane.childCount; ++i)
		{
			if((starterShipButton = blueprintScrollPane.GetChild(i).GetComponent<Button>()) != null && starterShipButton.GetComponentInChildren<Text>().text == "Starter Ship")
			{
				HighlightButton(starterShipButton);
				break;
			}
		}
		tutorialMessageField.text = "Load a basic Ship by selecting it from the Left\nAdd or remove Modules with the Buttons on the Right\nRotate Modules with [Q/E]\nBuilding Materials are automatically bought from the Station as long as their Stocks last";
		SpacecraftController playerSpacecraft = spacecraftManager.GetLocalPlayerMainSpacecraft();
		complete = false;
		do
		{
			yield return waitForQuadTutorialUpdateInterval;
		}
		while(playerSpacecraft.GetModules().Count < 6);
		if(starterShipButton != null)
		{
			UnHighlightButton(starterShipButton);
		}

		HighlightButton(buildButton);
		tutorialMessageField.text = "Save your Design by clicking 'Save Blueprint' on the Left\nClick 'Build' again to proceed";
		do
		{
			yield return waitForTutorialUpdateInterval;
		}
		while(buildingMenu.activeSelf);
		UnHighlightButton(buildButton);

		oldKeyBindingColor = moduleControlDisplay.color;
		moduleControlHighlighted = true;
		moduleControlDisplay.color = highlightColor;
		tutorialMessageField.text = "Zoom out [Scroll Wheel] and click the Name of the Station near you\nThen click 'Request Docking'\nDocking Permissions are shown by yellow Light from the affected Port\nClose the Station Menu and activate your own Port by pressing the Number Key displayed in the top left Corner of your Screen";
		complete = false;
		do
		{
			yield return waitForTutorialUpdateInterval;

			Dictionary<Vector2Int, Module> modules = playerSpacecraft.GetModules();
			foreach(Vector2Int modulePosition in modules.Keys)
			{
				if(modulePosition == modules[modulePosition].GetPosition() && modules[modulePosition] is DockingPort && ((DockingPort)modules[modulePosition]).IsActive())
				{
					complete = true;
					break;
				}
			}
		}
		while(!complete);
		moduleControlDisplay.color = oldKeyBindingColor;
		moduleControlHighlighted = false;

		oldKeyBindingColor = keyBindingDisplay.color;
		keyBindingHighlighted = true;
		keyBindingDisplay.color = highlightColor;
		tutorialMessageField.text = "Control your Ship with the Buttons shown on the right Side of your Screen\nNow align the activated Port of the Station with the Port of your Ship\nThen come really close to complete the Docking";
		do
		{
			yield return waitForTutorialUpdateInterval;
		}
		while(playerSpacecraft.GetDockedSpacecraftCount() <= 0);
		keyBindingDisplay.color = oldKeyBindingColor;
		keyBindingHighlighted = false;

		tutorialMessageField.text = "Being docked to a Station allows you to trade Materials or receive Rewards\nYou can accept Quests without docking, but you will need to dock to receive the Rewards\nNow accept any Quest in the Station Menu";
		do
		{
			yield return waitForTutorialUpdateInterval;
		}
		while(questManager.GetActiveQuestCount() <= 0);

		tutorialMessageField.text = "Close the Station Menu when you want to undock\nThen simply press the Activation Key for your Docking Port again";
		do
		{
			yield return waitForTutorialUpdateInterval;
		}
		while(playerSpacecraft.GetDockedSpacecraftCount() > 0);

		nextButton.SetActive(true);

		tutorialMessageField.text = "If a Quest requires you to find and dock to a Vessel, zoom out [Scroll Wheel] until you find the red Quest Vessel Marker\nClick the Vessel Name for more Information";
		do
		{
			yield return waitForTutorialUpdateInterval;
		}
		while(!next);
		next = false;
		
		HighlightButton(velocityButton);
		tutorialMessageField.text = "You can toggle Velocity Markers in the Top Bar\nThe orange Line shows your Velocity in Relation to the last clicked Target\nThe green Line shows the Difference between your Velocity and perfect Orbiting Velocity";
		do
		{
			yield return waitForTutorialUpdateInterval;
		}
		while(!next);
		next = false;
		UnHighlightButton(velocityButton);

		tutorialMessageField.text = "Quests usually reward you with Money and Materials which you can use for Trade or to expand your Spacecraft";
		do
		{
			yield return waitForTutorialUpdateInterval;
		}
		while(!next);
		next = false;

		nextButton.SetActive(false);
		tutorialMessageField.gameObject.SetActive(false);
		skipButton.SetActive(false);
	}

	public void NextMessage()
	{
		next = true;
	}

	public void SkipTutorial()
	{
		StopAllCoroutines();

		nextButton.SetActive(false);
		tutorialMessageField.gameObject.SetActive(false);
		skipButton.SetActive(false);

		if(highlightedButton != null)
		{
			UnHighlightButton(highlightedButton);
		}
		if(moduleControlHighlighted)
		{
			moduleControlDisplay.color = oldKeyBindingColor;
		}
		if(keyBindingHighlighted)
		{
			keyBindingDisplay.color = oldKeyBindingColor;
		}

		skipped = true;
	}

	private void HighlightButton(Button button)
	{
		highlightedButton = button;

		oldColorBlock = buildButton.colors;
		ColorBlock newColorBlock = oldColorBlock;
		newColorBlock.normalColor = highlightColor;
		newColorBlock.highlightedColor = highlightColor;
		newColorBlock.pressedColor = highlightColor;
		newColorBlock.selectedColor = highlightColor;
		button.colors = newColorBlock;
	}

	private void UnHighlightButton(Button button)
	{
		button.colors = oldColorBlock;

		highlightedButton = null;
	}
}
