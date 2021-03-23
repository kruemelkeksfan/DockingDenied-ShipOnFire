using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Module : MonoBehaviour
{
	[SerializeField] protected string moduleName = "Module";
	[SerializeField] protected int mass = 1;
	[SerializeField] protected int hp = 100;
	[SerializeField] private Vector2Int[] reservedPositions = { Vector2Int.zero };
	[Tooltip("Whether all reserved Positions after the First still provide valid Attachment Points.")]
	[SerializeField] private bool attachableReservePositions = false;
	[Tooltip("Whether all reserved Positions after the First can overlap with other reserved Positions which have this Flag enabled.")]
	[SerializeField] private bool overlappingReservePositions = false;
	private Vector2Int[] bufferedReservedPositions = { Vector2Int.zero };
	protected bool constructed = false;
	protected new Transform transform = null;
	protected Spacecraft spacecraft = null;
	protected Vector2Int position = Vector2Int.zero;

	protected virtual void Awake()
	{
		transform = gameObject.GetComponent<Transform>();
		bufferedReservedPositions = reservedPositions;
	}

	protected virtual void OnEnable()
	{
		
	}

	protected virtual void Start()
	{

	}

	public virtual void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		spacecraft = gameObject.GetComponentInParent<Spacecraft>();

		this.position = position;
		transform.localPosition = BuildingMenu.GetInstance().GridToLocalPosition(position);
		UpdateReservedPositionBuffer(position);
		spacecraft.UpdateModuleMass(transform.localPosition, mass);
		constructed = true;

		foreach(Vector2Int bufferedReservedPosition in bufferedReservedPositions)
		{
			spacecraft.AddModule(bufferedReservedPosition, this);
		}

		if(listenUpdates)
		{
			spacecraft.AddUpdateListener(this);
		}
		if(listenFixedUpdates)
		{
			spacecraft.AddFixedUpdateListener(this);
		}
	}

	public virtual void Deconstruct()
	{
		spacecraft.UpdateModuleMass(transform.localPosition, -mass);

		foreach(Vector2Int bufferedReservedPosition in bufferedReservedPositions)
		{
			spacecraft.RemoveModule(bufferedReservedPosition);
		}

		GameObject.Destroy(gameObject);

		spacecraft.RemoveUpdateListener(this);
		spacecraft.RemoveFixedUpdateListener(this);
	}

	public void Rotate(int direction)
	{
		Rotate(direction * -90.0f);
	}

	public void Rotate(float angle)
	{
		if(angle % 90.0f == 0.0f)
		{
			transform.localRotation = Quaternion.Euler(0.0f, 0.0f, angle);
		}
		else
		{
			Debug.LogWarning("Trying to rotate Module " + this + " by " + angle + "Degrees which is not a Multiple of 90.0 Degrees!");
		}
	}

	public virtual void UpdateNotify()
	{

	}

	public virtual void FixedUpdateNotify()
	{

	}

	private void UpdateReservedPositionBuffer(Vector2Int position)
	{
		if(!constructed)
		{
			bufferedReservedPositions = new Vector2Int[reservedPositions.Length];
			for(int i = 0; i < bufferedReservedPositions.Length; ++i)
			{
				bufferedReservedPositions[i] = Vector2Int.RoundToInt(position + (Vector2)(transform.localRotation * (Vector2)reservedPositions[i]));
			}
		}
	}

	public string GetModuleName()
	{
		return moduleName;
	}

	public Vector2Int GetPosition()
	{
		return position;
	}

	public Transform GetTransform()
	{
		return transform;
	}

	public Vector2Int[] GetReservedPositions(Vector2Int position)
	{
		UpdateReservedPositionBuffer(position);

		return bufferedReservedPositions;
	}

	public bool HasAttachableReservePositions()
	{
		return attachableReservePositions;
	}

	public bool HasOverlappingReservePositions()
	{
		return overlappingReservePositions;
	}
}
