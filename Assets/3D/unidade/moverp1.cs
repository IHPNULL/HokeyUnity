using UnityEngine;
using System.Collections;

public class moverp1 : MonoBehaviour
{
    private Rigidbody b;

    public string horizontal;
    public string vertical;
    public int gravit;
    private float rot;
    private float vel;
    public Quaternion rotaçao;

    public float Horizontal
    {
        get;
        private set;
    }

    public float Vertical
    {
        get;
        private set;
    }

    void Start()
    {
        b = GetComponent<Rigidbody>();
        rot = mostradado.rotaao;
        vel = mostradado.speed;
    }

    void FixedUpdate()
    {
        rotaçao = (b.transform.rotation);
        float moveHorizontal = Input.GetAxis(horizontal);
        float moveVertical = Input.GetAxis(vertical);


        Physics.gravity = new Vector3(0, -1 * gravit, 0);

        transform.Rotate(0, 0, moveVertical * rot);
        b.velocity = (moveHorizontal * vel * -transform.up);
    }
  }