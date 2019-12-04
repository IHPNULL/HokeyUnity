using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Botaomenos : MonoBehaviour
{
    public bool mousedentro;
    bool controle;
    public SpriteRenderer bt;


    void Start()
    {
        bt = GetComponent<SpriteRenderer>();
        mousedentro = false;
    }

    void Update()
    {
        if (mousedentro == true)
        {
            if (Input.GetMouseButtonDown(0))
            {
                bt.color = new Color(255, 255, 255);
                if((mostradado.speed - 100) > 0 )
                    mostradado.speed -= 100;
            }
        }
    }

    private void OnMouseEnter()
    {
        mousedentro = true;

    }
    private void OnMouseExit()
    {
        bt.color = new Color(255, 255, 255);
        mousedentro = false;
    }
}