using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class buint : MonoBehaviour
{
    public bool mousedentro;
    public bool x;
    public bool y;
    public bool Z;
    public GameObject objeto;
    public GameObject objeto2;
    public GameObject objeto3;
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
                bt.color = new Color(255, 255, 255);
                objeto.SetActive(x);
                objeto2.SetActive(y);
                objeto3.SetActive(Z);
                WIN.time = 0;
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