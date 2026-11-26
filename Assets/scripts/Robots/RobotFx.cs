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
    // Firm suspension: stiff, close to critically damped (zeta ~0.84: 37 / (2*sqrt(480))), so the body settles instead of
    // wobbling like jelly. Acceleration comes from the physics velocity on fixed steps (render-frame
    // position deltas spike whenever a frame has no physics step), low-passed over AccTau seconds.
    const float MaxLean = 6f, MaxHit = 12f, Spring = 480f, Damping = 37f, LeanPerAcc = 0.0013f, AccTau = 0.035f, VelTau = 0.09f, JoltDv = 200f;
    Rigidbody rb;
    Vector3 physVel, accSmooth, smoothVel; bool hasPhys;
    MaterialPropertyBlock mpb;

    public void Reset(float size)
    {
        hasLast = false; wheelRadius = 17f * size; pitch = roll = pitchVel = rollVel = 0f; lastVel = Vector3.zero;
        hasPhys = false; accSmooth = smoothVel = Vector3.zero;
    }

    bool Simulated => (rb || TryGetComponent(out rb)) && !rb.isKinematic;

    void FixedUpdate()
    {
        if (!Simulated) { hasPhys = false; return; }
        var v = rb.linearVelocity; v.y = 0;
        if (hasPhys)
        {
            var dv = v - physVel;
            var a = dv / Time.fixedDeltaTime;
            accSmooth = Vector3.Lerp(accSmooth, a, 1f - Mathf.Exp(-Time.fixedDeltaTime / AccTau));
            // A jolt (crash, hard stop): a speed change no motor can make in one physics step kicks the body;
            // the damped spring settles it. Driving changes speed by well under JoltDv per step.
            Vector3 f = transform.rotation * Vector3.down; f.y = 0; f.Normalize();
            Vector3 r = transform.rotation * Vector3.right; r.y = 0; r.Normalize();
            float df = Vector3.Dot(dv, f), dl = Vector3.Dot(dv, r);
            if (Mathf.Abs(df) > JoltDv) pitchVel += Mathf.Clamp(-df * 0.3f, -240f, 240f);   // stopping dives the nose
            if (Mathf.Abs(dl) > JoltDv) rollVel += Mathf.Clamp(-dl * 0.3f, -240f, 240f);
        }
        physVel = v; hasPhys = true;
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
        // Host: smoothed physics acceleration. Online joiner (kinematic, follows snapshots): smoothed
        // velocity from the transform, so snapshot steps don't shake the body.
        Vector3 acc;
        if (Simulated && hasPhys) acc = accSmooth;
        else
        {
            var sv = Vector3.Lerp(smoothVel, v, 1f - Mathf.Exp(-dt / VelTau));
            var raw = (sv - smoothVel) / dt; smoothVel = sv;
            accSmooth = Vector3.Lerp(accSmooth, raw, 1f - Mathf.Exp(-dt / AccTau));
            acc = accSmooth;
        }
        lastVel = v;
        Vector3 right = transform.rotation * Vector3.right; right.y = 0; right.Normalize();
        float af = Vector3.Dot(acc, fwd), al = Vector3.Dot(acc, right);
        float pitchTarget = Mathf.Clamp(-af * LeanPerAcc, -MaxLean, MaxLean);
        float rollTarget = Mathf.Clamp(-al * LeanPerAcc, -MaxLean, MaxLean);
        // Semi-implicit spring step, sub-stepped so a slow frame can't make it overshoot.
        int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.008f), 1, 8); float h = dt / steps;
        for (int i = 0; i < steps; i++)
        {
            pitchVel += (Spring * (pitchTarget - pitch) - Damping * pitchVel) * h; pitch += pitchVel * h;
            rollVel += (Spring * (rollTarget - roll) - Damping * rollVel) * h; roll += rollVel * h;
        }
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
            float k = 0.6f + Mathf.Sin(Time.time * 5f) * 0.45f + 0.45f;   // 0.6 .. 1.5 x the pad's own glow
            foreach (var r in magnets)
            {
                if (!r) continue;
                var m = r.sharedMaterial;
                var baseGlow = m && m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : new Color(0.4f, 0.8f, 2.5f);
                mpb.SetColor("_EmissionColor", baseGlow * k);
                r.SetPropertyBlock(mpb);
            }
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
