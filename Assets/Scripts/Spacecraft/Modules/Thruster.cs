using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Thruster : HotkeyModule
{
	[SerializeField] private string fuelName = "Xenon";
	private Transform spacecraftTransform = null;
	private new Rigidbody2D rigidbody = null;
	private GravityWellController gravityWellController = null;
	private Engine engine = null;
	private Vector2 thrustDirection = Vector2.zero;
	private float throttle = 0.0f;
	private float fuelSupply = 0.0f;
	private ParticleSystem thrustParticles = null;
	private ParticleSystem.MainModule thrustParticlesMain = new ParticleSystem.MainModule();
	private Vector3 initialParticleSize = Vector3.zero;
	private float power = 1.0f;
	private Slider powerSlider = null;
	private InputField powerInputField = null;
	private Button toggleButton = null;
	private Text toggleButtonText = null;
	private bool active = true;
	private bool needRefuel = true;

	public override void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		base.Build(position, false, true);

		rigidbody = gameObject.GetComponentInParent<Rigidbody2D>();
		spacecraftTransform = spacecraft.transform;
		thrustDirection = transform.localRotation * Vector2.up;
		spacecraft.AddThruster(this);

		gravityWellController = GravityWellController.GetInstance();

		thrustParticles = gameObject.GetComponentInChildren<ParticleSystem>();
		thrustParticlesMain = thrustParticles.main;
		initialParticleSize = new Vector3(thrustParticlesMain.startSizeXMultiplier, thrustParticlesMain.startSizeYMultiplier, thrustParticlesMain.startSizeZMultiplier);

		engine = new Engine();
		AddComponentSlot(GoodManager.ComponentType.IonEngine, engine);

		if(moduleMenu != null)
		{
			// Status
			AddStatusField("Internal Fuel", (fuelSupply.ToString("F4") + " m3"));
			AddStatusField("Activated", active.ToString());

			// Settings
			powerSlider = settingPanel.GetComponentInChildren<Slider>();
			powerInputField = settingPanel.GetComponentInChildren<InputField>();

			powerSlider.onValueChanged.AddListener(delegate
			{
				PowerSliderChanged();
			});
			powerInputField.onEndEdit.AddListener(delegate
			{
				PowerInputFieldChanged();
			});

			int power = 100;
			powerSlider.value = power;
			powerInputField.text = power.ToString();

			toggleButton = settingPanel.GetComponentInChildren<Button>();
			toggleButtonText = toggleButton.GetComponentInChildren<Text>();
			toggleButton.onClick.AddListener(delegate
			{
				HotkeyDown();
			});
		}
	}

	public override void Deconstruct()
	{
		spacecraft.RemoveThruster(this);

		base.Deconstruct();
	}

	public override void FixedUpdateNotify()
	{
		// Don't apply Thrust during a Frame in which the Origin shifted,
		// because the Physics freak out when moving transform.position while Forces are being applied
		// TODO: Check for Origin Shift in Spacecraft (instead of here) to avoid unnecessary Method Calls
		if(constructed && throttle > MathUtil.EPSILON && power > MathUtil.EPSILON && active && !gravityWellController.IsOriginShifted())
		{
			float deltaTime = timeController.GetFixedDeltaTime();
			if((engine.GetSecondaryFuelConsumption() * throttle * power * deltaTime) > fuelSupply)
			{
				if(inventoryController.Withdraw(fuelName, 1, true))
				{
					fuelSupply += 1.0f;
				}
				else
				{
					needRefuel = true;

					return;
				}
			}

			float finalThrottle = throttle * power * inventoryController.DischargeEnergyPartially((float)(engine.GetPrimaryFuelConsumption() * throttle * power * (deltaTime / 36000.0)));

			fuelSupply -= engine.GetSecondaryFuelConsumption() * finalThrottle * deltaTime;
			if(fuelSupply < 0.0f)
			{
				Debug.LogWarning("Negative fuelSupply detected in " + this + " from " + gameObject + "!");
			}

			thrustParticlesMain.startSizeXMultiplier = initialParticleSize.x * finalThrottle;
			thrustParticlesMain.startSizeYMultiplier = initialParticleSize.y * finalThrottle;
			thrustParticlesMain.startSizeZMultiplier = initialParticleSize.z * finalThrottle;

			rigidbody.AddForceAtPosition(spacecraftTransform.rotation * thrustDirection * engine.GetThrust() * finalThrottle * deltaTime,
				transform.position, ForceMode2D.Impulse);
		}

		if(moduleMenu != null)
		{
			UpdateStatusField("Internal Fuel", (fuelSupply.ToString("F4") + " m3"));
			UpdateStatusField("Activated", active.ToString());
		}
	}

	public override bool InstallComponent(string componentName)
	{
		bool result = base.InstallComponent(componentName);

		spacecraft.UpdateCenterOfThrust();

		return result;
	}

	public override bool RemoveComponent(GoodManager.ComponentType componentType)
	{
		bool result = base.RemoveComponent(componentType);

		spacecraft.UpdateCenterOfThrust();

		return result;
	}

	public override void HotkeyDown()
	{
		active = !active;

		toggleButtonText.text = active ? "Deactivate" : "Activate";
	}

	public Vector2 GetThrustDirection()
	{
		return thrustDirection;
	}

	public float GetThrust()
	{
		return engine.GetThrust() * power;
	}

	public bool SetThrottle(float throttle)
	{
		if(constructed && throttle > MathUtil.EPSILON && power > MathUtil.EPSILON && active)
		{
			if((needRefuel || fuelSupply <= MathUtil.EPSILON) && inventoryController.Withdraw(fuelName, 1, true))
			{
				fuelSupply += 1.0f;
				needRefuel = false;
			}

			if(!needRefuel && fuelSupply > MathUtil.EPSILON && inventoryController.GetEnergyCharge() > MathUtil.EPSILON)
			{
				if(this.throttle <= 0.0f)
				{
					thrustParticlesMain.startSizeXMultiplier = 0.0f;
					thrustParticlesMain.startSizeYMultiplier = 0.0f;
					thrustParticlesMain.startSizeZMultiplier = 0.0f;

					thrustParticles.Play();
				}
				this.throttle = Mathf.Clamp(throttle, 0.0f, 1.0f);

				return true;
			}

			if((needRefuel || fuelSupply <= MathUtil.EPSILON) && infoController.GetMessageCount() < 1)
			{
				infoController.AddMessage("Out of " + fuelName + " for Propulsion!", true);
			}
		}

		thrustParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
		this.throttle = 0.0f;
		return false;
	}

	public void PowerSliderChanged()
	{
		float power = powerSlider.value;
		powerInputField.text = power.ToString();

		this.power = power / 100.0f;

		spacecraft.UpdateCenterOfThrust();
	}

	public void PowerInputFieldChanged()
	{
		float power = Mathf.Clamp(int.Parse(powerInputField.text), 0, 100);
		powerSlider.value = power;
		powerInputField.text = power.ToString();

		this.power = power / 100.0f;

		spacecraft.UpdateCenterOfThrust();
	}
}
