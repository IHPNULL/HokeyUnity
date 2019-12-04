using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class mostraponto : MonoBehaviour
{
    private Text msg;

    void Start()
    {
        msg = GetComponent<Text>();
    }

    void Update()
    {
        msg.text = WIN.lpnt.ToString() + " PONTOS";  
    }
}
