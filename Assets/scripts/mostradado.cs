using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class mostradado : MonoBehaviour
{
    public static int rotaao = 6;
    public static float speed = 1400;
    public bool rot = false;
    public bool vel = false;
    private Text msg;

    void Start()
    {
        msg = GetComponent<Text>();
    }

    void Update()
    {

        if(vel == true)
            msg.text = speed.ToString();
        else if (rot == true)
            msg.text = rotaao.ToString();
    }
}
