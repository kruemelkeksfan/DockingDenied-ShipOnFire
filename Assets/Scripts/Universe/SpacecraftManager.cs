using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpacecraftManager : MonoBehaviour
{
	private struct PlayerSpacecraftRecord
	{
		public Spacecraft mainSpacecraft;
		public List<Spacecraft> spacecraft;

		public PlayerSpacecraftRecord(Spacecraft mainSpacecraft)
		{
			this.mainSpacecraft = mainSpacecraft;
			this.spacecraft = new List<Spacecraft>();
			this.spacecraft.Add(mainSpacecraft);
		}
	}

	public static SpacecraftManager instance = null;

	[SerializeField] private Spacecraft localPlayerSpacecraft = null;		// Temporary Solution until multiple Ships and Multiplayer is implemented
	private Dictionary<string, PlayerSpacecraftRecord> playerSpacecraft = null;
	// private List<SpaceStationController> aiSpacecraft = null;    // Enable if necessary
	// private List<SpaceStationController> spaceStations = null;   // Enable if necessary
	private List<IListener> spacecraftChangeListeners = null;		// TODO: call Notify() on these when Spacecraft change

	public static SpacecraftManager GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		playerSpacecraft = new Dictionary<string, PlayerSpacecraftRecord>();
		playerSpacecraft.Add("LocalPlayer", new PlayerSpacecraftRecord(localPlayerSpacecraft));

		spacecraftChangeListeners = new List<IListener>();

		instance = this;
	}

	public void AddSpacecraftChangeListener(IListener listener)
	{
		spacecraftChangeListeners.Add(listener);
	}

	public void RemoveSpacecraftChangeListener(IListener listener)
	{
		spacecraftChangeListeners.Remove(listener);
	}

	public Spacecraft GetLocalPlayerMainSpacecraft()
	{
		return playerSpacecraft["LocalPlayer"].mainSpacecraft;
	}
}
