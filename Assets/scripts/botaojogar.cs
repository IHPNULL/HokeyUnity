using System.Collections;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using UnityEngine;

public class botaojogar : MonoBehaviour
{
    public bool mousedentro;
    public GameObject ativar;
    public GameObject ativar1;
    public GameObject desativar;
    public GameObject desativar1;
    public GameObject hockey1;
    public GameObject hockey2;
    public GameObject puck;
    private SpriteRenderer bt;
    Vector3 originalPos1;
    Vector3 originalPos2;
    Vector3 originalrot1;
    Vector3 originalrot2;

    void Start()
    {
        originalPos1 = new Vector3(hockey1.transform.position.x, hockey1.transform.position.y, hockey1.transform.position.z);
        originalPos2 = new Vector3(hockey2.transform.position.x, hockey2.transform.position.y, hockey2.transform.position.z);
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
                desativar.SetActive(false);
                desativar1.SetActive(false);
                ativar.SetActive(true);
                ativar1.SetActive(true);
                hockey1.transform.position = new Vector3(hockey1.transform.position.x, hockey1.transform.position.y, hockey1.transform.position.z);
                hockey2.transform.position = new Vector3(hockey2.transform.position.x, hockey2.transform.position.y, hockey2.transform.position.z);
                bt.color = new Color(255, 255, 255);
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