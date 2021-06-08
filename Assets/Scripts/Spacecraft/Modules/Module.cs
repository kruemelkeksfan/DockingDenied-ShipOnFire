using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Module : MonoBehaviour, IUpdateListener, IFixedUpdateListener
{
	[SerializeField] protected string moduleName = "Module";
	[SerializeField] protected int hp = 100;
	[SerializeField] protected bool pressurized = true;
	[SerializeField] private Vector2Int[] reservedPositions = { Vector2Int.zero };
	[Tooltip("Whether all reserved Positions after the First still provide valid Attachment Points.")]
	[SerializeField] private bool attachableReservePositions = false;
	[Tooltip("Whether all reserved Positions after the First can overlap with other reserved Positions which have this Flag enabled.")]
	[SerializeField] private bool overlappingReservePositions = false;
	[SerializeField] private GoodManager.Load[] buildingCosts = { new GoodManager.Load("Steel", 0), new GoodManager.Load("Aluminium", 0),
		new GoodManager.Load("Copper", 0), new GoodManager.Load("Gold", 0), new GoodManager.Load("Silicon", 0) };
	[TextArea(1, 2)] [SerializeField] private string description = "Module Description missing!";
	protected float mass = 0.0002f;
	private Vector2Int[] bufferedReservedPositions = { Vector2Int.zero };
	protected bool constructed = false;
	protected new Transform transform = null;
	protected Spacecraft spacecraft = null;
	protected Vector2Int position = Vector2Int.zero;

	protected virtual void Awake()
	{
		transform = gameObject.GetComponent<Transform>();
		bufferedReservedPositions = new Vector2Int[reservedPositions.Length];
	}

	protected virtual void Start()
	{

	}

	public virtual void Build(Vector2Int position, bool listenUpdates = false, bool listenFixedUpdates = false)
	{
		spacecraft = gameObject.GetComponentInParent<Spacecraft>();

		GoodManager goodManager = GoodManager.GetInstance();
		mass = 0.0f;
		foreach(GoodManager.Load cost in buildingCosts)
		{
			mass += goodManager.GetGood(cost.goodName).mass * cost.amount;
		}
		if(mass <= 0.0f)
		{
			mass = 0.0002f;
		}

		this.position = position;
		transform.localPosition = BuildingMenu.GetInstance().GridToLocalPosition(position);
		UpdateReservedPositionBuffer(position, transform.localRotation);
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

	private void UpdateReservedPositionBuffer(Vector2Int position, Quaternion localRotation)
	{
		if(!constructed)
		{
			for(int i = 0; i < bufferedReservedPositions.Length; ++i)
			{
				bufferedReservedPositions[i] = Vector2Int.RoundToInt(position + (Vector2)(localRotation * (Vector2)reservedPositions[i]));
			}
		}
	}

	public string GetModuleName()
	{
		return moduleName;
	}

	public string GetDescription()
	{
		return description;
	}

	public Vector2Int GetPosition()
	{
		return position;
	}

	public Transform GetTransform()
	{
		return transform;
	}

	public Spacecraft GetSpacecraft()
	{
		return spacecraft;
	}

	public Vector2Int[] GetReservedPositions(Vector2Int position, Quaternion localRotation)
	{
		UpdateReservedPositionBuffer(position, localRotation);

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

	public GoodManager.Load[] GetBuildingCosts()
	{
		return buildingCosts;
	}
}
