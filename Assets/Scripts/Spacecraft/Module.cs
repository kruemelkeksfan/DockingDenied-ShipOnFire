using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Module : MonoBehaviour
{
	[SerializeField] protected int mass = 1;
	[SerializeField] protected int hp = 100;
	[SerializeField] private Vector2Int[] reservedPositions = { Vector2Int.zero };
	[SerializeField] private bool firstPositionNeighboursOnly = false;
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

	protected virtual void Start()
	{
		spacecraft = gameObject.GetComponentInParent<Spacecraft>();
	}

	public virtual void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		constructed = true;
		this.position = position;
		UpdateBuffer(position);
		spacecraft.UpdateModuleMass(transform.localPosition, mass);

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
		spacecraft.UpdateModuleMass(transform.localPosition, 0.0f, mass);

		foreach(Vector2Int bufferedReservedPosition in bufferedReservedPositions)
		{
			spacecraft.RemoveModule(bufferedReservedPosition);
		}

		GameObject.Destroy(gameObject, 0.02f);

		spacecraft.RemoveUpdateListener(this);
		spacecraft.RemoveFixedUpdateListener(this);
	}

	public void Rotate(int direction)
	{
		transform.localRotation = Quaternion.Euler(0.0f, 0.0f, direction * -90.0f);
	}

	public virtual void UpdateNotify()
	{

	}

	public virtual void FixedUpdateNotify()
	{

	}

	private void UpdateBuffer(Vector2Int position)
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

	public Vector2Int GetPosition()
	{
		return position;
	}

	public Vector2Int[] GetReservedPositions(Vector2Int position)
	{
		UpdateBuffer(position);

		return bufferedReservedPositions;
	}

	public bool GetFirstPositionNeighboursOnly()
	{
		return firstPositionNeighboursOnly;
	}
}
