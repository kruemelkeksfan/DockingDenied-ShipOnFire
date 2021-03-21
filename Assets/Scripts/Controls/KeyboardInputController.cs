using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeyboardInputController : MonoBehaviour
{
    [SerializeField] private Spacecraft spacecraft = null;
    [SerializeField] private GameObject buildingMenu = null;

    private void Update()
    {
        if(!buildingMenu.activeSelf)
		{
            spacecraft.SetThrottles(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), Input.GetAxis("Rotate"));
		}
    }
}
