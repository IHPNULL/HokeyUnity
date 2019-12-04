using UnityEngine;
using System.Collections;


public class Mover : MonoBehaviour
{
    public float speed;
    public string horizontal;
    public string vertical;
    private Rigidbody rb;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }
    void FixedUpdate()
    {
        float moveHorizontal = Input.GetAxis(horizontal);
        float moveVertical = Input.GetAxis(vertical);


        Vector3 movement = new Vector3(moveHorizontal, 0.0f, moveVertical);
        rb.Transform.Position(movement * speed,0,0);
        //rb.Rotation(movement * speed);
    }
}


