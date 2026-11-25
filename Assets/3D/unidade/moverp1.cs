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

    // Null = keyboard axes above. MatchManager sets this (AI for red in 1 Player).
    public IRobotInput input;
    // Robots hold still during menus, countdowns and goal replays.
    public static bool Frozen;
    // 1 = robots stop and turn on a dime; lower = they skid (the ice arena).
    public static float Grip = 1f;

    void Start()
    {
        b = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        rotaçao = (b.transform.rotation);
        rot = mostradado.rotaao;
        vel = mostradado.speed;
        float moveHorizontal, moveVertical;
        if (Frozen) { moveHorizontal = 0; moveVertical = 0; }
        else if (input != null) { moveHorizontal = input.Drive; moveVertical = input.Turn; }
        else { moveHorizontal = Input.GetAxis(horizontal); moveVertical = Input.GetAxis(vertical); }
        Horizontal = moveHorizontal; Vertical = moveVertical;


        Physics.gravity = new Vector3(0, -1 * gravit, 0);

        transform.Rotate(0, 0, moveVertical * rot);
        var wanted = moveHorizontal * vel * -transform.up;
        b.linearVelocity = Grip >= 1f ? wanted : Vector3.Lerp(b.linearVelocity, wanted, Grip);
    }
  }