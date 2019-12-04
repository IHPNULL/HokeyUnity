using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine;

public class WIN : MonoBehaviour
{
    private Text msg;
    private int min;

    public static bool ctr;
    public bool x;
    public bool y;
    public bool Z;
    public GameObject objeto;
    public GameObject objeto2;
    public GameObject objeto3;
    public static float time;
    public static int lmin = 5;
    public static int lpnt = 5;
    public GameObject bolinha;
    public GameObject hockey1;
    public GameObject hockey2;
    public Quaternion rotaçaoh1;
    public Quaternion rotaçaoh2;


    void Start()
    {
        puck.originalPos = new Vector3(668.3125f, 67.53125f, 390.6067f);
        msg = GetComponent<Text>();
        time = 0;
    }

    void Update()
    {
        Debug.Log(time);
        
        time += Time.deltaTime;

        if (time>=60)
        {
            time = 0;
            min++;
        }


        if(min == lmin || marcadorverm.pontosverm >= lpnt || marcadorazul.pontosazul >= lpnt)
        {
            if (marcadorverm.pontosverm > marcadorazul.pontosazul && ctr == false)
            {
                msg.text = "                    VITÓRIA!!!\n\n\n                   DERROTA!!!";
                ctr = true;
                time = 0;
            }
            else if(marcadorverm.pontosverm < marcadorazul.pontosazul && ctr == false)
            {
                msg.text = "                    DERROTA!!!\n\n\n                    VITÓRIA!!!";
                ctr = true;
                time = 0;
            }
            else if(marcadorverm.pontosverm == marcadorazul.pontosazul && ctr == false)
            {
                msg.text = "                    EMPATE!!!\n\n\n                    EMPATE!!!";
                ctr = true;
                time = 0;
            }
            if (time >= 4)
            {
              

               msg.text = null;
               ctr = false;
               time = 0;
               min = 0;
               objeto.SetActive(x);
               objeto2.SetActive(y);
               objeto3.SetActive(Z);
               marcadorverm.pontosverm = 0;
               marcadorazul.pontosazul = 0;

               hockey1.transform.position = new Vector3(1343.509f , 29.74236f , 371.5625f);               
               hockey1.transform.rotation = Quaternion.Euler(-91.34699f, -1.525879e-05f, -90f);
               hockey2.transform.position = new Vector3(41.3125f , 29.74236f , 411.5625f);
               hockey2.transform.rotation = Quaternion.Euler( -91.34699f, 0f, 90f);
               bolinha.transform.position = new Vector3(668.3125f, 67.53125f, 390.6067f);
            }
        }
    }
}
