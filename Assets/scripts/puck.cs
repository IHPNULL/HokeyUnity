using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class puck : MonoBehaviour
{
    

    public bool enter = false;
    public static Vector3 originalPos;

    void Start()
    {
        originalPos = new Vector3(668.3125f, 67.53125f, 390.6067f);
        gameObject.transform.position = originalPos;
    }

    private void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.name == "gol")
        { 
            enter = true;
            marcadorazul.pontosazul++;
            gameObject.transform.position = originalPos;
            //Debug.Log(marcador.pontosverme + "   /  " + marcadorazul.pontosazul);
        }

        if (col.gameObject.name == "gol (1)")
          {
            enter = true;
            marcadorverm.pontosverm++;
            gameObject.transform.position = originalPos;
            //Debug.Log(marcador.pontosverme + "   /  " + marcadorazul.pontosazul);
        }
    }
    private void OnTriggerExit(Collider other)
    {
            enter = false;
    }
}
