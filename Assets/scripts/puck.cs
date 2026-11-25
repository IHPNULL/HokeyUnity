using UnityEngine;

public class puck : MonoBehaviour
{
    public bool enter = false;
    public static Vector3 originalPos;

    Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        originalPos = new Vector3(668.3125f, 67.53125f, 390.6067f);
        ResetToCenter();
    }

    // Goals are trigger volumes inside each net ("gol" = red's goal, "gol (1)" = blue's goal).
    void OnTriggerEnter(Collider other) => CheckGoal(other.gameObject.name);

    // Kept for scenes where the goal volume is still a solid collider.
    void OnCollisionEnter(Collision col) => CheckGoal(col.gameObject.name);

    void CheckGoal(string goalName)
    {
        if (goalName == "gol") { enter = true; marcadorazul.pontosazul++; ResetToCenter(); }
        else if (goalName == "gol (1)") { enter = true; marcadorverm.pontosverm++; ResetToCenter(); }
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
