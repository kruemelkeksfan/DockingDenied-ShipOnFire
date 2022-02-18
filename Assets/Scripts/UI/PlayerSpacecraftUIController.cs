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
	[SerializeField] private Color velocityVectorColor = Color.blue;
	[SerializeField] private Color orbitalVectorColor = Color.green;
	[SerializeField] private RectTransform navVectorPrefab = null;
	[SerializeField] private Color targetNavVectorColor = Color.blue;
	[SerializeField] private Color planetNavVectorColor = Color.green;
	[SerializeField] private Transform orbitMarkerPrefab = null;
	[SerializeField] private Color targetOrbitMarkerColor = Color.blue;
	[SerializeField] private Color playerOrbitMarkerColor = Color.green;
	[SerializeField] private Vector3 largeOrbitMarkerScale = Vector3.one;
	[SerializeField] private float orbitMarkerTimeStep = 0.01f;
	[SerializeField] private int largeOrbitMarkerIntervall = 5;
	[SerializeField] private float orbitUpdateIntervall = 2.0f;
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
	private RectTransform velocityVector = null;
	private RectTransform orbitalVector = null;
	private RectTransform targetNavVector = null;
	private RectTransform planetNavVector = null;
	private float scaleFactor = 1.0f;
	private float velocityVectorWidth = 1.0f;
	private float navVectorWidth = 1.0f;
	private float surfaceAltitude = 0.0f;
	private Vector3 flightData = Vector3.zero;
	private Vector3 smallOrbitMarkerScale = Vector3.one;
	private Transform currentOrbitMarkerTarget = null;
	private List<Transform> playerOrbitMarkers = null;
	private List<Transform> targetOrbitMarkers = null;

	private void Start()
	{
		waitForOrbitUpdateInterval = new WaitForSecondsRealtime(orbitUpdateIntervall);

		gravityWellController = GravityWellController.GetInstance();

		playerSpacecraft = GetComponent<SpacecraftController>();
		playerSpacecraftTransform = playerSpacecraft.GetTransform();
		playerSpacecraftRigidbody = playerSpacecraft.GetRigidbody();
		camera = Camera.main;
		cameraTransform = camera.GetComponent<Transform>();

		mapMarker = GameObject.Instantiate<RectTransform>(mapMarkerPrefab, uiTransform.position, uiTransform.rotation, uiTransform);
		minMapMarkerDisplayDistance *= minMapMarkerDisplayDistance;                                                                         // Square to avoid Sqrt later on

		// Instantiate in reverse Order to render the more important Vectors on top
		orbitalVector = GameObject.Instantiate<RectTransform>(vectorPrefab, uiTransform.position, uiTransform.rotation, uiTransform);
		orbitalVector.GetComponent<Image>().color = orbitalVectorColor;
		velocityVector = GameObject.Instantiate<RectTransform>(vectorPrefab, uiTransform.position, uiTransform.rotation, uiTransform);
		velocityVector.GetComponent<Image>().color = velocityVectorColor;

		targetNavVector = GameObject.Instantiate<RectTransform>(navVectorPrefab, uiTransform.position, uiTransform.rotation, uiTransform);
		targetNavVector.GetComponent<Image>().color = targetNavVectorColor;
		planetNavVector = GameObject.Instantiate<RectTransform>(navVectorPrefab, uiTransform.position, uiTransform.rotation, uiTransform);
		planetNavVector.GetComponent<Image>().color = planetNavVectorColor;

		scaleFactor = Mathf.Abs(1.0f / cameraTransform.position.z);
		velocityVectorWidth = velocityVector.sizeDelta.x;
		navVectorWidth = planetNavVector.sizeDelta.x;
		surfaceAltitude = gravityWellController.GetSurfaceAltitude();

		playerOrbitMarkers = new List<Transform>();
		targetOrbitMarkers = new List<Transform>();
		Transform orbitMarker = GameObject.Instantiate<Transform>(orbitMarkerPrefab);
		smallOrbitMarkerScale = orbitMarker.localScale;
		orbitMarker.GetComponent<MeshRenderer>().material.color = playerOrbitMarkerColor;
		orbitMarker.gameObject.SetActive(false);
		playerOrbitMarkers.Add(orbitMarker);

		toggleController = ToggleController.GetInstance();
		toggleController.AddToggleObject("VelocityVectors", velocityVector.gameObject);
		toggleController.AddToggleObject("VelocityVectors", orbitalVector.gameObject);
		toggleController.AddToggleObject("NavVectors", targetNavVector.gameObject);
		toggleController.AddToggleObject("NavVectors", planetNavVector.gameObject);

		StartCoroutine(UpdateOrbitDisplay());

		playerSpacecraft.AddUpdateListener(this);
	}

	private void OnDestroy()
	{
		playerSpacecraft?.RemoveUpdateListener(this);

		if(toggleController != null)
		{
			toggleController.RemoveToggleObject("VelocityVectors", velocityVector.gameObject);
			toggleController.RemoveToggleObject("VelocityVectors", orbitalVector.gameObject);
			toggleController.RemoveToggleObject("NavVectors", targetNavVector.gameObject);
			toggleController.RemoveToggleObject("NavVectors", planetNavVector.gameObject);
		}

		if(velocityVector != null)
		{
			GameObject.Destroy(velocityVector.gameObject);
		}
		if(orbitalVector != null)
		{
			GameObject.Destroy(orbitalVector.gameObject);
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

		// Awkward Replacement for parenting the Markers, since parenting leads to Rotation Issues
		ShiftOrbitMarkers(playerSpacecraftTransform, playerOrbitMarkers);
		if(currentOrbitMarkerTarget != null && targetOrbitMarkers.Count > 0)
		{
			ShiftOrbitMarkers(currentOrbitMarkerTarget, targetOrbitMarkers);
		}

		Vector2 orbitalVelocity = playerSpacecraftRigidbody.velocity - ((Vector2)gravityWellController.CalculateOptimalOrbitalVelocity(playerSpacecraftRigidbody));
		flightData = new Vector3((float)gravityWellController.LocalToGlobalPosition(playerSpacecraftTransform.position).Magnitude() - surfaceAltitude,
			(targetSpacecraftRigidbody != null ? (playerSpacecraftRigidbody.velocity - targetSpacecraftRigidbody.velocity).magnitude : -1.0f), orbitalVelocity.magnitude);
		UpdateVelocityVector(velocityVector, (targetSpacecraftRigidbody != null ? (playerSpacecraftRigidbody.velocity - targetSpacecraftRigidbody.velocity) : Vector2.zero), flightData.y, scaleFactor);
		UpdateVelocityVector(orbitalVector, orbitalVelocity, flightData.z, scaleFactor);
		UpdateNavVector(targetNavVector, (targetSpacecraftTransform != null ? targetSpacecraftTransform.position : playerSpacecraftTransform.position), scaleFactor);
		UpdateNavVector(planetNavVector, -gravityWellController.GetLocalOrigin(), scaleFactor);
	}

	// TODO: Switch to LineRenderer? What was the Problem with LineRenderer?
	private void UpdateVelocityVector(RectTransform vector, Vector2 velocity, float velocityMagnitude, float scaleFactor)
	{
		if(vector.gameObject.activeSelf && velocityMagnitude > 0.0f)
		{
			Vector2 position = uiTransform.InverseTransformDirection(velocity) * (vectorLengthFactor * 0.5f);
			Quaternion rotation = Quaternion.FromToRotation(uiTransform.up, velocity);
			// Workaround for Rotation sometimes pointing in the wrong Direction for some Frames, probably a Unity Bug with Rotations near 180°
			if(Mathf.Abs(Vector2.Dot(rotation * Vector2.right, position - (Vector2)uiTransform.localPosition)) < 0.2f)
			{
				vector.sizeDelta = new Vector2(velocityVectorWidth * scaleFactor, velocityMagnitude * vectorLengthFactor);
				vector.anchoredPosition = position;
				vector.localRotation = rotation;
			}
		}
		else
		{
			vector.sizeDelta = new Vector2(0.0f, 0.0f);
		}
	}

	// TODO: Switch to LineRenderer? What was the Problem with LineRenderer?
	private void UpdateNavVector(RectTransform vector, Vector2 targetPosition, float scaleFactor)
	{
		if(vector.gameObject.activeSelf && targetPosition != (Vector2)playerSpacecraftTransform.position)
		{
			Vector2 direction = uiTransform.InverseTransformPoint(targetPosition);
			Quaternion rotation = Quaternion.FromToRotation(uiTransform.up, targetPosition - (Vector2)playerSpacecraftTransform.position);
			// Workaround for Rotation sometimes pointing in the wrong Direction for some Frames, probably a Unity Bug with Rotations near 180°
			if(Mathf.Abs(Vector2.Dot(rotation * Vector2.right, (direction * 0.5f) - (Vector2)uiTransform.localPosition)) < 0.2f)
			{
				vector.sizeDelta = new Vector2(navVectorWidth * scaleFactor, direction.magnitude);
				vector.anchoredPosition = direction * 0.5f;
				vector.localRotation = rotation;
			}
		}
		else
		{
			vector.sizeDelta = new Vector2(0.0f, 0.0f);
		}
	}

	private IEnumerator UpdateOrbitDisplay()
	{
		while(true)
		{
			yield return waitForOrbitUpdateInterval;

			currentOrbitMarkerTarget = targetSpacecraftTransform;

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
		if(!orbiter.CalculateOrbitalElements(gravityWellController.LocalToGlobalPosition(orbiterTransform.position), orbiterRigidbody.velocity, startTime))
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
				orbitMarkers.Add(GameObject.Instantiate<Transform>(orbitMarkerPrefab, position, Quaternion.identity));
				orbitMarkers[i].GetComponent<MeshRenderer>().material.color = orbitMarkerColor;
			}

			// Break if position is not visible
			// Break after Marker Creation to ensure that orbitMarkers.Count > i
			Vector3 viewportPosition = camera.WorldToViewportPoint(position);
			if(viewportPosition.x < 0.0f || viewportPosition.x > 1.0f
				|| viewportPosition.y < 0.0f || viewportPosition.y > 1.0f)
			{
				break;
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
		currentOrbitMarkerTarget = null;
	}
}
