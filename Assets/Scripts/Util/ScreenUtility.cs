using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScreenUtility
{
    public static Vector2? WorldToUIPoint(Vector3 worldPoint, Camera camera, Transform cameraTransform, RectTransform uiTransform)
	{
		if(Vector3.Dot(cameraTransform.forward, worldPoint - cameraTransform.position) >= 0.0f)
		{
			Vector2 screenPoint;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(uiTransform, camera.WorldToScreenPoint(worldPoint), null, out screenPoint);
			return screenPoint;
		}
		else
		{
			return null;
		}
	}
}
