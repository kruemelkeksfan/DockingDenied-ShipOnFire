using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour, IListener
{
	[SerializeField] private float movementSpeed = 1.0f;
	[SerializeField] private float rotationSpeed = 1.0f;
	[SerializeField] private float zoomSpeed = 1.0f;
	[SerializeField] private float maxZHeight = -0.04f;
	[SerializeField] private float maxDistance = 2000.0f;
	[SerializeField] private float planetSafetyRadius = 280.0f;
	private new Transform transform = null;
	private Transform spacecraftTransform = null;
	private Vector3 startPosition = Vector3.zero;
	private Vector3 startRotation = Vector3.zero;
	private Vector3 localPosition;
	private Vector3 localRotation;
	private bool fixedCamera = false;
	private float sqrPlanetSafetyRadius = 0.0f;

	private void Start()
	{
		transform = gameObject.GetComponent<Transform>();

		startPosition = transform.position;
		startRotation = transform.rotation.eulerAngles;

		localPosition = startPosition;
		localRotation = startRotation;

		sqrPlanetSafetyRadius = planetSafetyRadius * planetSafetyRadius;		// Square to avoid Sqrt later

		SpacecraftManager.GetInstance().AddSpacecraftChangeListener(this);
		Notify();
	}

	private void Update()
	{
		if(Input.GetButtonUp("Camera Mode"))
		{
			fixedCamera = !fixedCamera;
		}

		Quaternion rotation = Quaternion.Euler(localRotation);
		if(!fixedCamera)
		{
			rotation = spacecraftTransform.rotation * rotation;
		}
		transform.position = spacecraftTransform.position + (rotation * localPosition);
		transform.rotation = rotation;

		// Check Camera Constraints and if violated, correct the current Position and reverse engineer the new unrotated (local)Position
		// Camera too close/below the Physics Plane?
		if(transform.position.z > maxZHeight)
		{
			transform.position = new Vector3(transform.position.x, transform.position.y, maxZHeight);
			localPosition = Quaternion.Inverse(rotation) * (transform.position - spacecraftTransform.position);
		}
		// Camera too far from the Physics Plane?
		// TODO: Is this necessary? Appears to be covered by next Condition
		else if(localPosition.z < -maxDistance)
		{
			transform.position = new Vector3(transform.position.x, transform.position.y, -maxDistance);
			localPosition = Quaternion.Inverse(rotation) * (transform.position - spacecraftTransform.position);
		}
		// Camera too far away from the Action?
		if(Mathf.Abs(transform.position.x) > maxDistance || Mathf.Abs(transform.position.y) > maxDistance || Mathf.Abs(transform.position.z) > maxDistance)
		{
			transform.position = new Vector3(Mathf.Clamp(transform.position.x, -maxDistance, maxDistance), Mathf.Clamp(transform.position.y, -maxDistance, maxDistance), Mathf.Clamp(transform.position.z, -maxDistance, maxDistance));
			localPosition = Quaternion.Inverse(rotation) * (transform.position - spacecraftTransform.position);
		}
		// Camera inside the Planet?
		if(transform.position.sqrMagnitude <= sqrPlanetSafetyRadius)
		{
			transform.position = transform.position.normalized * planetSafetyRadius;
			localPosition = Quaternion.Inverse(rotation) * (transform.position - spacecraftTransform.position);
		}

		if(Input.GetButton("Rotate Camera"))
		{
			Cursor.visible = false;
			Cursor.lockState = CursorLockMode.Locked;

			Vector3 direction = Vector3.zero;
			if(Input.GetAxis("Vertical") > 0.0f)
			{
				direction += transform.up;
			}
			if(Input.GetAxis("Horizontal") < 0.0f)
			{
				direction -= transform.right;
			}
			if(Input.GetAxis("Vertical") < 0.0f)
			{
				direction -= transform.up;
			}
			if(Input.GetAxis("Horizontal") > 0.0f)
			{
				direction += transform.right;
			}

			float directionMultiplier = (Vector3.Dot(transform.forward, Vector3.forward) * 1.2f - 0.2f) * rotationSpeed;                                               // Turn Camera slower when looking at a flatter Angle
			localPosition += direction * movementSpeed * -localPosition.z;
			localRotation += new Vector3(-Input.GetAxis("Mouse Y") * directionMultiplier, Input.GetAxis("Mouse X") * directionMultiplier, 0.0f);
		}
		else
		{
			Cursor.visible = true;
			Cursor.lockState = CursorLockMode.None;
		}

		if(Input.GetButtonUp("Reset Camera"))
		{
			localPosition = startPosition;
			localRotation = startRotation;
		}

		if(!EventSystem.current.IsPointerOverGameObject())
		{
			localPosition += Vector3.forward * Input.GetAxis("Mouse ScrollWheel") * zoomSpeed * -localPosition.z;
		}
	}

	public void Notify()
	{
		spacecraftTransform = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft().GetTransform();
	}
}
