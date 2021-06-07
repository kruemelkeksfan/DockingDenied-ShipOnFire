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
	[SerializeField] private float minZHeight = -2000.0f;
	private new Transform transform = null;
	private Transform spacecraftTransform = null;
	private Vector3 startPosition = Vector3.zero;
	private Vector3 startRotation = Vector3.zero;
	private Vector3 localPosition;
	private Vector3 localRotation;
	private bool fixedCamera = true;

	private void Start()
	{
		transform = gameObject.GetComponent<Transform>();

		startPosition = transform.position;
		startRotation = transform.rotation.eulerAngles;

		localPosition = startPosition;
		localRotation = startRotation;

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

		// TODO: Collider for Camera
		if(Input.GetButton("Rotate Camera"))
		{
			Cursor.visible = false;
			Cursor.lockState = CursorLockMode.Locked;

			Vector3 direction = new Vector3();
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

		if(!EventSystem.current.IsPointerOverGameObject() && !Mathf.Approximately(Input.GetAxis("Mouse ScrollWheel"), 0.0f))
		{
			localPosition += transform.forward * Input.GetAxis("Mouse ScrollWheel") * zoomSpeed * -localPosition.z;
			if(localPosition.z > maxZHeight)
			{
				localPosition = new Vector3(localPosition.x, localPosition.y, maxZHeight);
			}
			else if(localPosition.z < minZHeight)
			{
				localPosition = new Vector3(localPosition.x, localPosition.y, minZHeight);
			}
		}
	}

	public void Notify()
	{
		spacecraftTransform = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft().GetTransform();
	}
}
