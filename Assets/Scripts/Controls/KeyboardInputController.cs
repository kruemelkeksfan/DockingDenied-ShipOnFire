using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeyboardInputController : MonoBehaviour
{
    [SerializeField] private GameObject buildingMenu = null;
    private Spacecraft spacecraft = null;

    private void Awake()
    {
        spacecraft = gameObject.GetComponent<Spacecraft>();
    }


    private void Update()
    {
        if(!buildingMenu.activeSelf)
		{
            spacecraft.SetThrottles(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), Input.GetAxis("Rotate"));
		}
    }
}
