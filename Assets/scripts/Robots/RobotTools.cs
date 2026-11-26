using System.Collections.Generic;
using UnityEngine;

// The robot's tool in play, plus strength on puck hits. Reads moverp1.Action (keyboard or AI)
// and triggers on the press. Positions are in the chassis' local frame (see RobotLoadout).
public class RobotTools : MonoBehaviour
{
    public RobotSpec spec;
    public RobotTool Tool => spec ? spec.tool : RobotTool.None;
    public float Cooldown => Mathf.Max(0f, readyAt - Time.time);
    public bool Holding { get; private set; }      // Claw: puck in the cradle
    public int Uses { get; private set; }          // for tests

    [System.NonSerialized] public Vector3 originalScale; [System.NonSerialized] public bool originalSet;   // RobotLoadout
    [System.NonSerialized] public Transform kickPlate;
    [System.NonSerialized] public List<Renderer> flames;

    moverp1 mover; Rigidbody body, puckRb;
    bool lastAction; float readyAt, kickAnimUntil, releasedAt = -10f;
    Vector3 plateRest;

    const float BoostTime = 0.7f, BoostCooldown = 3f, KickCooldown = 1f, ClawCooldown = 0.6f;

    public void Setup(RobotSpec s)
    {
        spec = s; readyAt = 0; Holding = false; lastAction = false;
        mover = GetComponent<moverp1>(); body = GetComponent<Rigidbody>();
    }

    Rigidbody Puck()
    {
        if (puckRb) return puckRb;
        var p = FindAnyObjectByType<puck>(FindObjectsInactive.Exclude);
        if (p) puckRb = p.GetComponent<Rigidbody>();
        return puckRb;
    }

    // World point of the puck's resting spot in the pocket / just ahead of the arms.
    Vector3 Local(float x, float y) => body.position + body.rotation * Vector3.Scale(new Vector3(x, y, 0f), transform.lossyScale);
    Vector3 Forward { get { var f = body.rotation * Vector3.down; f.y = 0; return f.normalized; } }

    void FixedUpdate()
    {
        if (spec == null || mover == null) return;
        bool action = mover.Action && !moverp1.Frozen;
        if (action && !lastAction) Trigger();
        lastAction = action;

        if (Tool == RobotTool.Claw) Cradle();
        if (Tool == RobotTool.Booster && flames != null) foreach (var f in flames) if (f) f.gameObject.SetActive(mover.Boosting);
        if (kickPlate)
        {
            if (plateRest == Vector3.zero) plateRest = kickPlate.localPosition;
            kickPlate.localPosition = plateRest + (Time.time < kickAnimUntil ? new Vector3(0, -0.35f, 0) : Vector3.zero);
        }
    }

    // Fire the tool now (the key press; tests call it directly).
    public void Trigger()
    {
        if (Time.time < readyAt || moverp1.Frozen) return;
        var p = Puck();
        switch (Tool)
        {
            case RobotTool.Booster:
                mover.boostUntil = Time.time + BoostTime; readyAt = Time.time + BoostCooldown; Uses++;
                break;
            case RobotTool.Kicker:
                readyAt = Time.time + KickCooldown; kickAnimUntil = Time.time + 0.12f; Uses++;
                if (p && InPocket(p.position, 1.25f)) Shoot(p, 1900f);
                break;
            case RobotTool.Claw:
                if (Holding && p) { Shoot(p, 1300f); Holding = false; releasedAt = Time.time; readyAt = Time.time + ClawCooldown; Uses++; }
                break;
        }
    }

    void Shoot(Rigidbody p, float speed)
    {
        Vector3 v = Forward * speed * spec.strength + Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up) * 0.5f;
        v.y = 0;
        p.linearVelocity = v;
        GameSfxHook.Kick?.Invoke(p.position);
    }

    // Is the point inside the pocket between the arms (reach scales the depth past the arm tips)?
    public bool InPocket(Vector3 world, float reach = 1f)
    {
        var l = transform.InverseTransformPoint(world);   // chassis local (scale removed)
        return Mathf.Abs(l.x) < RobotLoadout.ArmInner + 0.05f && l.y < RobotLoadout.PocketBack + 0.05f && l.y > RobotLoadout.ArmTip * reach - 0.1f;
    }

    // Claw: a puck in the pocket follows the robot while it isn't turning hard or getting hit.
    void Cradle()
    {
        var p = Puck(); if (!p || moverp1.Frozen) { Holding = false; return; }
        if (Time.time - releasedAt < ClawCooldown) { Holding = false; return; }
        Vector3 rv = body.linearVelocity; rv.y = 0;
        Vector3 pv = p.linearVelocity; pv.y = 0;
        // Only a puck deep in the pocket, while the robot is really driving (pinned against the
        // boards the cradle would just squeeze the puck out sideways).
        var l = transform.InverseTransformPoint(p.position);
        bool inside = InPocket(p.position) && l.y > -0.55f;
        float spin = Mathf.Abs(mover.Vertical) * mostradado.rotaao * mover.turnMul;   // deg per 50 Hz step
        bool slip = (pv - rv).magnitude > 1100f || spin > 7.5f || rv.magnitude < 120f;
        if (!inside || slip) { Holding = false; return; }
        Holding = true;
        // Pull the puck to the cradle spot (just ahead of the body front) and carry it.
        Vector3 spot = Local(0f, RobotLoadout.PocketBack - 0.38f); spot.y = p.position.y;
        Vector3 want = rv + Vector3.ClampMagnitude((spot - p.position) * 10f, 300f);
        Vector3 nv = Vector3.Lerp(pv, want, 0.35f); nv.y = p.linearVelocity.y;
        p.linearVelocity = nv;
    }

    // Strength: harder hits on the puck from the robot's body.
    void OnCollisionEnter(Collision c)
    {
        if (spec == null || c.rigidbody == null || c.rigidbody.GetComponent<puck>() == null) return;
        if (Mathf.Approximately(spec.strength, 1f) || Tool == RobotTool.Claw && Holding) return;
        Vector3 away = c.rigidbody.position - body.position; away.y = 0;   // robot -> puck
        float hit = c.relativeVelocity.magnitude;
        if (hit <= 150f || away.sqrMagnitude < 1f) return;
        c.rigidbody.AddForce(away.normalized * hit * (spec.strength - 1f) * 0.6f, ForceMode.VelocityChange);
    }
}

// Optional sound hook (GameSfx sets it) so the tool code doesn't depend on the audio code.
public static class GameSfxHook { public static System.Action<Vector3> Kick; }
