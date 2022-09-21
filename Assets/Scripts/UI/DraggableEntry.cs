using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public abstract class DraggableEntry : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
	protected InfoController infoController = null;
	protected bool armed = false;
	private new RectTransform transform = null;
	private RectTransform mainCanvas = null;
	private EventSystem eventSystem = null;
	private List<RaycastResult> raycastResults = null;

	protected abstract bool Transfer(List<RaycastResult> hoveredGameObjectResults);

	private void Start()
	{
		infoController = InfoController.GetInstance();
		mainCanvas = GameObject.FindGameObjectWithTag("MainUI").GetComponent<RectTransform>();
		transform = GetComponent<RectTransform>();
		eventSystem = EventSystem.current;
		raycastResults = new List<RaycastResult>();
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		transform.SetParent(mainCanvas, true);
	}

	public void OnDrag(PointerEventData eventData)
	{
		if(armed)
		{
			transform.anchoredPosition += eventData.delta / transform.lossyScale;
		}
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		if(armed)
		{
			eventSystem.RaycastAll(eventData, raycastResults);
			Transfer(raycastResults);
			GameObject.Destroy(gameObject);
		}
	}
}
