using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour, IListener
{
	[SerializeField] private float movementSpeed = 1.0f;
	[SerializeField] private float rotationSpeed = 1.0f;
	[SerializeField] private float zoomSpeed = 1.0f;
	[SerializeField] private float maxViewAngle = 60.0f;
	[SerializeField] private float maxZHeight = -0.04f;
	[SerializeField] private float maxDistance = 2000.0f;
	private GravityWellController gravityWellController = null;
	private new Transform transform = null;
	private Transform spacecraftTransform = null;
	private Vector3 startPosition = Vector3.zero;
	private Vector3 startRotation = Vector3.zero;
	private Vector3 localPosition = Vector3.zero;
	private Vector3 localRotation = Vector3.zero;
	private bool fixedCamera = false;
	private float planetSurfaceAltitude = 0.0f;
	private float sqrPlanetSurfaceAltitude = 0.0f;

	private void Start()
	{
		gravityWellController = GravityWellController.GetInstance();

		transform = gameObject.GetComponent<Transform>();

		SpacecraftManager.GetInstance().AddSpacecraftChangeListener(this);
		Notify();

		startPosition = transform.localPosition;
		startRotation = transform.localRotation.eulerAngles;

		localPosition = startPosition;
		localRotation = startRotation;

		planetSurfaceAltitude = gravityWellController.GetSurfaceAltitude();
		// Square to avoid Sqrt later
		sqrPlanetSurfaceAltitude =  planetSurfaceAltitude * planetSurfaceAltitude;
	}

	private void Update()
	{
		// Query and apply Controls
		if(Input.GetButtonUp("Camera Mode"))
		{
			fixedCamera = !fixedCamera;

			InfoController.GetInstance().AddMessage("Camera " + (fixedCamera ? "now" : "no longer") + " rotates with Spacecraft");
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

			localPosition += direction * movementSpeed * -localPosition.z;
			localRotation += new Vector3(
				-Input.GetAxis("Mouse Y") * rotationSpeed,
				Input.GetAxis("Mouse X") * rotationSpeed,
				0.0f);

			// Ensure that the Player can't look back
			localRotation = new Vector3(
				Mathf.Clamp(localRotation.x, -maxViewAngle, maxViewAngle),
				Mathf.Clamp(localRotation.y, -maxViewAngle, maxViewAngle),
				0.0f);
		}
		else
		{
			Cursor.visible = true;
			Cursor.lockState = CursorLockMode.None;
		}

		if(!EventSystem.current.IsPointerOverGameObject())
		{
			localPosition += Vector3.forward * Input.GetAxis("Mouse ScrollWheel") * zoomSpeed * -localPosition.z;
		}

		if(Input.GetButtonUp("Reset Camera"))
		{
			localPosition = startPosition;
			localRotation = startRotation;
		}

		// Set Camera Position and Rotation
		transform.localPosition = localPosition;
		if(fixedCamera)
		{
			transform.rotation = spacecraftTransform.rotation;

			transform.RotateAround(spacecraftTransform.position, spacecraftTransform.right, localRotation.x);
			transform.RotateAround(spacecraftTransform.position, spacecraftTransform.up, localRotation.y);
			transform.RotateAround(spacecraftTransform.position, spacecraftTransform.forward, localRotation.z);
		}
		else
		{
			transform.rotation = Quaternion.identity;

			transform.RotateAround(spacecraftTransform.position, Vector3.right, localRotation.x);
			transform.RotateAround(spacecraftTransform.position, Vector3.up, localRotation.y);
			transform.RotateAround(spacecraftTransform.position, Vector3.forward, localRotation.z);
		}

		// Check Camera Constraints and if violated, correct the current Position and reverse engineer the new unrotated localPosition
		// Camera too close/below the Physics Plane?
		if(transform.position.z > maxZHeight)
		{
			transform.position = new Vector3(transform.position.x, transform.position.y, maxZHeight - MathUtil.EPSILON);
			localPosition = Quaternion.Inverse(transform.rotation) * (transform.position - spacecraftTransform.position);
		}
		// Camera too far away from the Action?
		if(Mathf.Abs(transform.position.x) > maxDistance
			|| Mathf.Abs(transform.position.y) > maxDistance
			|| Mathf.Abs(transform.position.z) > maxDistance)
		{
			transform.position = new Vector3(
				Mathf.Clamp(transform.position.x, -maxDistance + MathUtil.EPSILON, maxDistance - MathUtil.EPSILON),
				Mathf.Clamp(transform.position.y, -maxDistance + MathUtil.EPSILON, maxDistance - MathUtil.EPSILON),
				Mathf.Clamp(transform.position.z, -maxDistance + MathUtil.EPSILON, maxDistance - MathUtil.EPSILON));
			localPosition = Quaternion.Inverse(transform.rotation) * (transform.position - spacecraftTransform.position);
		}
		// Camera inside the Planet?
		Vector3 globalPosition = (Vector3) gravityWellController.LocalToGlobalPosition(transform.position) + new Vector3(0.0f, 0.0f, transform.position.z);
		if(globalPosition.sqrMagnitude <= sqrPlanetSurfaceAltitude)
		{
			globalPosition = globalPosition.normalized * planetSurfaceAltitude;
			transform.position = (Vector3) gravityWellController.GlobalToLocalPosition(globalPosition) + new Vector3(0.0f, 0.0f, globalPosition.z);
			localPosition = Quaternion.Inverse(transform.rotation) * (transform.position - spacecraftTransform.position);
		}
	}

	public void Notify()
	{
		spacecraftTransform = SpacecraftManager.GetInstance().GetLocalPlayerMainSpacecraft().GetTransform();
	}
}
