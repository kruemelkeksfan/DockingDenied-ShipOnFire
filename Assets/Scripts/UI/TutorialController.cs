using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TutorialController : MonoBehaviour
{
	private delegate bool CancelCondition();

	private static WaitForSecondsRealtime waitForTutorialUpdateInterval = null;
	private static WaitForSecondsRealtime waitForQuadTutorialUpdateInterval = null;
	private static bool skipped = false;

	[SerializeField] private float tutorialUpdateInterval = 1.0f;
	[SerializeField] private Text tutorialMessageField = null;
	[SerializeField] private GameObject buildingMenu = null;
	[SerializeField] private Text keyBindingDisplay = null;
	[SerializeField] private Button buildButton = null;
	[SerializeField] private Button buildAreaButton = null;
	[SerializeField] private Button velocityButton = null;
	[SerializeField] private Color highlightColor = Color.red;
	[SerializeField] private GameObject nextButton = null;
	[SerializeField] private GameObject skipButton = null;
	private ToggleController toggleController = null;
	private SpacecraftManager spacecraftManager = null;
	private QuestManager questManager = null;
	private Button highlightedButton = null;
	private ColorBlock oldColorBlock = new ColorBlock();
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

		toggleController = ToggleController.GetInstance();
		spacecraftManager = SpacecraftManager.GetInstance();
		questManager = QuestManager.GetInstance();

		if(!skipped)
		{
			StartCoroutine(UpdateTutorial());
		}
	}

	private IEnumerator UpdateTutorial()
	{
		yield return waitForQuadTutorialUpdateInterval;

		tutorialMessageField.gameObject.SetActive(true);
		skipButton.SetActive(true);

		tutorialMessageField.text = "Welcome to Space!\nStart constructing your Spacecraft by clicking 'Build' in the top-left Corner";
		HighlightButton(buildButton);
		do
		{
			yield return waitForTutorialUpdateInterval;
		}
		while(!buildingMenu.activeSelf);
		UnHighlightButton(buildButton);

		tutorialMessageField.text = "For now you can only build in the Vicinity of Stations\nTo see the Building Range, click 'Show Build Area' in the Top Bar";
		HighlightButton(buildAreaButton);
		do
		{
			yield return waitForTutorialUpdateInterval;
		}
		while(!toggleController.IsGroupToggled("BuildAreaIndicators"));
		UnHighlightButton(buildAreaButton);

		tutorialMessageField.text = "A Starter Ship should at least have:\nSome Solid Containers, a Docking Port, a Thruster in each Direction and a Solar Module\nYou can rotate Modules [Q/E]\nBuilding Materials are automatically bought from the Station\nHowever their Stocks and your Money are limited, so don't get carried away";
		complete = false;
		do
		{
			yield return waitForQuadTutorialUpdateInterval;

			Dictionary<Vector2Int, Module> modules = spacecraftManager.GetLocalPlayerMainSpacecraft().GetModules();
			int containerCount = 0;
			int portCount = 0;
			int thrusterCount = 0;
			int solarCount = 0;
			foreach(Vector2Int modulePosition in modules.Keys)
			{
				if(modulePosition == modules[modulePosition].GetPosition())
				{
					if(modules[modulePosition] is Container)
					{
						++containerCount;
					}
					if(modules[modulePosition] is DockingPort)
					{
						++portCount;
					}
					if(modules[modulePosition] is Thruster)
					{
						++thrusterCount;
					}
					if(modules[modulePosition] is SolarModule)
					{
						++solarCount;
					}
				}
			}
			complete = containerCount >= 1 && portCount >= 1 && thrusterCount >= 4 && solarCount >= 1;
		}
		while(!complete);

		HighlightButton(buildButton);
		tutorialMessageField.text = "You can save your Design by clicking 'Save Blueprint' on the left\nThat Way you don't have to rebuild it every Time you need it\nWhen you are done, close the Building Menu by clicking 'Build' in the top-left Corner again";
		do
		{
			yield return waitForTutorialUpdateInterval;
		}
		while(buildingMenu.activeSelf);
		UnHighlightButton(buildButton);

		tutorialMessageField.text = "Zoom out [Scroll Wheel] and click the Name of the Station near you\nThen click 'Request Docking' in the Station Menu\nDocking Permissions stay active for 2 Minutes and are indicated by yellow Light emerging from the affected Port\nActivate your own Docking Port by pressing the Number Key displayed in the top left Corner of your Screen";
		Spacecraft playerSpacecraft = spacecraftManager.GetLocalPlayerMainSpacecraft();
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

		oldKeyBindingColor = keyBindingDisplay.color;
		keyBindingHighlighted = true;
		keyBindingDisplay.color = highlightColor;
		tutorialMessageField.text = "Control your Ship with the tiny Set of Buttons shown on the right Side of your Screen\nNow align the activated Port of the Station and with that on your Ship\nThen come really close to complete the Docking";
		do
		{
			yield return waitForTutorialUpdateInterval;
		}
		while(playerSpacecraft.GetDockedSpacecraftCount() <= 0);
		keyBindingDisplay.color = oldKeyBindingColor;
		keyBindingHighlighted = false;

		tutorialMessageField.text = "Being docked to a Station allows you to trade Materials or receive Rewards for completed Quests\nYou can accept Quests without docking, but you will later need to dock to receive the Rewards\nNow accept any Quest in the Station Menu";
		do
		{
			yield return waitForTutorialUpdateInterval;
		}
		while(questManager.GetActiveQuestCount() <= 0);

		tutorialMessageField.text = "Close the Station Menu when you want to undock again\nThen simply press the Activation Key for your Docking Port again";
		do
		{
			yield return waitForTutorialUpdateInterval;
		}
		while(playerSpacecraft.GetDockedSpacecraftCount() > 0);

		nextButton.SetActive(true);

		tutorialMessageField.text = "If a Quest requires you to find and dock to a Vessel, zoom out [Scroll Wheel] until you find the red Quest Vessel Marker";
		do
		{
			yield return waitForTutorialUpdateInterval;
		}
		while(!next);
		next = false;
		
		HighlightButton(velocityButton);
		tutorialMessageField.text = "You can toggle Velocity Markers in the Top Bar\nThe blue Line shows your Velocity in Relation to the last clicked Target\nThe green Line shows the Difference between your Velocity and perfect Orbiting Velocity";
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
