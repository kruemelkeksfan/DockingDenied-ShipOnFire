using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Module : MonoBehaviour
{
	[SerializeField] private int mass = 1;
	[SerializeField] private int hp = 100;
	[SerializeField] private Vector2Int[] reservedPositions = { Vector2Int.zero };
	[SerializeField] private bool firstPositionNeighboursOnly = false;
	private Vector2Int[] bufferedReservedPositions = { Vector2Int.zero };
	private bool finished = false;
	private new Transform transform = null;
	private Spacecraft spacecraft = null;
	private Vector2Int position = Vector2Int.zero;

	private void Awake()
	{
		transform = gameObject.GetComponent<Transform>();
		bufferedReservedPositions = reservedPositions;
	}

	private void Start()
	{
		spacecraft = gameObject.GetComponentInParent<Spacecraft>();
	}

	public void Build(Vector2Int position)
	{
		finished = true;
		this.position = position;
		UpdateBuffer(position);

		foreach(Vector2Int bufferedReservedPosition in bufferedReservedPositions)
		{
			spacecraft.AddModule(bufferedReservedPosition, this);
		}
	}
	public void Deconstruct()
	{
		if(position != Vector2Int.zero)													// Do not allow to remove the Command Module
		{
			foreach(Vector2Int bufferedReservedPosition in bufferedReservedPositions)
			{
				spacecraft.RemoveModule(bufferedReservedPosition);
			}

			GameObject.Destroy(gameObject, 0.02f);
		}
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
		if(!finished)
		{
			bufferedReservedPositions = new Vector2Int[reservedPositions.Length];
			for(int i = 0; i < bufferedReservedPositions.Length; ++i)
			{
				bufferedReservedPositions[i] = Vector2Int.RoundToInt(position + (Vector2)(transform.rotation * (Vector2)reservedPositions[i]));
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
