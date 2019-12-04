using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class marcadorazul : MonoBehaviour
{
    private Text texto;
    public static int pontosazul = 0;

    void Start()
    {
        texto = GetComponent<Text>();
    }



    void Update()
    {
        texto.text = "AZUL:  " + pontosazul.ToString();
    }
}