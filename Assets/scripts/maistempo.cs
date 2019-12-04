using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class maistempo : MonoBehaviour
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
                if (WIN.lmin < 15)
                    WIN.lmin += 1;
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