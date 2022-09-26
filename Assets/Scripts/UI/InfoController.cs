using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class InfoController : MonoBehaviour, IUpdateListener, IListener
{
	private struct Message
	{
		public string message;
		public float timestamp;
	}

	private static InfoController instance = null;
	private static bool helpActive = true;

	[SerializeField] private Text messageField = null;
	[SerializeField] private float skippingMessageDuration = 0.2f;
	[SerializeField] private float messageDuration = 6.0f;
	[SerializeField] private Text controlHint = null;
	[SerializeField] private Text resourceDisplay = null;
	[SerializeField] private Text secondaryDisplay = null;
	[SerializeField] private GameObject keyBindingDisplay = null;
	[SerializeField] private Text showFlightInfoButtonText = null;
	[SerializeField] private AudioClip warningAudio = null;
	[SerializeField] private Text colorblindModeText = null;
	[SerializeField] private GameObject confirmationPanel = null;
	[SerializeField] private Text confirmationPanelText = null;
	[SerializeField] private Button confirmationPanelConfirmButton = null;
	private TimeController timeController = null;
	private AudioController audioController = null;
	private GravityWellController gravityWellController = null;
	private Queue<Message> messages = null;
	private float lastDequeue = 0.0f;
	private Dictionary<string, uint> buildingCosts = null;
	private float moduleMass = 0.0f;
	private SpacecraftController playerSpacecraft = null;
	private Transform playerSpacecraftTransform = null;
	private Rigidbody2D playerSpacecraftRigidbody = null;
	private InventoryController playerSpacecraftInventoryController = null;
	private StringBuilder textBuilder = null;
	private bool updateResourceDisplay = true;
	private bool updateBuildingResourceDisplay = true;
	private bool showBuildingResourceDisplay = false;
	private bool showFlightInfo = false;
	private double expiryTime = -1.0;
	private bool flightControls = true;
	private bool colorblindMode = false;

	public static InfoController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");                                               // Decimal Points FTW!!elf!

		messages = new Queue<Message>();
		textBuilder = new StringBuilder();

		instance = this;
	}

	private void Start()
	{
		audioController = AudioController.GetInstance();
		gravityWellController = GravityWellController.GetInstance();

		SpacecraftManager.GetInstance().AddSpacecraftChangeListener(this);
		Notify();

		timeController = TimeController.GetInstance();
		timeController.AddUpdateListener(this);
	}

	private void OnDestroy()
	{
		timeController?.RemoveUpdateListener(this);
	}

	public void UpdateNotify()
	{
		if(updateResourceDisplay)
		{
			textBuilder.Clear();
			textBuilder.Append(playerSpacecraftInventoryController.GetMoney());
			textBuilder.Append("$ / ");
			textBuilder.Append(Mathf.RoundToInt(playerSpacecraftRigidbody.mass));
			textBuilder.Append(" t / Energy - ");
			textBuilder.Append(playerSpacecraftInventoryController.GetEnergyChargeString(true));
			textBuilder.Append(" / Xe - ");
			textBuilder.Append(playerSpacecraftInventoryController.GetGoodAmount("Xenon"));
			textBuilder.Append(" t / H2 - ");
			textBuilder.Append(playerSpacecraftInventoryController.GetGoodAmount("Hydrogen"));
			textBuilder.Append(" t / O2 - ");
			textBuilder.Append(playerSpacecraftInventoryController.GetGoodAmount("Oxygen"));
			textBuilder.Append(" t / Food - ");
			textBuilder.Append(playerSpacecraftInventoryController.GetGoodAmount("Food"));
			textBuilder.Append(" t / H2O - ");
			textBuilder.Append(playerSpacecraftInventoryController.GetGoodAmount("Water"));
			resourceDisplay.text = textBuilder.ToString();

			updateResourceDisplay = false;
		}

		if(showBuildingResourceDisplay && updateBuildingResourceDisplay)
		{
			textBuilder.Clear();
			textBuilder.Append("Steel - ");
			textBuilder.Append(playerSpacecraftInventoryController.GetGoodAmount("Steel"));
			if(buildingCosts != null)
			{
				textBuilder.Append(" (");
				textBuilder.Append(buildingCosts.ContainsKey("Steel") ? buildingCosts["Steel"] : 0);
				textBuilder.Append(")");
			}
			textBuilder.Append(" / Aluminium - ");
			textBuilder.Append(playerSpacecraftInventoryController.GetGoodAmount("Aluminium"));
			if(buildingCosts != null)
			{
				textBuilder.Append(" (");
				textBuilder.Append(buildingCosts.ContainsKey("Aluminium") ? buildingCosts["Aluminium"] : 0);
				textBuilder.Append(")");
			}
			textBuilder.Append(" / Copper - ");
			textBuilder.Append(playerSpacecraftInventoryController.GetGoodAmount("Copper"));
			if(buildingCosts != null)
			{
				textBuilder.Append(" (");
				textBuilder.Append(buildingCosts.ContainsKey("Copper") ? buildingCosts["Copper"] : 0);
				textBuilder.Append(")");
			}
			textBuilder.Append(" / Gold - ");
			textBuilder.Append(playerSpacecraftInventoryController.GetGoodAmount("Gold"));
			if(buildingCosts != null)
			{
				textBuilder.Append(" (");
				textBuilder.Append(buildingCosts.ContainsKey("Gold") ? buildingCosts["Gold"] : 0);
				textBuilder.Append(")");
			}
			textBuilder.Append(" / Silicon - ");
			textBuilder.Append(playerSpacecraftInventoryController.GetGoodAmount("Silicon"));
			if(buildingCosts != null)
			{
				textBuilder.Append(" (");
				textBuilder.Append(buildingCosts.ContainsKey("Silicon") ? buildingCosts["Silicon"] : 0);
				textBuilder.Append(")");
			}
			if(buildingCosts != null)
			{
				textBuilder.Append(" / Mass - ");
				textBuilder.Append(Mathf.RoundToInt(moduleMass));
				textBuilder.Append(" t");
			}
			secondaryDisplay.text = textBuilder.ToString();

			updateBuildingResourceDisplay = false;
		}
		else if(!showBuildingResourceDisplay)
		{
			if(flightControls)
			{
				if(showFlightInfo)
				{
					double fixedTime = timeController.GetFixedTime();
					playerSpacecraft.CalculateOrbitalElements(
						gravityWellController.LocalToGlobalPosition(playerSpacecraftTransform.position),
						playerSpacecraftRigidbody.velocity,
						fixedTime);
					float playerVelocity = playerSpacecraft.IsOnRails() ? (float)playerSpacecraft.CalculateVelocity(fixedTime).Magnitude() : playerSpacecraftRigidbody.velocity.magnitude;
					int periapsis = (int)(playerSpacecraft.CalculatePeriapsisAltitude() / 1000.0);
					int apoapsis = (int)(playerSpacecraft.CalculateApoapsisAltitude() / 1000.0);

					textBuilder.Clear();
					textBuilder.Append("Alt - ");
					textBuilder.Append((int)(gravityWellController.LocalToGlobalPosition(playerSpacecraftTransform.position).Magnitude() / 1000.0));
					textBuilder.Append(" km / Speed - ");
					textBuilder.Append((playerVelocity / 1000.0f).ToString("F4"));
					textBuilder.Append(" km/s / Peri - ");
					if(periapsis > 0)
					{
						textBuilder.Append(periapsis.ToString());
						textBuilder.Append(" km / Apo - ");
					}
					else
					{
						textBuilder.Append("¯\\_(ツ)_/¯ / Apo - ");
					}
					if(apoapsis > 0)
					{
						textBuilder.Append(apoapsis.ToString());
						textBuilder.Append(" km");
					}
					else
					{
						textBuilder.Append("¯\\_(ツ)_/¯");
					}

					double time = timeController.GetTime();
					if(expiryTime > 0.0f)
					{
						if(time >= expiryTime)
						{
							expiryTime = -1.0f;
						}
						else
						{
							textBuilder.Append(" / Docking Permission - ");
							textBuilder.Append((int)(expiryTime - time));
							textBuilder.Append(" s");
						}
					}
					secondaryDisplay.text = textBuilder.ToString();
				}
				else
				{
					secondaryDisplay.text = string.Empty;
				}
			}
			else
			{
				secondaryDisplay.text = "Menu open - Flight Controls blocked";
			}
		}

		if(Input.GetButtonDown("ShowHelp"))
		{
			helpActive = !helpActive;
		}
		if(keyBindingDisplay.activeSelf != helpActive)
		{
			keyBindingDisplay.SetActive(helpActive);
		}

		float messageDuration = Input.GetButton("Skip Info Log") ? skippingMessageDuration : this.messageDuration;

		float realtimeSinceStartup = Time.realtimeSinceStartup;
		while(messages.Count > 0 && messages.Peek().timestamp + messageDuration < realtimeSinceStartup && lastDequeue + messageDuration < realtimeSinceStartup)
		{
			messages.Dequeue();
			lastDequeue = realtimeSinceStartup;
		}

		textBuilder.Clear();
		foreach(Message message in messages)
		{
			textBuilder.AppendLine(message.message);
		}
		messageField.text = textBuilder.ToString();
	}

	public void Notify()
	{
		playerSpacecraft = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft();
		playerSpacecraftTransform = playerSpacecraft.GetTransform();
		playerSpacecraftRigidbody = playerSpacecraft.GetRigidbody();
		playerSpacecraftInventoryController = playerSpacecraft.GetInventoryController();
	}

	public void UpdateControlHint(Dictionary<string, string[]> keyBindings)
	{
		// Check if controlHint still exists to avoid Exceptions while quitting the Game
		if(controlHint != null)
		{
			StringBuilder hint = new StringBuilder(256);
			foreach(string key in keyBindings.Keys)
			{
				if(keyBindings[key].Length > 0)
				{
					hint.Append(key + " - \t");
				}

				bool first = true;
				foreach(string action in keyBindings[key])
				{
					if(!first)
					{
						hint.Append("\t\t");
					}
					hint.Append(action + "\n");
					first = false;
				}
			}

			controlHint.text = hint.ToString();
		}
	}

	public void UpdateResourceDisplay()
	{
		updateResourceDisplay = true;
	}

	public void UpdateBuildingResourceDisplay()
	{
		updateBuildingResourceDisplay = true;
	}

	public void ActivateConfirmationPanel(string text, UnityEngine.Events.UnityAction confirmationAction)
	{
		confirmationPanelText.text = text;

		confirmationPanelConfirmButton.onClick.RemoveAllListeners();
		confirmationPanelConfirmButton.onClick.AddListener(confirmationAction);
		confirmationPanelConfirmButton.onClick.AddListener(delegate
		{
			DeactivateConfirmationPanel();
		});

		confirmationPanel.SetActive(true);
	}

	public void DeactivateConfirmationPanel()
	{
		confirmationPanel.SetActive(false);
	}

	public void ToggleFlightInfo()
	{
		showFlightInfo = !showFlightInfo;

		if(showFlightInfo)
		{
			showFlightInfoButtonText.text = showFlightInfoButtonText.text.Replace("Show", "Hide");
		}
		else
		{
			showFlightInfoButtonText.text = showFlightInfoButtonText.text.Replace("Hide", "Show");
		}
	}

	public void ToggleColorblindMode()
	{
		colorblindMode = !colorblindMode;

		if(colorblindMode)
		{
			colorblindModeText.text = colorblindModeText.text.Replace("Enable", "Disable");
		}
		else
		{
			colorblindModeText.text = colorblindModeText.text.Replace("Disable", "Enable");
		}
	}

	public void AddMessage(string message, bool warning)
	{
		Message messageRecord = new Message();
		messageRecord.message = message;
		messageRecord.timestamp = Time.realtimeSinceStartup;

		messages.Enqueue(messageRecord);

		if(warning)
		{
			audioController.PlayAudio(warningAudio, null);
		}
	}

	public int GetMessageCount()
	{
		return messages.Count;
	}

	public bool IsColorblindModeActivated()
	{
		return colorblindMode;
	}

	public void SetShowBuildingResourceDisplay(bool showBuildingResourceDisplay)
	{
		updateBuildingResourceDisplay = showBuildingResourceDisplay;
		this.showBuildingResourceDisplay = showBuildingResourceDisplay;
	}

	public void SetBuildingCosts(Module module)
	{
		if(module != null)
		{
			GoodManager.Load[] buildingCostArray = module.GetBuildingCosts();
			this.buildingCosts = new Dictionary<string, uint>(buildingCostArray.Length);
			foreach(GoodManager.Load cost in buildingCostArray)
			{
				this.buildingCosts.Add(cost.goodName, cost.amount);
			}

			this.moduleMass = module.GetMass();
		}
		else
		{
			this.buildingCosts = null;
			this.moduleMass = 0.0f;
		}

		UpdateBuildingResourceDisplay();
	}

	public void SetBuildingCosts(GoodManager.Load[] buildingCostArray, float moduleMass)
	{
		this.buildingCosts = new Dictionary<string, uint>(buildingCostArray.Length);
		foreach(GoodManager.Load cost in buildingCostArray)
		{
			this.buildingCosts.Add(cost.goodName, cost.amount);
		}

		this.moduleMass = moduleMass;

		UpdateBuildingResourceDisplay();
	}

	public void SetDockingExpiryTime(double expiryTime)
	{
		this.expiryTime = expiryTime;
	}

	public void SetFlightControls(bool flightControls)
	{
		this.flightControls = flightControls;
	}
}
