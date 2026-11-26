using UnityEngine;

// Tank-style robot drive with momentum: the motor pulls the robot toward the wanted speed
// along its nose, sideways sliding is damped by tyre grip, and hits from other robots or
// the boards are kept (the robot is shoved and recovers instead of stopping dead).
public class moverp1 : MonoBehaviour
{
    private Rigidbody b;

    public string horizontal;
    public string vertical;
    public int gravit;
    private float rot;
    private float vel;

    public float Horizontal { get; private set; }
    public float Vertical { get; private set; }

    // Null = keyboard axes above. MatchManager sets this (AI opponents and teammates).
    public IRobotInput input;
    // Robots hold still during menus, countdowns and goal replays.
    public static bool Frozen;
    // 1 = normal grip; lower = the robot skids (the ice arena).
    public static float Grip = 1f;

    // Per-robot stats (RobotLoadout sets them from the chosen RobotSpec; 1 = standard chassis).
    [System.NonSerialized] public float speedMul = 1f, accelMul = 1f, turnMul = 1f, gripMul = 1f, sideGripMul = 1f, stunGrip = 0.2f;
    [System.NonSerialized] public float boostUntil;     // Booster tool
    public bool Boosting => Time.time < boostUntil;
    public bool Action { get; private set; }            // tool key this step

    // The handling setting is in degrees per 50 Hz step (the old menu's unit); scale by the
    // real step so turning speed doesn't depend on the physics rate.
    const float RefStep = 0.02f;

    // Motor: top speed in ~0.25 s. Coasting / braking and sideways grip are weaker,
    // so a robot that gets hit slides before it recovers.
    const float DriveAccel = 4f, CoastDecel = 2.2f, SideGrip = 2.5f;   // x top speed per second
    float spin;      // deg per step from impacts, decays
    float hitUntil;  // just hit by another robot: tyres lose grip, the robot slides

    static PhysicsMaterial hull;

    void Start()
    {
        b = GetComponent<Rigidbody>();
        b.linearDamping = 1f;   // same for every robot (the scene had 1 on red, 0 on blue)
        if (Physics.gravity.y != -gravit) Physics.gravity = new Vector3(0, -gravit, 0);   // global: set once, not every step
        // Grip is simulated above, so the hull itself is frictionless (floor friction would stop
        // a shoved robot dead) and a little bouncy against the other robots.
        if (hull == null) hull = new PhysicsMaterial("Robot hull") { dynamicFriction = 0f, staticFriction = 0f, bounciness = 0.3f, frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Average };
        foreach (var c in GetComponentsInChildren<Collider>(true)) if (!c.isTrigger) c.sharedMaterial = hull;
    }

    void FixedUpdate()
    {
        bool boost = Boosting;
        rot = mostradado.rotaao * turnMul;
        vel = mostradado.speed * speedMul * (boost ? 1.5f : 1f);
        float drive, turn;
        if (Frozen) { drive = 0; turn = 0; Action = false; }
        else if (input != null) { drive = input.Drive; turn = input.Turn; Action = input.Action; }
        else { drive = Input.GetAxis(horizontal); turn = Input.GetAxis(vertical); Action = false; }
        if (boost) drive = 1f;   // a boost is a straight charge
        Horizontal = drive; Vertical = turn;

        float dt = Time.fixedDeltaTime;
        spin *= Mathf.Pow(0.85f, dt / RefStep);
        // Turn through the Rigidbody (not transform.Rotate) so interpolation and collisions see it.
        b.MoveRotation(b.rotation * Quaternion.Euler(0, 0, (turn * rot + spin) * dt / RefStep));

        float grip = Mathf.Min(Mathf.Lerp(0.35f, 1f, Grip) * gripMul, 1.2f);   // ice keeps a third of the grip
        if (Time.time < hitUntil) grip *= stunGrip;
        Vector3 fwd = b.rotation * Vector3.down; fwd.y = 0; fwd.Normalize();   // nose = -up of the model
        Vector3 side = Vector3.Cross(Vector3.up, fwd);
        Vector3 v = b.linearVelocity;
        float vf = Vector3.Dot(v, fwd), vs = Vector3.Dot(v, side);

        float wanted = drive * vel;
        bool pushing = Mathf.Abs(drive) > 0.05f && (Mathf.Sign(wanted) == Mathf.Sign(vf) || Mathf.Abs(vf) < 1f);
        float accel = (pushing ? DriveAccel * accelMul * (boost ? 2f : 1f) : CoastDecel) * vel * grip;
        if (Frozen) accel = 20f * vel;   // countdown / kickoff: stand still
        vf = Mathf.MoveTowards(vf, wanted, accel * dt);
        vs = Mathf.MoveTowards(vs, 0f, SideGrip * sideGripMul * vel * grip * (Frozen ? 8f : 1f) * dt);

        b.linearVelocity = fwd * vf + side * vs + Vector3.up * v.y;
    }

    // Glancing robot-on-robot hits also twist the robot a little.
    void OnCollisionEnter(Collision c)
    {
        if (b == null || c.rigidbody == null || c.rigidbody.GetComponent<moverp1>() == null) return;
        Vector3 rel = c.relativeVelocity; rel.y = 0;
        if (rel.magnitude > 300f) hitUntil = Time.time + Mathf.Lerp(0.15f, 0.45f, Mathf.InverseLerp(300f, 1800f, rel.magnitude));
        Vector3 arm = c.GetContact(0).point - b.worldCenterOfMass; arm.y = 0;
        float torque = Vector3.Cross(arm.normalized, rel).y;   // signed, units/s
        spin = Mathf.Clamp(torque * 0.004f, -6f, 6f) * (Vector3.Dot(transform.forward, Vector3.up) >= 0 ? 1f : -1f);
    }
}
