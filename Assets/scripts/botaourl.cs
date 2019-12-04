using System.Collections;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using UnityEngine;

public class botaourl : MonoBehaviour
{
    public bool mousedentro;
    public string URL;

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
                Application.OpenURL(URL);
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