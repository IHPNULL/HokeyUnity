using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class marcadorverm : MonoBehaviour
{
    private Text texto;
    public static int pontosverm = 0;

    void Start()
    {
        texto = GetComponent<Text>();
    }



    void Update()
    {
        texto.text = "VERMELHO: " + pontosverm.ToString();
    }
}