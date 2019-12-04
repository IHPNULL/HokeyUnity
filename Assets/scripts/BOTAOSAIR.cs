using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BOTAOSAIR : MonoBehaviour
{
    public bool mousedentro;
    private SpriteRenderer bt;



    void Start()
    {
        bt = GetComponent<SpriteRenderer>();
        mousedentro = false;
    }

    void Update()
    {
        if (mousedentro == true)
        {
            bt.color = new Color(255, 255, 0);
            if (Input.GetMouseButtonDown(0))
            {
                Application.Quit();
                Debug.Log("saindo");
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