using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSpacecraftUIController : MonoBehaviour, IUpdateListener
{
	[SerializeField] private RectTransform uiTransform = null;
	[SerializeField] private RectTransform mapMarkerPrefab = null;
	[SerializeField] private float minMapMarkerDisplayDistance = 1.0f;
	[SerializeField] private RectTransform vectorPrefab = null;
	[SerializeField] private float vectorLengthFactor = 20.0f;
	[SerializeField] private Color velocityVectorColor = Color.blue;
	[SerializeField] private Color orbitalVectorColor = Color.green;
	[SerializeField] private RectTransform navVectorPrefab = null;
	[SerializeField] private Color targetNavVectorColor = Color.red;
	[SerializeField] private Color planetNavVectorColor = Color.green;
	private ToggleController toggleController = null;
	private GravityWellController gravityWellController = null;
	private Spacecraft spacecraft = null;
	private new Transform transform = null;
	private new Rigidbody2D rigidbody = null;
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
	private Rigidbody2D target = null;
	private float surfaceAltitude = 0.0f;
	private Vector3 flightData = Vector3.zero;

	private void Start()
	{
		gravityWellController = GravityWellController.GetInstance();

		spacecraft = GetComponent<Spacecraft>();
		transform = spacecraft.GetTransform();
		rigidbody = GetComponent<Rigidbody2D>();
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
		surfaceAltitude = GravityWellController.GetInstance().GetSurfaceAltitude();

		toggleController = ToggleController.GetInstance();
		toggleController.AddToggleObject("VelocityVectors", velocityVector.gameObject);
		toggleController.AddToggleObject("VelocityVectors", orbitalVector.gameObject);
		toggleController.AddToggleObject("NavVectors", targetNavVector.gameObject);
		toggleController.AddToggleObject("NavVectors", planetNavVector.gameObject);

		spacecraft.AddUpdateListener(this);
	}

	private void OnDestroy()
	{
		spacecraft?.RemoveUpdateListener(this);

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
		float scaleFactor = (transform.position - cameraTransform.position).magnitude * this.scaleFactor;

		if((transform.position - cameraTransform.position).sqrMagnitude >= minMapMarkerDisplayDistance)
		{
			mapMarker.gameObject.SetActive(true);
			mapMarker.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
		}
		else
		{
			mapMarker.gameObject.SetActive(false);
		}

		Vector2 orbitalVelocity = rigidbody.velocity - gravityWellController.CalculateOptimalOrbitalVelocity(rigidbody);
		flightData = new Vector3(rigidbody.position.magnitude - surfaceAltitude, (target != null ? (rigidbody.velocity - target.velocity).magnitude : 0.0f), orbitalVelocity.magnitude);
		UpdateVelocityVector(velocityVector, (target != null ? (rigidbody.velocity - target.velocity) : Vector2.zero), flightData.y, scaleFactor);
		UpdateVelocityVector(orbitalVector, orbitalVelocity, flightData.z, scaleFactor);
		UpdateNavVector(targetNavVector, (target != null ? target.position : Vector2.zero), scaleFactor);
		UpdateNavVector(planetNavVector, Vector2.zero, scaleFactor);
	}

	private void UpdateVelocityVector(RectTransform vector, Vector2 velocity, float velocityMagnitude, float scaleFactor)
	{
		if(vector.gameObject.activeSelf && velocityMagnitude > 0.0f)
		{
			// Workaround for Rotation sometimes pointing in the wroing Direction for some Frames, probably a Unity Bug with Rotations near 180°
			Vector2 position = uiTransform.InverseTransformDirection(velocity) * (vectorLengthFactor * 0.5f);
			Quaternion rotation = Quaternion.FromToRotation(uiTransform.up, velocity);
			if(Mathf.Abs(Vector2.Dot(rotation * Vector2.right, position - (Vector2)uiTransform.localPosition)) < 0.0002f)
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

	private void UpdateNavVector(RectTransform vector, Vector2 targetPosition, float scaleFactor)
	{
		if(vector.gameObject.activeSelf && targetPosition != rigidbody.position)
		{
			// Workaround for Rotation sometimes pointing in the wroing Direction for some Frames, probably a Unity Bug with Rotations near 180°
			Vector2 direction = uiTransform.InverseTransformPoint(targetPosition);
			Quaternion rotation = Quaternion.FromToRotation(uiTransform.up, targetPosition - rigidbody.position);
			if(Mathf.Abs(Vector2.Dot(rotation * Vector2.right, (direction * 0.5f) - (Vector2)uiTransform.localPosition)) < 0.0002f)
			{                                                   // Vector from local Origin to Target Position in local Space
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

	public Vector3 GetFlightData()
	{
		return flightData;
	}

	public void SetTarget(Rigidbody2D target)
	{
		this.target = target;
	}
}
