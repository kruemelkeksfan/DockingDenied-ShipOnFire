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
	private List<Constructor> constructors = null;
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

		constructors = new List<Constructor>();

		spacecraftChangeListeners = new List<IListener>();

		instance = this;
	}

	public void AddConstructor(Constructor constructor)
	{
		constructors.Add(constructor);
	}

	public void RemoveConstructor(Constructor constructor)
	{
		constructors.Remove(constructor);
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

	public List<Constructor> GetConstructorsNearPosition(Vector3 position)
	{		
		// Custom Insertion Sort, because ArrayList.Sort() uses QuickSort which is unsuitable for pre-ordered Lists
		// Algorithm implemented after Pseudo-Code-Example from https://en.wikipedia.org/wiki/Insertion_sort#Algorithm
		for(int i = 0; i < constructors.Count; ++i)
		{
			Constructor currentConstructor = constructors[i];
			float currentConstructorDistance = ((currentConstructor.GetTransform().position) - position).sqrMagnitude;
			int j = i - 1;
			while(j >= 0 && ((constructors[j].GetTransform().position) - position).sqrMagnitude > currentConstructorDistance)
			{
				constructors[j + 1] = constructors[j];
				--j;
			}
			constructors[j + 1] = currentConstructor;
		}

		return constructors;
	}
}
