using System.Collections.Generic;
using UnityEngine;

// Hand-written opponent (no ML, no NavMesh): the rink is an open box.
// Rule 1: get behind the puck on the line to the target goal, then charge through it;
//         if the robot is on the wrong side of the puck, swing around it instead of pushing it home.
// Rule 2: steer toward that point, slowing down for sharp turns; back up when stuck.
// Difficulty = top speed fraction + how late the AI sees the puck + how far it lets the puck drift.
public class AIRobotInput : MonoBehaviour, IRobotInput
{
    public Transform puck;
    public bool blueTeam;                    // false = red (defends the +x goal)
    Vector3 attackGoal => blueTeam ? Rink.RedGoal : Rink.BlueGoal;   // goal the AI shoots at
    Vector3 ownGoal => blueTeam ? Rink.BlueGoal : Rink.RedGoal;      // goal the AI defends

    // 3x3: the team decides who goes for the puck. Attack = the 1x1 behaviour; Defend = hold
    // the slot between the puck and our net; Support = wait upfield on the far side for a pass.
    public enum Role { Attack, Defend, Support }
    public Role role = Role.Attack;

    public float speedFraction = 0.8f, reactionDelay = 0.18f, aimError = 40f;

    public float Drive { get; private set; }
    public float Turn { get; private set; }
    public Vector3 Target { get; private set; }
    public string Mode { get; private set; }

    const float Behind = 110f, RobotBack = 70f;
    static readonly float[] Offsets = { 0, 20, -20, 40, -40, 60, -60, 80, -80, 100, -100 };
    readonly RaycastHit[] hits = new RaycastHit[8];

    // Pillars and goal posts in the straight path: detour around the side away from them.
    Vector3 AvoidObstacles(Vector3 me, Vector3 target)
    {
        Vector3 d = target - me; float dist = d.magnitude;
        if (dist < 1f) return target;
        d /= dist;
        int n = Physics.SphereCastNonAlloc(new Vector3(me.x, 70f, me.z), 55f, d, hits, dist, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue; Collider obstacle = null;
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider; var b = c.bounds.size;
            if (c.attachedRigidbody != null || b.x > 300f || b.z > 300f || hits[i].distance <= 0f) continue;
            if (hits[i].distance < best) { best = hits[i].distance; obstacle = c; }
        }
        if (obstacle == null) return target;
        Vector3 oc = obstacle.bounds.center; oc.y = 0;
        Vector3 side = Vector3.Cross(Vector3.up, d);
        float s = Vector3.Dot(oc - me, side) > 0f ? -1f : 1f;
        float r = Mathf.Max(obstacle.bounds.extents.x, obstacle.bounds.extents.z);
        return oc + side * s * (r + 130f);
    }

    // True when nothing static (boards, goal frame) is within dist of p along dir.
    bool Clear(Vector3 p, Vector3 dir, float dist)
    {
        int n = Physics.RaycastNonAlloc(new Vector3(p.x, 30f, p.z), dir.normalized, hits, dist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++) if (hits[i].collider.attachedRigidbody == null) return false;
        return true;
    }

    readonly Queue<(float t, Vector3 p)> seen = new Queue<(float, Vector3)>();
    Vector3 lastPos, lastSeen; float stuckTime, reverseUntil, noise;

    public void SetDifficulty(GameConfig.Difficulty d)
    {
        switch (d)
        {
            case GameConfig.Difficulty.Easy: speedFraction = 0.55f; reactionDelay = 0.40f; aimError = 110f; break;
            case GameConfig.Difficulty.Medium: speedFraction = 0.78f; reactionDelay = 0.20f; aimError = 55f; break;
            default: speedFraction = 0.97f; reactionDelay = 0.06f; aimError = 15f; break;
        }
    }

    Rigidbody body, puckBody;
    // Physics-step positions: with interpolation on, transform.position lags the simulation.
    Vector3 MyPos => body ? body.position : transform.position;
    Vector3 PuckPos => puckBody ? puckBody.position : puck.position;

    void OnEnable() { seen.Clear(); body = GetComponent<Rigidbody>(); puckBody = puck ? puck.GetComponent<Rigidbody>() : null; lastPos = MyPos; lastSeen = puck ? PuckPos : Vector3.zero; lastSeen.y = 0; stuckTime = 0; reverseUntil = 0; }

    void FixedUpdate()
    {
        if (puck == null || moverp1.Frozen) { Drive = Turn = 0; stuckTime = 0; lastPos = MyPos; lastPos.y = 0; return; }
        float now = Time.time;
        if (puckBody == null || puckBody.transform != puck) puckBody = puck.GetComponent<Rigidbody>();
        seen.Enqueue((now, PuckPos));
        while (seen.Count > 1 && seen.Peek().t < now - reactionDelay) seen.Dequeue();
        Vector3 p = seen.Peek().p; p.y = 0;

        Quaternion rot = body ? body.rotation : transform.rotation;
        Vector3 me = MyPos; me.y = 0;
        Vector3 fwd = rot * Vector3.down; fwd.y = 0; fwd.Normalize();
        Vector3 up = rot * Vector3.forward; // robot's spin axis (world up)

        // Aim point inside the attack goal, wobbling with difficulty.
        noise = Mathf.PerlinNoise(now * 0.3f, 7f) * 2f - 1f;
        Vector3 aim = attackGoal + new Vector3(0, 0, noise * aimError); aim.y = 0;
        Vector3 wanted = (aim - p).normalized;

        // Pick the shot direction closest to "at the goal" that leaves room behind the puck for the
        // robot (a puck on the boards can only be played along them or knocked loose).
        Vector3 shotDir = wanted; bool room = false;
        for (int i = 0; i < Offsets.Length && !room; i++)
        {
            var d = Quaternion.Euler(0, Offsets[i], 0) * wanted;
            if (Clear(p, -d, Behind + RobotBack)) { shotDir = d; room = true; }
        }
        Vector3 behind = p - shotDir * Behind;

        bool wrongSide = Vector3.Dot(me - p, shotDir) > -20f;   // robot is between the puck and where it should go
        bool lined = !wrongSide && Vector3.Distance(me, behind) < 140f && Vector3.Angle(fwd, shotDir) < 35f;

        Vector3 target;
        Vector3 og = ownGoal; og.y = 0;
        Vector3 toOwn = (og - p).normalized;
        bool ramHome = Vector3.Dot((p - me).normalized, toOwn) > 0.2f;   // ramming would send it toward our net
        if (!room && !ramHome)
        {
            // Pinned puck: ram it to knock it loose.
            target = p + (p - me).normalized * 150f;
            lined = true;
        }
        else if (!room)
        {
            // Pinned puck and we're on the wrong side of it: get between it and our net first.
            Vector3 side = Vector3.Cross(Vector3.up, toOwn);
            float s = Mathf.Sign(Vector3.Dot(me - p, side)); if (s == 0) s = 1;
            target = Vector3.Distance(me, p) < 220f ? p + side * s * 200f + toOwn * 60f : p + toOwn * 200f;
            lined = false; wrongSide = true;
        }
        else if (lined) target = p + shotDir * 200f;             // charge through the puck
        else if (wrongSide)
        {
            // Swing around the puck on whichever side the robot already is.
            Vector3 side = Vector3.Cross(Vector3.up, shotDir);
            float s = Mathf.Sign(Vector3.Dot(me - p, side)); if (s == 0) s = 1;
            target = p - shotDir * 140f + side * s * 170f;
            if (!Clear(p, target - p, (target - p).magnitude + RobotBack)) target = p - shotDir * 140f - side * s * 170f;
        }
        else if (Vector3.Distance(me, behind) < 150f) target = p;   // at the setup spot: turn to face the puck
        else target = behind;
        // Defend: puck sliding toward our net and we're not between them -> race back to the goal mouth.
        Vector3 pv = (p - lastSeen) / Mathf.Max(Time.fixedDeltaTime, 1e-4f); lastSeen = p;
        Vector3 outward = (og - Rink.Center).normalized;   // from rink center toward our net
        bool goalSide = (me - og).magnitude < (p - og).magnitude - 40f;
        bool defend = Vector3.Dot(pv, outward) > 350f && !goalSide;
        bool sprint = defend;   // racing the puck back to our net: every difficulty hustles
        if (defend) { target = og - outward * 200f + Vector3.forward * Mathf.Clamp(p.z - og.z, -180f, 180f); lined = false; }

        // Team roles (3x3). A defender or supporter still plays the puck when it comes close.
        float puckDist = Vector3.Distance(me, p);
        if (role == Role.Defend && !(puckDist < 260f && goalSide))
        {
            Vector3 toPuck = p - og; float d = toPuck.magnitude;
            target = og + toPuck / Mathf.Max(d, 1f) * Mathf.Clamp(d * 0.4f, 220f, 650f);
            if (Vector3.Distance(me, target) < 80f) target = p;   // in the slot: face the puck
            lined = false; defend = true;
        }
        else if (role == Role.Support && puckDist > 300f)
        {
            Vector3 ag = attackGoal; ag.y = 0;
            target = new Vector3(Mathf.Lerp(p.x, ag.x, 0.45f), 0, Rink.Center.z + (Rink.Center.z - p.z) * 0.6f);
            if (Vector3.Distance(me, target) < 80f) target = p;
            lined = false;
        }

        // Never loiter in our own net: drive back out in front of it.
        bool inNet = Vector3.Dot(me - og, outward) > -40f;
        if (inNet) { target = og - outward * 260f; lined = false; }

        Mode = inNet ? "exit" : role == Role.Support && puckDist > 300f ? "support" : defend ? "defend" : !room ? (ramHome ? "goal-side" : "ram") : lined ? "charge" : wrongSide ? "around" : "behind";

        target = AvoidObstacles(me, target);

        target.x = Mathf.Clamp(target.x, Rink.MinX + 60f, Rink.MaxX - 60f);
        target.z = Mathf.Clamp(target.z, Rink.MinZ + 60f, Rink.MaxZ - 60f);
        Target = target;

        Vector3 to = target - me;
        float angle = Vector3.SignedAngle(fwd, to, up);
        float turn = Mathf.Clamp(angle / 25f, -1f, 1f);
        float drive = Mathf.Abs(angle) < 50f ? 1f : Mathf.Abs(angle) < 100f ? 0.45f : 0.15f;
        if (to.magnitude < 60f && !lined) drive *= 0.4f;
        // Turning circle at full speed is ~vel/turnRate; slow down when the target is inside it.
        float turnRadius = mostradado.speed * speedFraction * 0.02f / (Mathf.Max(1, mostradado.rotaao) * Mathf.Deg2Rad);   // rotaao = deg per 50 Hz step
        if (Mathf.Abs(angle) > 30f && to.magnitude < turnRadius * 1.6f) drive *= 0.35f;

        // Unstick: pinned against a wall or the other robot.
        float moved = (me - lastPos).magnitude / Mathf.Max(Time.fixedDeltaTime, 1e-4f);
        lastPos = me;
        stuckTime = (drive > 0.4f && moved < 60f) ? stuckTime + Time.fixedDeltaTime : 0f;
        if (stuckTime > 0.8f) { reverseUntil = now + 0.5f; stuckTime = 0; }
        if (now < reverseUntil) { drive = -1f; turn = 0.6f; }

        Drive = drive * (sprint && role != Role.Defend ? Mathf.Max(speedFraction, 0.9f) : speedFraction);
        Turn = turn;
    }
}
