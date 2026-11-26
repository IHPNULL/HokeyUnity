using System.Collections.Generic;
using UnityEngine;

// Per-robot animation and effects: spinning wheels, brake lights, pulsing magnets, booster
// flames, skid spray. Motion is read from the transform, so it works the same on an online
// joiner's screen (where robots follow the host's snapshots) as on the host.
public class RobotFx : MonoBehaviour
{
    [System.NonSerialized] public List<Transform> wheels = new List<Transform>();
    [System.NonSerialized] public List<Renderer> brakeLights = new List<Renderer>();
    [System.NonSerialized] public List<Renderer> magnets = new List<Renderer>();
    [System.NonSerialized] public List<Renderer> flames = new List<Renderer>();
    [System.NonSerialized] public ParticleSystem boost;
    [System.NonSerialized] public Transform tilt;       // visible body pivot (RobotLoadout)
    public Vector2 Tilt => new Vector2(pitch, roll);   // degrees: nose down +, lean (tests)
    [System.NonSerialized] public Material brakeOn, brakeOff;
    public float wheelRadius = 17f;     // world units at size 1
    public float WheelAngle { get; private set; }   // tests
    public bool Braking { get; private set; }

    Vector3 lastPos, lastVel; float lastFwd; bool hasLast; float sprayAt;
    float pitch, roll, pitchVel, rollVel;
    const float MaxLean = 9f, MaxHit = 14f, Spring = 140f, Damping = 13f;
    MaterialPropertyBlock mpb;

    public void Reset(float size)
    {
        hasLast = false; wheelRadius = 17f * size; pitch = roll = pitchVel = rollVel = 0f; lastVel = Vector3.zero;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime; if (dt <= 0f) return;
        var pos = transform.position;
        if (!hasLast) { lastPos = pos; hasLast = true; return; }
        Vector3 v = (pos - lastPos) / dt; v.y = 0; lastPos = pos;
        Vector3 fwd = transform.rotation * Vector3.down; fwd.y = 0; fwd.Normalize();
        float fs = Vector3.Dot(v, fwd);
        Vector3 side = v - fwd * fs;

        // Body lean: squat when accelerating, dive when braking, lean out of turns, rock on hits.
        Vector3 acc = (v - lastVel) / dt; lastVel = v;
        Vector3 right = transform.rotation * Vector3.right; right.y = 0; right.Normalize();
        float af = Vector3.Dot(acc, fwd), al = Vector3.Dot(acc, right);
        float lim = acc.magnitude > 20000f ? MaxHit : MaxLean;   // a crash rocks it further
        float pitchTarget = Mathf.Clamp(-af * 0.0012f, -lim, lim);
        float rollTarget = Mathf.Clamp(-al * 0.0012f, -lim, lim);
        if (acc.magnitude > 20000f)   // a jolt (crash, hard stop): kick the body, the spring rocks it back
        {
            pitchVel += Mathf.Clamp(-af * 0.004f, -260f, 260f);
            rollVel += Mathf.Clamp(-al * 0.004f, -260f, 260f);
        }
        pitchVel += (Spring * (pitchTarget - pitch) - Damping * pitchVel) * dt; pitch += pitchVel * dt;
        rollVel += (Spring * (rollTarget - roll) - Damping * rollVel) * dt; roll += rollVel * dt;
        pitch = Mathf.Clamp(pitch, -MaxHit, MaxHit); roll = Mathf.Clamp(roll, -MaxHit, MaxHit);
        if (tilt) tilt.localRotation = Quaternion.Euler(pitch, roll, 0f);

        // Wheels: roll with forward speed (axle = chassis X).
        float deg = fs * dt / Mathf.Max(1f, wheelRadius) * Mathf.Rad2Deg;
        WheelAngle += deg;
        foreach (var w in wheels) if (w) w.localRotation = Quaternion.Euler(-WheelAngle, 0, 0);

        // Brake lights: slowing down hard or reversing.
        float decel = (Mathf.Abs(lastFwd) - Mathf.Abs(fs)) / dt;
        bool braking = decel > 900f || fs < -60f;
        if (braking != Braking && brakeOn && brakeOff)
            foreach (var r in brakeLights) if (r) r.sharedMaterial = braking ? brakeOn : brakeOff;
        Braking = braking;
        lastFwd = fs;

        // Magnets pulse.
        if (magnets.Count > 0)
        {
            mpb ??= new MaterialPropertyBlock();
            float k = 1.2f + Mathf.Sin(Time.time * 5f) * 0.9f;
            mpb.SetColor("_EmissionColor", new Color(0.4f, 0.8f, 2.5f) * k);
            foreach (var r in magnets) if (r) r.SetPropertyBlock(mpb);
        }

        // Booster flames (the flame meshes are switched by RobotTools or by the host's snapshot).
        bool boosting = false; foreach (var f in flames) if (f && f.gameObject.activeSelf) boosting = true;
        if (boost)
        {
            var em = boost.emission;
            if (em.enabled != boosting) { em.enabled = boosting; if (boosting) boost.Play(); }
        }

        // Skid spray: sliding sideways or braking hard, colored by the floor.
        if (Time.time >= sprayAt && (side.magnitude > 300f || decel > 2200f) && wheels.Count > 0)
        {
            sprayAt = Time.time + 0.06f;
            var c = ArenaTheme.Current == 1 ? new Color(0.9f, 0.96f, 1f, 0.7f) : ArenaTheme.Current == 2 ? new Color(0.55f, 0.52f, 0.5f, 0.5f) : new Color(0.75f, 0.62f, 0.45f, 0.45f);
            Vector3 away = side.sqrMagnitude > 1f ? -side : -fwd * Mathf.Sign(fs);
            foreach (var w in wheels) if (w) FxPool.Spray(new Vector3(w.position.x, 15f, w.position.z), away, c, 2);
        }
    }
}
