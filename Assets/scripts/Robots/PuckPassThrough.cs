using UnityEngine;

// A collider the puck ignores: the robot's "Pocket Plug" fills the open U-pocket so pillars, boards and
// other robots can't wedge into it, while the puck still slides in for the claw and the kicker.
// Physics.IgnoreCollision is reset whenever either collider is disabled, so it is re-applied on enable
// and once a second.
[RequireComponent(typeof(Collider))]
public class PuckPassThrough : MonoBehaviour
{
    Collider own; float next;

    void OnEnable() { own = GetComponent<Collider>(); Apply(); }

    void FixedUpdate() { if (Time.time >= next) Apply(); }

    void Apply()
    {
        next = Time.time + 1f;
        if (!own) return;
        foreach (var p in FindObjectsByType<puck>(FindObjectsSortMode.None))
            foreach (var c in p.GetComponentsInChildren<Collider>())
                if (c && c.enabled) Physics.IgnoreCollision(own, c, true);
    }
}
