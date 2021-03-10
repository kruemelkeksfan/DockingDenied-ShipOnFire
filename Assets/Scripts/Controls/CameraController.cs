using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
	[SerializeField] private float movementSpeed = 1.0f;
	[SerializeField] private float rotationSpeed = 1.0f;
	[SerializeField] private float zoomSpeed = 1.0f;
	[SerializeField] private float maxZHeight = -0.04f;
	private new Transform transform = null;
	private Transform spacecraftTransform = null;
	private Vector3 startPosition = Vector3.zero;
	private Quaternion startRotation = Quaternion.identity;

	private void Start()
	{
		transform = gameObject.GetComponent<Transform>();
		spacecraftTransform = gameObject.GetComponentInParent<Spacecraft>().transform;

		startPosition = transform.localPosition;
		startRotation = transform.localRotation;
	}

	private void Update()
	{
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

			float directionMultiplier = Vector3.Dot(transform.rotation * Vector3.forward, Vector3.forward) * 1.2f - 0.2f;												// Turn Camera slower when looking at a flatter Angle
			transform.RotateAround(spacecraftTransform.position, transform.right, -Input.GetAxis("Mouse Y") * rotationSpeed * directionMultiplier);
			transform.RotateAround(spacecraftTransform.position, transform.up, Input.GetAxis("Mouse X") * rotationSpeed * directionMultiplier);
			transform.position += direction * movementSpeed;
		}
		else
		{
			Cursor.visible = true;
			Cursor.lockState = CursorLockMode.None;
		}

		if(Input.GetButtonUp("Reset Camera"))
		{
			transform.localPosition = startPosition;
			transform.localRotation = startRotation;
		}

		transform.position += transform.forward * Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
		if(transform.position.z > maxZHeight)
		{
			transform.position = new Vector3(transform.position.x, transform.position.y, maxZHeight);
		}
	}
}
