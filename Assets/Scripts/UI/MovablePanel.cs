using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class MovablePanel : MonoBehaviour, IDragHandler
{
	private new RectTransform transform = null;
	private RectTransform canvasTransform = null;

	private void Start()
	{
		transform = GetComponent<RectTransform>();
		canvasTransform = GetComponentInParent<Canvas>().gameObject.GetComponent<RectTransform>();
	}

	public void OnDrag(PointerEventData eventData)
	{
		transform.anchoredPosition += eventData.delta / transform.lossyScale;

		// Basic Position Clamping copied from https://forum.unity.com/threads/keep-ui-objects-inside-screen.523766/#post-4728482
		// And adjusted to work for arbitrary Anchors
		float minX = -(canvasTransform.sizeDelta.x * transform.anchorMin.x) + (transform.sizeDelta.x * transform.pivot.x);
		float maxX = (canvasTransform.sizeDelta.x * (1.0f - transform.anchorMax.x)) - (transform.sizeDelta.x * (1.0f - transform.pivot.x));
		float minY = -(canvasTransform.sizeDelta.y * transform.anchorMin.y) + (transform.sizeDelta.y * transform.pivot.y);
		float maxY = (canvasTransform.sizeDelta.y * (1.0f - transform.anchorMax.y)) - (transform.sizeDelta.y * (1.0f - transform.pivot.y));
		float clampedX = Mathf.Clamp(transform.anchoredPosition.x, minX, maxX);
		float clampedY = Mathf.Clamp(transform.anchoredPosition.y, minY, maxY);
		transform.anchoredPosition = new Vector2(clampedX, clampedY);
	}
}
