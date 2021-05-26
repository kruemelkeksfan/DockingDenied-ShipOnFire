using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AsteroidController : MonoBehaviour
{
	private GravityWellController gravityWellController = null;
	private new Rigidbody2D rigidbody = null;
	private bool touched = false;

	private void OnDestroy()
	{
		gravityWellController = GravityWellController.GetInstance();
		rigidbody = GetComponent<Rigidbody2D>();
		if(rigidbody != null)
		{
			gravityWellController?.RemoveGravityObject(rigidbody);
		}
	}

	private void OnCollisionEnter2D(Collision2D collision)
	{
		if(!touched)
		{
			touched = gravityWellController.MarkAsteroidTouched(rigidbody, collision.gameObject.GetComponent<Rigidbody2D>());
		}
	}
}
