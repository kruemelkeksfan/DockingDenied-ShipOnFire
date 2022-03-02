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
	[SerializeField] private RectTransform playerHeadPrefab = null;
	[SerializeField] private RectTransform targetHeadPrefab = null;
	[SerializeField] private RectTransform orbitalHeadPrefab = null;
	[SerializeField] private RectTransform navVectorPrefab = null;
	[SerializeField] private Color velocityDifferenceVectorColor = Color.cyan;
	[SerializeField] private Color playerVelocityVectorColor = Color.blue;
	[SerializeField] private Color targetVelocityVectorColor = Color.yellow;
	[SerializeField] private Color orbitalVelocityVectorColor = Color.green;
	[SerializeField] private Color targetNavVectorColor = Color.yellow;
	[SerializeField] private Color planetNavVectorColor = Color.green;
	[SerializeField] private float vectorLengthFactor = 0.04f;
	[SerializeField] private float constantVectorLengthFactor = 20.0f;
	[SerializeField] private float velocityVectorWidth = 1.0f;
	[SerializeField] private float vectorHeadWidthFactor = 2.0f;
	[SerializeField] private float targetNavVectorWidth = 2.0f;
	[SerializeField] private float planetNavVectorWidth = 2.0f;
	[SerializeField] private float playerHeadRotation = 45.0f;
	[SerializeField] private float targetHeadRotation = 0.0f;
	[SerializeField] private float orbitalHeadRotation = 0.0f;
	[SerializeField] private Transform orbitMarkerPrefab = null;
	[SerializeField] private Color targetOrbitMarkerColor = Color.yellow;
	[SerializeField] private Color playerOrbitMarkerColor = Color.green;
	[SerializeField] private Vector3 largeOrbitMarkerScale = Vector3.one;
	[SerializeField] private float orbitMarkerTimeStep = 0.01f;
	[SerializeField] private int largeOrbitMarkerIntervall = 5;
	[SerializeField] private float orbitUpdateIntervall = 0.2f;
	private UpdateController updateController = null;
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
	private RectTransform velocityDifferenceVector = null;
	private RectTransform playerVelocityVector = null;
	private RectTransform targetVelocityVector = null;
	private RectTransform orbitalVelocityVector = null;
	private RectTransform playerHead = null;
	private RectTransform targetHead = null;
	private RectTransform orbitalHead = null;
	private RectTransform targetNavVector = null;
	private RectTransform planetNavVector = null;
	private float scaleFactor = 1.0f;
	private Transform orbitMarkerParent = null;
	private Vector3 smallOrbitMarkerScale = Vector3.one;
	private Transform currentOrbitMarkerTarget = null;
	private List<Transform> playerOrbitMarkers = null;
	private List<Transform> targetOrbitMarkers = null;

	private void Start()
	{
		waitForOrbitUpdateInterval = new WaitForSecondsRealtime(orbitUpdateIntervall);

		updateController = UpdateController.GetInstance();
		gravityWellController = GravityWellController.GetInstance();
		toggleController = ToggleController.GetInstance();

		playerSpacecraft = GetComponent<SpacecraftController>();
		playerSpacecraftTransform = playerSpacecraft.GetTransform();
		playerSpacecraftRigidbody = playerSpacecraft.GetRigidbody();
		camera = Camera.main;
		cameraTransform = camera.GetComponent<Transform>();
		orbitMarkerParent = MenuController.GetInstance().GetOrbitMarkerParent();

		mapMarker = GameObject.Instantiate<RectTransform>(mapMarkerPrefab, uiTransform.position, uiTransform.rotation, uiTransform);
		// Square to avoid Sqrt later on
		minMapMarkerDisplayDistance *= minMapMarkerDisplayDistance;

		// Instantiate in reverse Order to render the more important Vectors on top
		planetNavVector = GameObject.Instantiate<RectTransform>(navVectorPrefab, uiTransform.position, Quaternion.identity, uiTransform);
		planetNavVector.GetComponent<Image>().color = planetNavVectorColor;
		planetNavVector.gameObject.SetActive(toggleController.IsGroupToggled("PlanetNavVector"));
		targetNavVector = GameObject.Instantiate<RectTransform>(navVectorPrefab, uiTransform.position, Quaternion.identity, uiTransform);
		targetNavVector.GetComponent<Image>().color = targetNavVectorColor;
		targetNavVector.gameObject.SetActive(toggleController.IsGroupToggled("TargetNavVector"));

		bool velocityVectorActive = toggleController.IsGroupToggled("VelocityVectors");
		bool orbitalVectorActive = toggleController.IsGroupToggled("OrbitalVelocityVector");
		orbitalVelocityVector = GameObject.Instantiate<RectTransform>(vectorPrefab, uiTransform.position, Quaternion.identity, uiTransform);
		orbitalHead = GameObject.Instantiate<RectTransform>(orbitalHeadPrefab, uiTransform.position, Quaternion.identity, uiTransform);
		orbitalVelocityVector.GetComponent<Image>().color = orbitalVelocityVectorColor;
		orbitalHead.GetComponent<Image>().color = orbitalVelocityVectorColor;
		orbitalVelocityVector.gameObject.SetActive(orbitalVectorActive);
		orbitalHead.gameObject.SetActive(orbitalVectorActive);
		targetVelocityVector = GameObject.Instantiate<RectTransform>(vectorPrefab, uiTransform.position, Quaternion.identity, uiTransform);
		targetHead = GameObject.Instantiate<RectTransform>(targetHeadPrefab, uiTransform.position, Quaternion.identity, uiTransform);
		targetVelocityVector.GetComponent<Image>().color = targetVelocityVectorColor;
		targetHead.GetComponent<Image>().color = targetVelocityVectorColor;
		targetVelocityVector.gameObject.SetActive(velocityVectorActive);
		targetHead.gameObject.SetActive(velocityVectorActive);
		playerVelocityVector = GameObject.Instantiate<RectTransform>(vectorPrefab, uiTransform.position, Quaternion.identity, uiTransform);
		playerHead = GameObject.Instantiate<RectTransform>(playerHeadPrefab, uiTransform.position, Quaternion.identity, uiTransform);
		playerVelocityVector.GetComponent<Image>().color = playerVelocityVectorColor;
		playerHead.GetComponent<Image>().color = playerVelocityVectorColor;
		playerVelocityVector.gameObject.SetActive(velocityVectorActive);
		playerHead.gameObject.SetActive(velocityVectorActive);

		velocityDifferenceVector = GameObject.Instantiate<RectTransform>(vectorPrefab, uiTransform.position, Quaternion.identity, uiTransform);
		velocityDifferenceVector.GetComponent<Image>().color = velocityDifferenceVectorColor;
		velocityDifferenceVector.gameObject.SetActive(toggleController.IsGroupToggled("VelocityDifferenceVector"));

		scaleFactor = Mathf.Abs(1.0f / cameraTransform.position.z);

		playerOrbitMarkers = new List<Transform>();
		targetOrbitMarkers = new List<Transform>();
		Transform orbitMarker = GameObject.Instantiate<Transform>(orbitMarkerPrefab, orbitMarkerParent);
		smallOrbitMarkerScale = orbitMarker.localScale;
		orbitMarker.GetComponent<MeshRenderer>().material.color = playerOrbitMarkerColor;
		orbitMarker.gameObject.SetActive(false);
		playerOrbitMarkers.Add(orbitMarker);

		toggleController.AddToggleObject("VelocityDifferenceVector", velocityDifferenceVector.gameObject);
		toggleController.AddToggleObject("VelocityVectors", playerVelocityVector.gameObject);
		toggleController.AddToggleObject("VelocityVectors", targetVelocityVector.gameObject);
		toggleController.AddToggleObject("OrbitalVelocityVector", orbitalVelocityVector.gameObject);
		toggleController.AddToggleObject("VelocityVectors", playerHead.gameObject);
		toggleController.AddToggleObject("VelocityVectors", targetHead.gameObject);
		toggleController.AddToggleObject("OrbitalVelocityVector", orbitalHead.gameObject);
		toggleController.AddToggleObject("TargetNavVector", targetNavVector.gameObject);
		toggleController.AddToggleObject("PlanetNavVector", planetNavVector.gameObject);

		StartCoroutine(UpdateOrbitDisplay());

		updateController.AddUpdateListener(this);
	}

	private void OnDestroy()
	{
		updateController?.RemoveUpdateListener(this);

		if(toggleController != null)
		{
			toggleController.RemoveToggleObject("VelocityDifferenceVector", velocityDifferenceVector.gameObject);
			toggleController.RemoveToggleObject("VelocityVectors", playerVelocityVector.gameObject);
			toggleController.RemoveToggleObject("VelocityVectors", targetVelocityVector.gameObject);
			toggleController.RemoveToggleObject("OrbitalVelocityVector", orbitalVelocityVector.gameObject);
			toggleController.RemoveToggleObject("VelocityVectors", playerHead.gameObject);
			toggleController.RemoveToggleObject("VelocityVectors", targetHead.gameObject);
			toggleController.RemoveToggleObject("OrbitalVelocityVector", orbitalHead.gameObject);
			toggleController.RemoveToggleObject("TargetNavVector", targetNavVector.gameObject);
			toggleController.RemoveToggleObject("PlanetNavVector", planetNavVector.gameObject);
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

		bool velocityDifferenceActive = toggleController.IsGroupToggled("VelocityDifferenceVector");
		bool velocityActive = toggleController.IsGroupToggled("VelocityVectors");

		Vector2 playerVelocity = Vector2.zero;
		Vector2 targetVelocity = Vector2.zero;
		if(velocityDifferenceActive || velocityActive)
		{
			float time = updateController.GetFixedTime();
			playerVelocity = playerSpacecraft.IsOnRails() ? (Vector2)playerSpacecraft.CalculateVelocity(time) : playerSpacecraftRigidbody.velocity;
			if(targetSpacecraftRigidbody != null)
			{
				targetVelocity = targetSpacecraft.IsOnRails() ? (Vector2)targetSpacecraft.CalculateVelocity(time) : targetSpacecraftRigidbody.velocity;
			}
		}

		if(velocityDifferenceActive)
		{
			if(targetSpacecraftRigidbody != null)
			{
				Vector2 velocityDifference = playerVelocity - targetVelocity;
				UpdateVelocityVector(velocityDifferenceVector, velocityDifference, velocityVectorWidth, scaleFactor, null, 0.0f, true);
			}
			else
			{
				velocityDifferenceVector.sizeDelta = new Vector2(0.0f, 0.0f);
			}
		}

		if(velocityActive)
		{
			UpdateVelocityVector(playerVelocityVector, playerVelocity, velocityVectorWidth, scaleFactor, playerHead, playerHeadRotation);
			if(targetSpacecraftRigidbody != null)
			{
				UpdateVelocityVector(targetVelocityVector, targetVelocity, velocityVectorWidth, scaleFactor, targetHead, targetHeadRotation);
			}
			else
			{
				targetVelocityVector.sizeDelta = new Vector2(0.0f, 0.0f);
				targetHead.sizeDelta = new Vector2(0.0f, 0.0f);
			}
		}
		if(toggleController.IsGroupToggled("OrbitalVelocityVector"))
		{
			Vector2Double optimalOrbitalVelocity = gravityWellController.CalculateOptimalOrbitalVelocity(playerSpacecraftRigidbody);
			UpdateVelocityVector(orbitalVelocityVector, optimalOrbitalVelocity, velocityVectorWidth, scaleFactor, orbitalHead, orbitalHeadRotation);
		}

		if(toggleController.IsGroupToggled("OrbitMarkers"))
		{
			// Awkward Replacement for parenting the Markers, since parenting leads to Rotation Issues
			ShiftOrbitMarkers(playerSpacecraftTransform, playerOrbitMarkers);
			if(currentOrbitMarkerTarget != null && targetOrbitMarkers.Count > 0)
			{
				ShiftOrbitMarkers(currentOrbitMarkerTarget, targetOrbitMarkers);
			}
		}

		if(toggleController.IsGroupToggled("TargetNavVector"))
		{
			if(targetSpacecraftTransform != null)
			{
				UpdateNavVector(targetNavVector, targetSpacecraftTransform.position, targetNavVectorWidth, scaleFactor);
			}
			else
			{
				targetNavVector.sizeDelta = new Vector2(0.0f, 0.0f);
			}
		}
		if(toggleController.IsGroupToggled("PlanetNavVector"))
		{
			UpdateNavVector(planetNavVector, -gravityWellController.GetLocalOrigin(), planetNavVectorWidth, scaleFactor);
		}
	}

	// TODO: Switch to LineRenderer? What was the Problem with LineRenderer?
	private void UpdateVelocityVector(RectTransform vector, Vector2 velocity, float vectorWidth, float scaleFactor,
		RectTransform head = null, float headRotation = 0.0f, bool constantLength = false)
	{
		float velocityMagnitude = velocity.magnitude;
		float rotationAngle = Vector2.SignedAngle(uiTransform.up, velocity);
		Quaternion rotation = Quaternion.Euler(0.0f, 0.0f, rotationAngle);

		if(constantLength)
		{
			vector.sizeDelta = new Vector2(vectorWidth * scaleFactor, velocityMagnitude * constantVectorLengthFactor);
			if(head != null)
			{
				head.anchoredPosition = rotation * new Vector2(0.0f, velocityMagnitude * constantVectorLengthFactor);
				head.localRotation = Quaternion.Euler(0.0f, 0.0f, rotationAngle + headRotation);
				head.sizeDelta = new Vector2(vectorWidth * vectorHeadWidthFactor * scaleFactor, vectorWidth * vectorHeadWidthFactor * scaleFactor);
			}
		}
		else
		{
			vector.sizeDelta = new Vector2(vectorWidth * scaleFactor, velocityMagnitude * vectorLengthFactor * scaleFactor);
			if(head != null)
			{
				head.anchoredPosition = rotation * new Vector2(0.0f, velocityMagnitude * vectorLengthFactor * scaleFactor);
				head.localRotation = Quaternion.Euler(0.0f, 0.0f, rotationAngle + headRotation);
				head.sizeDelta = new Vector2(vectorWidth * vectorHeadWidthFactor * scaleFactor, vectorWidth * vectorHeadWidthFactor * scaleFactor);
			}
		}
		vector.localRotation = rotation;
	}

	// TODO: Switch to LineRenderer? What was the Problem with LineRenderer?
	private void UpdateNavVector(RectTransform vector, Vector2 targetPosition, float vectorWidth, float scaleFactor)
	{
		Vector2 direction = uiTransform.InverseTransformPoint(targetPosition);
		Quaternion rotation = Quaternion.Euler(0.0f, 0.0f, Vector2.SignedAngle(uiTransform.up, targetPosition - (Vector2)playerSpacecraftTransform.position));

		vector.sizeDelta = new Vector2(vectorWidth * scaleFactor, direction.magnitude);
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

			if(toggleController.IsGroupToggled("OrbitMarkers"))
			{
				float startTime = updateController.GetFixedTime();
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

	public void SetTarget(SpacecraftController targetSpacecraft, Transform targetSpacecraftTransform, Rigidbody2D targetSpacecraftRigidbody)
	{
		this.targetSpacecraft = targetSpacecraft;
		this.targetSpacecraftTransform = targetSpacecraftTransform;
		this.targetSpacecraftRigidbody = targetSpacecraftRigidbody;
	}
}
