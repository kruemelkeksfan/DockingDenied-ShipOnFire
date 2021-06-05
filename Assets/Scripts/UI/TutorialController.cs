using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TutorialController : MonoBehaviour
{
	private delegate bool CancelCondition();

	private static WaitForSecondsRealtime waitForTutorialUpdateInterval = null;
	private static bool skipped = false;

	[SerializeField] private float tutorialUpdateInterval = 1.0f;
	[SerializeField] private GameObject buildingMenu = null;
	[SerializeField] private GameObject skipButton = null;
	private InfoController infoController = null;
	private ToggleController toggleController = null;
	private SpacecraftManager spacecraftManager = null;
	private QuestManager questManager = null;
	private Transform cameraTransform = null;
	private float startZoom = 0.0f;
	private bool complete = false;

	private void Start()
	{
		if(waitForTutorialUpdateInterval == null)
		{
			waitForTutorialUpdateInterval = new WaitForSecondsRealtime(tutorialUpdateInterval);
		}

		infoController = InfoController.GetInstance();
		toggleController = ToggleController.GetInstance();
		spacecraftManager = SpacecraftManager.GetInstance();
		questManager = QuestManager.GetInstance();

		cameraTransform = Camera.main.GetComponent<Transform>();
		startZoom = cameraTransform.position.z;

		if(!skipped)
		{
			StartCoroutine(UpdateTutorial());
		}
	}

	private IEnumerator UpdateTutorial()
	{
		yield return waitForTutorialUpdateInterval;

		skipButton.SetActive(true);

		infoController.AddMessage("Welcome to Space!");

		while(!buildingMenu.activeSelf)
		{
			infoController.AddMessage("Start constructing your Spacecraft by clicking 'Build' in the top-left Corner");
			yield return WaitForClear(delegate
			{
				return buildingMenu.activeSelf;
			});
		}

		while(!toggleController.IsGroupToggled("BuildAreaIndicators"))
		{
			infoController.AddMessage("For now you can only build in the Vicinity of Stations");
			infoController.AddMessage("To see the Building Range, click 'Show Build Area' in the Top Bar");
			yield return WaitForClear(delegate
			{
				return toggleController.IsGroupToggled("BuildAreaIndicators");
			});
		}

		complete = false;
		HashSet<DockingPort> dockingPorts = new HashSet<DockingPort>();
		while(!complete)
		{
			infoController.AddMessage("A Starter Ship should at least have:");
			infoController.AddMessage("Some Solid Containers, a Docking Port, a Thruster in each Direction and a Solar Module");
			infoController.AddMessage("You can rotate Modules [Q/E]");
			infoController.AddMessage("Building Materials are automatically bought from the Station");
			infoController.AddMessage("However their Stocks and your Money are limited, so don't get carried away");
			yield return WaitForClear(delegate
			{
				return complete = complete || VerifyStartShipRequirements();
			});

			complete = complete || VerifyStartShipRequirements();
			if(complete)
			{
				Dictionary<Vector2Int, Module> modules = spacecraftManager.GetLocalPlayerMainSpacecraft().GetModules();
				foreach(Vector2Int modulePosition in modules.Keys)
				{
					if(modulePosition == modules[modulePosition].GetPosition() && modules[modulePosition] is DockingPort)
					{
						dockingPorts.Add((DockingPort)modules[modulePosition]);
					}
				}
			}
		}

		while(buildingMenu.activeSelf)
		{
			infoController.AddMessage("You can save your Design by clicking the Button on the left");
			infoController.AddMessage("That Way you don't have to rebuild it every Time you need it");
			infoController.AddMessage("When you are done, close the Building Menu by clicking 'Build' in the top-left Corner again");
			yield return WaitForClear(delegate
			{
				return !buildingMenu.activeSelf;
			});
		}

		complete = false;
		while(!complete)
		{
			infoController.AddMessage("Zoom out [Scroll Wheel] and click the Name of the Station near you");
			infoController.AddMessage("Then click 'Request Docking' in the Station Menu");
			infoController.AddMessage("Docking Permissions stay active for 2 Minutes and are indicated by yellow Light emerging from the affected Port");
			infoController.AddMessage("Activate your own Docking Port by pressing the Number Key displayed in the top left Corner of your Screen");
			yield return WaitForClear(delegate
			{
				foreach(DockingPort port in dockingPorts)
				{
					if(port.IsActive())
					{
						return true;
					}
				}
				return false;
			});

			foreach(DockingPort port in dockingPorts)
			{
				if(port.IsActive())
				{
					complete = true;
					break;
				}
			}
		}

		complete = false;
		while(!complete)
		{
			infoController.AddMessage("Now align the activated Port of the Station and with that on your Ship");
			infoController.AddMessage("Then come really close to complete the Docking");
			yield return WaitForClear(delegate
			{
				foreach(DockingPort port in dockingPorts)
				{
					if(!port.IsFree())
					{
						return true;
					}
				}
				return false;
			});

			foreach(DockingPort port in dockingPorts)
			{
				if(!port.IsFree())
				{
					complete = true;
					break;
				}
			}
		}

		while(questManager.GetActiveQuestCount() <= 0)
		{
			infoController.AddMessage("Being docked to a Station allows you to trade Materials or receive Rewards for completed Quests");
			infoController.AddMessage("You can accept Quests from any Station without docking, but you will need to dock to receive the Rewards after completing the Quest");
			infoController.AddMessage("Now accept any Quest in the Station Menu");
			yield return WaitForClear(delegate
			{
				return questManager.GetActiveQuestCount() > 0;
			});
		}

		yield return WaitForClear(delegate
		{
			return false;
		});
		infoController.AddMessage("Simply press the Activation Key for your Docking Port again to undock");
		yield return WaitForClear(delegate
		{
			return false;
		});
		infoController.AddMessage("If a Quest requires you to find and dock to a Vessel, zoom out [Scroll Wheel] until you find the red Quest Vessel Marker");
		yield return WaitForClear(delegate
		{
			return false;
		});
		infoController.AddMessage("You can toggle Velocity Markers in the Top Bar");
		infoController.AddMessage("The blue Line shows your Velocity in Relation to the last clicked Target");
		infoController.AddMessage("The green Line shows the Difference between your Velocity and perfect Orbiting Velocity");

		if(questManager.GetActiveQuestCount() <= 0)
		{
			infoController.AddMessage("Quests usually reward you with Money and Materials which you can Trade and use to expand your Spacecraft");
		}
	}

	public void SkipTutorial()
	{
		StopAllCoroutines();
		skipButton.SetActive(false);
		skipped = true;
	}

	private IEnumerator WaitForClear(CancelCondition cancelCondition)
	{
		while(infoController.GetMessageCount() > 0 && !cancelCondition())
		{
			yield return waitForTutorialUpdateInterval;
		}
	}

	private bool VerifyStartShipRequirements()
	{
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
		if(containerCount >= 1 && portCount >= 1 && thrusterCount >= 4 && solarCount >= 1)
		{
			return true;
		}
		else
		{
			return false;
		}
	}
}
