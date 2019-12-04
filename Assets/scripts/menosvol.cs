using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;


public class menosvol : MonoBehaviour
{

    public bool mousedentro;
    public AudioSource som;


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
                if (som.volume >= 0)
                    som.volume -= 0.1f;
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