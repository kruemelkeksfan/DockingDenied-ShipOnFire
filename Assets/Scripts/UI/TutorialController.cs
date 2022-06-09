using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TutorialController : MonoBehaviour
{
	private static bool skipped = false;

	[SerializeField] private float tutorialUpdateInterval = 1.0f;
	[SerializeField] private GameObject tutorialPanel = null;
	[SerializeField] private Text tutorialMessageField = null;
	[SerializeField] private GameObject buildingMenu = null;
	[SerializeField] private Text moduleControlDisplay = null;
	[SerializeField] private Text keyBindingDisplay = null;
	[SerializeField] private Button buildButton = null;
	[SerializeField] private Transform blueprintScrollPane = null;
	[SerializeField] private Button overlaysButton = null;
	[SerializeField] private Color highlightColor = Color.red;
	[SerializeField] private GameObject nextButton = null;
	private TimeController timeController = null;
	private SpacecraftManager spacecraftManager = null;
	private QuestManager questManager = null;
	private Button highlightedButton = null;
	private ColorBlock oldColorBlock = new ColorBlock();
	private bool moduleControlHighlighted = false;
	private bool keyBindingHighlighted = false;
	private Color oldKeyBindingColor = Color.white;
	private bool next = false;
	private bool complete = false;
	private TimeController.Coroutine tutorialCoroutine = null;

	private void Start()
	{
		timeController = TimeController.GetInstance();
		spacecraftManager = SpacecraftManager.GetInstance();
		questManager = QuestManager.GetInstance();

		if(!skipped)
		{
			tutorialCoroutine = timeController.StartCoroutine(UpdateTutorial(), true);
		}
		else
		{
			SkipTutorial();
		}
	}

	private IEnumerator<float> UpdateTutorial()
	{
		tutorialPanel.SetActive(false);

		yield return tutorialUpdateInterval * 4.0f;

		tutorialPanel.SetActive(true);

		nextButton.SetActive(false);

		HighlightButton(buildButton);
		tutorialMessageField.text = "Welcome to Space!\nIf you get stuck, you can always restart through the Main Menu\nStart by clicking 'Build'";
		do
		{
			yield return tutorialUpdateInterval;
		}
		while(!buildingMenu.activeSelf);
		UnHighlightButton(buildButton);

		nextButton.SetActive(true);

		tutorialMessageField.text = "For now you can only build in the Vicinity of Stations";
		do
		{
			yield return tutorialUpdateInterval;
		}
		while(!next);
		next = false;

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
		tutorialMessageField.text = "Load the 'StarterShip' by selecting it from the Panel on the Left\nYou can add or remove Modules with the Buttons on the Right and rotate Modules with [Q/E]\nBuilding Materials are automatically bought from the Station as long as their Stock lasts";
		SpacecraftController playerSpacecraft = spacecraftManager.GetLocalPlayerMainSpacecraft();
		complete = false;
		do
		{
			yield return tutorialUpdateInterval;
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
			yield return tutorialUpdateInterval;
		}
		while(buildingMenu.activeSelf);
		UnHighlightButton(buildButton);

		nextButton.SetActive(true);

		tutorialMessageField.text = "Zoom out [Scroll Wheel] and click the Name of the Station near you\nThen click 'Request Docking'\nDocking Permissions are shown by yellow Light from the affected Port";
		do
		{
			yield return tutorialUpdateInterval;
		}
		while(!next);
		next = false;

		nextButton.SetActive(false);

		oldKeyBindingColor = moduleControlDisplay.color;
		moduleControlHighlighted = true;
		moduleControlDisplay.color = highlightColor;
		tutorialMessageField.text = "Close the Station Menu\nThen activate your own Port by pressing the Number Key displayed in the Top Left Corner";
		complete = false;
		do
		{
			yield return tutorialUpdateInterval;

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
			yield return tutorialUpdateInterval;
		}
		while(playerSpacecraft.GetDockedSpacecraftCount() <= 0);
		keyBindingDisplay.color = oldKeyBindingColor;
		keyBindingHighlighted = false;

		tutorialMessageField.text = "Being docked to a Station allows you to trade Materials or receive Rewards\nYou can accept Quests without docking, but you will need to dock to receive the Rewards\nNow accept any Quest in the Station Menu";
		do
		{
			yield return tutorialUpdateInterval;
		}
		while(questManager.GetActiveQuestCount() <= 0);

		tutorialMessageField.text = "Close the Station Menu when you want to undock\nThen simply press the Activation Key for your Docking Port again";
		do
		{
			yield return tutorialUpdateInterval;
		}
		while(playerSpacecraft.GetDockedSpacecraftCount() > 0);

		nextButton.SetActive(true);

		tutorialMessageField.text = "If a Quest requires you to find and dock to a Vessel, zoom out [Scroll Wheel] until you find the red Quest Vessel Marker\nClick the Vessel Name for more Information";
		do
		{
			yield return tutorialUpdateInterval;
		}
		while(!next);
		next = false;
		
		HighlightButton(overlaysButton);
		tutorialMessageField.text = "The red Line shows your Velocity in Relation to the last clicked Target\nYou can toggle many other helpful Indicators under 'Overlays'";
		do
		{
			yield return tutorialUpdateInterval;
		}
		while(!next);
		next = false;
		UnHighlightButton(overlaysButton);

		tutorialMessageField.text = "Quests usually reward you with Money and Materials\nwhich you can use for Trade or to expand your Spacecraft";
		do
		{
			yield return tutorialUpdateInterval;
		}
		while(!next);
		next = false;

		tutorialMessageField.text = "This should be all you need to know right now\nRocket Science itself is trivial and left as an Exercise to the Reader\nGood Luck!";
		do
		{
			yield return tutorialUpdateInterval;
		}
		while(!next);
		next = false;

		nextButton.SetActive(false);
		tutorialPanel.gameObject.SetActive(false);
	}

	public void NextMessage()
	{
		next = true;
	}

	public void SkipTutorial()
	{
		if(tutorialCoroutine != null)
		{
			timeController.StopCoroutine(tutorialCoroutine);
			tutorialCoroutine = null;
		}

		nextButton.SetActive(false);
		tutorialPanel.SetActive(false);

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
