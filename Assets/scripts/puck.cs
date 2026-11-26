using UnityEngine;

public class puck : MonoBehaviour
{
    public bool enter = false;
    // Face-off spot (follows the rink size in 3x3).
    public static Vector3 originalPos => Rink.W(668.3125f, 390.6067f, 67.53125f);

    Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        ResetToCenter();
    }

    // Goals are trigger volumes inside each net ("gol" = red's goal, "gol (1)" = blue's goal).
    void OnTriggerEnter(Collider other) => CheckGoal(other.gameObject.name);

    // Kept for scenes where the goal volume is still a solid collider.
    void OnCollisionEnter(Collision col) => CheckGoal(col.gameObject.name);

    void CheckGoal(string goalName)
    {
        if (!enabled) return;   // online joiner: the host decides goals (trigger messages reach disabled scripts)
        if (goalName == "gol") { enter = true; marcadorazul.pontosazul++; ResetToCenter(); }
        else if (goalName == "gol (1)") { enter = true; marcadorverm.pontosverm++; ResetToCenter(); }
    }

    // Keep the puck in play: hard hits off the robots' wedge noses can pop it up and over the
    // net or boards. Cap the lift, and if it still ends up outside the rink, face off again.
    public static int OutOfPlayCount;   // for tests
    void FixedUpdate()
    {
        if (rb == null) return;
        var v = rb.linearVelocity;
        if (v.y > 250f) { v.y = 250f; rb.linearVelocity = v; }
        var p = rb.position;
        float sx = Rink.Scale.x, sz = Rink.Scale.y;
        bool outX = p.x < Rink.BlueGoal.x - 260f * sx || p.x > Rink.RedGoal.x + 260f * sx;
        bool outZ = p.z < Rink.MinZ - 120f * sz || p.z > Rink.MaxZ + 120f * sz;
        if (outX || outZ || p.y < -60f || p.y > 600f) { OutOfPlayCount++; ResetToCenter(); }
    }

    void ResetToCenter()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = originalPos;
        }
        transform.position = originalPos;
    }

    void OnTriggerExit(Collider other)
    {
        enter = false;
    }
}
