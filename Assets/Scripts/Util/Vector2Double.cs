using System;
using UnityEngine;

[Serializable]
public struct Vector2Double
{
	public static Vector2Double zero = new Vector2Double(0.0, 0.0);

	public double x;
	public double y;

	public Vector2Double(double x, double y)
	{
		this.x = x;
		this.y = y;
	}

	public Vector2Double(Vector2 vector)
	{
		this.x = vector.x;
		this.y = vector.y;
	}

	public Vector2Double(Vector3 vector)
	{
		this.x = vector.x;
		this.y = vector.y;
	}

	public static Vector2Double operator -(Vector2Double vector)
	{
		return new Vector2Double(-vector.x, -vector.y);
	}

	public static Vector2Double operator +(Vector2Double lho, Vector2Double rho)
	{
		return new Vector2Double(lho.x + rho.x, lho.y + rho.y);
	}

	public static Vector2Double operator -(Vector2Double lho, Vector2Double rho)
	{
		return new Vector2Double(lho.x - rho.x, lho.y - rho.y);
	}

	public static Vector2Double operator *(Vector2Double lho, double rho)
	{
		return new Vector2Double(lho.x * rho, lho.y * rho);
	}

	public static Vector2Double operator /(Vector2Double lho, double rho)
	{
		return new Vector2Double(lho.x / rho, lho.y / rho);
	}

	public static implicit operator Vector2Double(Vector2 vector)
	{
		return new Vector2Double(vector.x, vector.y);
	}

	public static implicit operator Vector2Double(Vector3 vector)
	{
		return new Vector2Double(vector.x, vector.y);
	}

	public static implicit operator Vector2(Vector2Double vector)
	{
		return new Vector2((float) vector.x, (float) vector.y);
	}

	public static implicit operator Vector3(Vector2Double vector)
	{
		return new Vector3((float) vector.x, (float) vector.y);
	}

	public static implicit operator string(Vector2Double vector)
	{
		return "(" + vector.x + "|" + vector.y + ")";
	}

	public static double Dot(Vector2Double lho, Vector2Double rho)
	{
		return lho.x * rho.x + lho.y * rho.y;
	}

	public double SqrMagnitude()
	{
		return x * x + y * y;
	}

	public double Magnitude()
	{
		return Math.Sqrt(x * x + y * y);
	}

	public static Vector2Double Perpendicular(Vector2Double vector)
	{
		return new Vector2Double(-vector.y, vector.x);
	}
}
