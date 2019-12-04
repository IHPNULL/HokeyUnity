using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class menospontos : MonoBehaviour
{
    public bool mousedentro;

    void Start()
    {
        mousedentro = false;
    }

    void Update()
    {
        if (mousedentro == true)
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (WIN.lpnt > 1)
                    WIN.lpnt -= 1;
            }
        }
    }

    private void OnMouseEnter()
    {
        mousedentro = true;

    }
    private void OnMouseExit()
    {
        mousedentro = false;
    }
}