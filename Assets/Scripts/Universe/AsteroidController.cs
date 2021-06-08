using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AsteroidController : MonoBehaviour
{
	private bool touched = false;

	private void OnDestroy()
	{
		Rigidbody2D rigidbody = GetComponent<Rigidbody2D>();
		if(rigidbody != null)
		{
			AsteroidSpawner.GetInstance()?.RemoveAsteroid(rigidbody);
		}
	}

	private void OnCollisionEnter2D(Collision2D collision)
	{
		if(!touched)
		{
			touched = GravityWellController.GetInstance().MarkAsteroidTouched(GetComponent<Rigidbody2D>(), collision.gameObject.GetComponent<Rigidbody2D>());
		}
	}
}
