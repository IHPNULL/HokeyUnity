using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class mostratempo : MonoBehaviour
{
    private Text msg;

    void Start()
    {
        msg = GetComponent<Text>();
    }

    void Update()
    {
        msg.text = WIN.lmin.ToString() + " MINUTOS";
    }
}
