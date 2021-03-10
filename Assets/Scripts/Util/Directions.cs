using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Directions
{
	public const byte UP = 0;
	public const byte RIGHT = 1;
	public const byte DOWN = 2;
	public const byte LEFT = 3;
	public readonly static Vector2Int[] VECTORS = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
}
