using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// TODO: What if Player has multiple Spacecraft? Activate/deactivate this when switching Spacecraft
public class PlayerSpacecraftUIController : MonoBehaviour, IUpdateListener
{
	private static WaitForSecondsRealtime waitForOrbitUpdateInterval = null;

	[SerializeField] private RectTransform uiTransform = null;
	[SerializeField] private RectTransform mapMarkerPrefab = null;
	[SerializeField] private float minMapMarkerDisplayDistance = 1.0f;
	[SerializeField] private RectTransform vectorPrefab = null;
	[SerializeField] private float vectorLengthFactor = 20.0f;
	[SerializeField] private Color playerVelocityVectorColor = Color.blue;
	[SerializeField] private Color targetVelocityVectorColor = Color.yellow;
	[SerializeField] private Color orbitalVelocityVectorColor = Color.green;
	[SerializeField] private RectTransform navVectorPrefab = null;
	[SerializeField] private Color targetNavVectorColor = Color.yellow;
	[SerializeField] private Color planetNavVectorColor = Color.green;
	[SerializeField] private Transform orbitMarkerPrefab = null;
	[SerializeField] private Color targetOrbitMarkerColor = Color.yellow;
	[SerializeField] private Color playerOrbitMarkerColor = Color.green;
	[SerializeField] private Vector3 largeOrbitMarkerScale = Vector3.one;
	[SerializeField] private float orbitMarkerTimeStep = 0.01f;
	[SerializeField] private int largeOrbitMarkerIntervall = 5;
	[SerializeField] private float orbitUpdateIntervall = 0.2f;
	private ToggleController toggleController = null;
	private GravityWellController gravityWellController = null;
	private SpacecraftController playerSpacecraft = null;
	private Transform playerSpacecraftTransform = null;
	private Rigidbody2D playerSpacecraftRigidbody = null;
	private SpacecraftController targetSpacecraft = null;
	private Transform targetSpacecraftTransform = null;
	private Rigidbody2D targetSpacecraftRigidbody = null;
	private new Camera camera = null;
	private Transform cameraTransform = null;
	private RectTransform mapMarker = null;
	private RectTransform playerVelocityVector = null;
	private RectTransform targetVelocityVector = null;
	private RectTransform orbitalVelocityVector = null;
	private RectTransform targetNavVector = null;
	private RectTransform planetNavVector = null;
	private float scaleFactor = 1.0f;
	private float velocityVectorWidth = 1.0f;
	private float navVectorWidth = 1.0f;
	private float surfaceAltitude = 0.0f;
	private Vector3 flightData = Vector3.zero;
	private Transform orbitMarkerParent = null;
	private Vector3 smallOrbitMarkerScale = Vector3.one;
	private Transform currentOrbitMarkerTarget = null;
	private List<Transform> playerOrbitMarkers = null;
	private List<Transform> targetOrbitMarkers = null;

	private void Start()
	{
		waitForOrbitUpdateInterval = new WaitForSecondsRealtime(orbitUpdateIntervall);

		gravityWellController = GravityWellController.GetInstance();
		toggleController = ToggleController.GetInstance();

		playerSpacecraft = GetComponent<SpacecraftController>();
		playerSpacecraftTransform = playerSpacecraft.GetTransform();
		playerSpacecraftRigidbody = playerSpacecraft.GetRigidbody();
		camera = Camera.main;
		cameraTransform = camera.GetComponent<Transform>();
		orbitMarkerParent = MenuController.GetInstance().GetOrbitMarkerParent();

		mapMarker = GameObject.Instantiate<RectTransform>(mapMarkerPrefab, uiTransform.position, uiTransform.rotation, uiTransform);
		minMapMarkerDisplayDistance *= minMapMarkerDisplayDistance;                                                                         // Square to avoid Sqrt later on

		// Instantiate in reverse Order to render the more important Vectors on top
		planetNavVector = GameObject.Instantiate<RectTransform>(navVectorPrefab, uiTransform.position, Quaternion.identity, uiTransform);
		planetNavVector.GetComponent<Image>().color = planetNavVectorColor;
		planetNavVector.gameObject.SetActive(toggleController.IsGroupToggled(ToggleController.GroupNames.PlanetNavVector));
		targetNavVector = GameObject.Instantiate<RectTransform>(navVectorPrefab, uiTransform.position, Quaternion.identity, uiTransform);
		targetNavVector.GetComponent<Image>().color = targetNavVectorColor;
		targetNavVector.gameObject.SetActive(toggleController.IsGroupToggled(ToggleController.GroupNames.TargetNavVector));

		orbitalVelocityVector = GameObject.Instantiate<RectTransform>(vectorPrefab, uiTransform.position, Quaternion.identity, uiTransform);
		orbitalVelocityVector.GetComponent<Image>().color = orbitalVelocityVectorColor;
		orbitalVelocityVector.gameObject.SetActive(toggleController.IsGroupToggled(ToggleController.GroupNames.OrbitalVelocityVector));
		targetVelocityVector = GameObject.Instantiate<RectTransform>(vectorPrefab, uiTransform.position, Quaternion.identity, uiTransform);
		targetVelocityVector.GetComponent<Image>().color = targetVelocityVectorColor;
		targetVelocityVector.gameObject.SetActive(toggleController.IsGroupToggled(ToggleController.GroupNames.VelocityVectors));
		playerVelocityVector = GameObject.Instantiate<RectTransform>(vectorPrefab, uiTransform.position, Quaternion.identity, uiTransform);
		playerVelocityVector.GetComponent<Image>().color = playerVelocityVectorColor;
		playerVelocityVector.gameObject.SetActive(toggleController.IsGroupToggled(ToggleController.GroupNames.VelocityVectors));

		scaleFactor = Mathf.Abs(1.0f / cameraTransform.position.z);
		velocityVectorWidth = targetVelocityVector.sizeDelta.x;
		navVectorWidth = planetNavVector.sizeDelta.x;
		surfaceAltitude = gravityWellController.GetSurfaceAltitude();

		playerOrbitMarkers = new List<Transform>();
		targetOrbitMarkers = new List<Transform>();
		Transform orbitMarker = GameObject.Instantiate<Transform>(orbitMarkerPrefab, orbitMarkerParent);
		smallOrbitMarkerScale = orbitMarker.localScale;
		orbitMarker.GetComponent<MeshRenderer>().material.color = playerOrbitMarkerColor;
		orbitMarker.gameObject.SetActive(false);
		playerOrbitMarkers.Add(orbitMarker);

		toggleController.AddToggleObject(ToggleController.GroupNames.VelocityVectors, playerVelocityVector.gameObject);
		toggleController.AddToggleObject(ToggleController.GroupNames.VelocityVectors, targetVelocityVector.gameObject);
		toggleController.AddToggleObject(ToggleController.GroupNames.OrbitalVelocityVector, orbitalVelocityVector.gameObject);
		toggleController.AddToggleObject(ToggleController.GroupNames.TargetNavVector, targetNavVector.gameObject);
		toggleController.AddToggleObject(ToggleController.GroupNames.PlanetNavVector, planetNavVector.gameObject);

		StartCoroutine(UpdateOrbitDisplay());

		playerSpacecraft.AddUpdateListener(this);
	}

	private void OnDestroy()
	{
		playerSpacecraft?.RemoveUpdateListener(this);

		if(toggleController != null)
		{
			toggleController.RemoveToggleObject(ToggleController.GroupNames.VelocityVectors, targetVelocityVector.gameObject);
			toggleController.RemoveToggleObject(ToggleController.GroupNames.VelocityVectors, orbitalVelocityVector.gameObject);
			toggleController.RemoveToggleObject(ToggleController.GroupNames.TargetNavVector, targetNavVector.gameObject);
			toggleController.RemoveToggleObject(ToggleController.GroupNames.PlanetNavVector, planetNavVector.gameObject);
		}

		if(targetVelocityVector != null)
		{
			GameObject.Destroy(targetVelocityVector.gameObject);
		}
		if(orbitalVelocityVector != null)
		{
			GameObject.Destroy(orbitalVelocityVector.gameObject);
		}
		if(targetNavVector != null)
		{
			GameObject.Destroy(targetNavVector.gameObject);
		}
		if(planetNavVector != null)
		{
			GameObject.Destroy(planetNavVector.gameObject);
		}
		if(mapMarker != null)
		{
			GameObject.Destroy(mapMarker.gameObject);
		}
	}

	public void UpdateNotify()
	{
		float scaleFactor = (playerSpacecraftTransform.position - cameraTransform.position).magnitude * this.scaleFactor;

		if((playerSpacecraftTransform.position - cameraTransform.position).sqrMagnitude >= minMapMarkerDisplayDistance)
		{
			mapMarker.gameObject.SetActive(true);
			mapMarker.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
		}
		else
		{
			mapMarker.gameObject.SetActive(false);
		}

		flightData = new Vector3((float)gravityWellController.LocalToGlobalPosition(playerSpacecraftTransform.position).Magnitude() - surfaceAltitude,
			playerSpacecraftRigidbody.velocity.magnitude,
			(targetSpacecraftRigidbody != null ? (playerSpacecraftRigidbody.velocity - targetSpacecraftRigidbody.velocity).magnitude : -1.0f));

		if(toggleController.IsGroupToggled(ToggleController.GroupNames.VelocityVectors))
		{
			UpdateVelocityVector(playerVelocityVector, playerSpacecraftRigidbody.velocity, flightData.y, scaleFactor);
			if(targetSpacecraftRigidbody != null)
			{
				UpdateVelocityVector(targetVelocityVector, targetSpacecraftRigidbody.velocity, targetSpacecraftRigidbody.velocity.magnitude, scaleFactor);
			}
			else
			{
				targetVelocityVector.sizeDelta = new Vector2(0.0f, 0.0f);
			}
		}
		if(toggleController.IsGroupToggled(ToggleController.GroupNames.OrbitalVelocityVector))
		{
			Vector2Double optimalOrbitalVelocity = gravityWellController.CalculateOptimalOrbitalVelocity(playerSpacecraftRigidbody);
			UpdateVelocityVector(orbitalVelocityVector, optimalOrbitalVelocity, (float)optimalOrbitalVelocity.Magnitude(), scaleFactor);
		}

		if(toggleController.IsGroupToggled(ToggleController.GroupNames.OrbitMarkers))
		{
			// Awkward Replacement for parenting the Markers, since parenting leads to Rotation Issues
			ShiftOrbitMarkers(playerSpacecraftTransform, playerOrbitMarkers);
			if(currentOrbitMarkerTarget != null && targetOrbitMarkers.Count > 0)
			{
				ShiftOrbitMarkers(currentOrbitMarkerTarget, targetOrbitMarkers);
			}
		}

		if(toggleController.IsGroupToggled(ToggleController.GroupNames.TargetNavVector))
		{
			if(targetSpacecraftTransform != null)
			{
				UpdateNavVector(targetNavVector, targetSpacecraftTransform.position, scaleFactor);
			}
			else
			{
				targetNavVector.sizeDelta = new Vector2(0.0f, 0.0f);
			}
		}
		if(toggleController.IsGroupToggled(ToggleController.GroupNames.PlanetNavVector))
		{
			UpdateNavVector(planetNavVector, -gravityWellController.GetLocalOrigin(), scaleFactor);
		}
	}

	// TODO: Switch to LineRenderer? What was the Problem with LineRenderer?
	private void UpdateVelocityVector(RectTransform vector, Vector2 velocity, float velocityMagnitude, float scaleFactor)
	{
		Quaternion rotation = Quaternion.Euler(0.0f, 0.0f, Vector2.SignedAngle(uiTransform.up, velocity));

		vector.sizeDelta = new Vector2(velocityVectorWidth * scaleFactor, velocityMagnitude * vectorLengthFactor * scaleFactor);
		vector.localRotation = rotation;
	}

	// TODO: Switch to LineRenderer? What was the Problem with LineRenderer?
	private void UpdateNavVector(RectTransform vector, Vector2 targetPosition, float scaleFactor)
	{
		Vector2 direction = uiTransform.InverseTransformPoint(targetPosition);
		Quaternion rotation = Quaternion.Euler(0.0f, 0.0f, Vector2.SignedAngle(uiTransform.up, targetPosition - (Vector2)playerSpacecraftTransform.position));

		vector.sizeDelta = new Vector2(navVectorWidth * scaleFactor, direction.magnitude);
		vector.localRotation = rotation;
	}

	private IEnumerator UpdateOrbitDisplay()
	{
		while(true)
		{
			yield return waitForOrbitUpdateInterval;

			// Set targetOrbitMarkers inactive if target was reset
			if(currentOrbitMarkerTarget != null && targetSpacecraftTransform == null)
			{
				foreach(Transform orbitMarker in targetOrbitMarkers)
				{
					orbitMarker.gameObject.SetActive(false);
				}
			}

			currentOrbitMarkerTarget = targetSpacecraftTransform;

			if(toggleController.IsGroupToggled(ToggleController.GroupNames.OrbitMarkers))
			{
				float startTime = Time.time;
				float scaleFactor = (playerSpacecraftTransform.position - cameraTransform.position).magnitude * this.scaleFactor;
				float orbitMarkerTimeStep = this.orbitMarkerTimeStep * scaleFactor;

				UpdateOrbitMarkers(startTime, scaleFactor, orbitMarkerTimeStep,
					playerSpacecraft, playerSpacecraftTransform, playerSpacecraftRigidbody, playerOrbitMarkers, playerOrbitMarkerColor);
				if(targetSpacecraft != null)
				{
					UpdateOrbitMarkers(startTime, scaleFactor, orbitMarkerTimeStep,
						targetSpacecraft, targetSpacecraftTransform, targetSpacecraftRigidbody, targetOrbitMarkers, targetOrbitMarkerColor);
				}
			}
		}
	}

	private void ShiftOrbitMarkers(Transform parentTransform, List<Transform> orbitMarkers)
	{
		Vector3 orbitMarkerShift = parentTransform.position - orbitMarkers[0].position;
		for(int i = 0; i < orbitMarkers.Count; ++i)
		{
			if(i > 0 && !orbitMarkers[i].gameObject.activeSelf)
			{
				break;
			}
			orbitMarkers[i].position += orbitMarkerShift;
		}
	}

	private void UpdateOrbitMarkers(float startTime, float scaleFactor, float orbitMarkerTimeStep,
		SpacecraftController orbiter, Transform orbiterTransform, Rigidbody2D orbiterRigidbody,
		List<Transform> orbitMarkers, Color orbitMarkerColor)
	{
		double orbitalPeriod = orbiter.GetOrbitalPeriod();
		// Check if Orbiter is onRails, because CalculateOrbitalElements() is unnecessary in this Case and rigidbody.velocity would be invalid
		if(!orbiter.IsOnRails() &&
			!orbiter.CalculateOrbitalElements(gravityWellController.LocalToGlobalPosition(orbiterTransform.position), orbiterRigidbody.velocity, startTime))
		{
			foreach(Transform orbitMarker in orbitMarkers)
			{
				orbitMarker.gameObject.SetActive(false);
			}

			return;
		}
		int i = 0;
		while(i * orbitMarkerTimeStep < orbitalPeriod)
		{
			Vector3 position = gravityWellController.GlobalToLocalPosition(orbiter.CalculateOnRailPosition(startTime + (i * orbitMarkerTimeStep)));

			if(orbitMarkers.Count > i)
			{
				orbitMarkers[i].gameObject.SetActive(true);
				orbitMarkers[i].position = position;
			}
			else
			{
				orbitMarkers.Add(GameObject.Instantiate<Transform>(orbitMarkerPrefab, position, Quaternion.identity, orbitMarkerParent));
				orbitMarkers[i].GetComponent<MeshRenderer>().material.color = orbitMarkerColor;
			}

			if(i % largeOrbitMarkerIntervall == 0)
			{
				orbitMarkers[i].localScale = largeOrbitMarkerScale * scaleFactor;
			}
			else
			{
				orbitMarkers[i].localScale = smallOrbitMarkerScale * scaleFactor;
			}

			++i;

			// Break if position is not visible
			// Break after Marker Creation to ensure that orbitMarkers.Count > i
			Vector3 viewportPosition = camera.WorldToViewportPoint(position);
			if(viewportPosition.x < 0.0f || viewportPosition.x > 1.0f
				|| viewportPosition.y < 0.0f || viewportPosition.y > 1.0f)
			{
				break;
			}
		}
		while(i < orbitMarkers.Count)
		{
			orbitMarkers[i].gameObject.SetActive(false);
			++i;
		}

		// First Orbit Marker is just a Reference Point for the Shift in Update(), so it shouldn't be displayed
		if(orbitMarkers.Count > 0)
		{
			orbitMarkers[0].gameObject.SetActive(false);
		}
	}

	public Vector3 GetFlightData()
	{
		return flightData;
	}

	public void SetTarget(SpacecraftController targetSpacecraft, Transform targetSpacecraftTransform, Rigidbody2D targetSpacecraftRigidbody)
	{
		this.targetSpacecraft = targetSpacecraft;
		this.targetSpacecraftTransform = targetSpacecraftTransform;
		this.targetSpacecraftRigidbody = targetSpacecraftRigidbody;
	}
}
