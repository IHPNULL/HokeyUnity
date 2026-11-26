using System.Collections.Generic;
using UnityEngine;

// Turns the standard chassis into a RobotSpec: stats on moverp1 / Rigidbody, body scale, the
// tool, and the detailing: team paint and materials, spinning wheels, LED strips, head and
// brake lights and per-robot bodywork (faceted parts from RobotParts, in the chassis' local
// frame), plus the hitbox: a few boxes on the flat Loadout frame that hug the new body.
// Re-applying is safe: the original scale and mass are remembered and the tool is rebuilt.
//
// Chassis local frame (the imported model): -Y = nose, +Z = up, X = side. The model is U-shaped:
// the side arms reach y = -0.9, the body front is at y = +0.40, leaving a puck pocket ~1.2 wide.
public static class RobotLoadout
{
    public const float PocketBack = 0.40f, ArmTip = -0.90f, ArmInner = 0.60f, Top = 0.26f, Bottom = -0.17f, Back = 0.87f;

    public static void Apply(GameObject robot, RobotSpec spec, int slot = 0)
    {
        if (robot == null || spec == null) return;
        bool red = robot.name.StartsWith("unidadeverm");
        var tool = robot.GetComponent<RobotTools>() ?? robot.AddComponent<RobotTools>();
        var rb = robot.GetComponent<Rigidbody>();
        if (!tool.originalSet) { tool.originalScale = robot.transform.localScale; tool.originalSet = true; }

        robot.transform.localScale = tool.originalScale * spec.size;
        if (rb) rb.mass = spec.weight;

        var m = robot.GetComponent<moverp1>();
        if (m)
        {
            m.speedMul = spec.speed;
            // Strength drives the motor; extra weight costs acceleration (8 kg = standard).
            m.accelMul = spec.strength / Mathf.Sqrt(spec.weight / 8f);
            // Bigger bodies turn slower.
            m.turnMul = Mathf.Pow(spec.size, -0.6f);
            m.gripMul = spec.tool == RobotTool.Magnets ? 1.6f : 1f;
            m.stunGrip = spec.tool == RobotTool.Magnets ? 0.6f : 0.2f;
            m.sideGripMul = spec.tool == RobotTool.Plow ? 1.6f : spec.tool == RobotTool.Magnets ? 1.3f : 1f;
            m.boostUntil = 0f;
        }

        tool.Setup(spec);

        var old = robot.transform.Find("Loadout");
        // Destroy is deferred to the end of the frame: detach first, or Detail() below would treat the
        // old loadout's copies as chassis parts and copy them again (parts piled up every match).
        if (old) { old.gameObject.SetActive(false); old.SetParent(null, false); old.name = "Loadout (old)"; Object.Destroy(old.gameObject); }
        var root = new GameObject("Loadout").transform;
        root.SetParent(robot.transform, false);
        // Everything visible hangs under a tilt pivot at the robot's floor contact (RobotFx leans it
        // in turns, on braking and on hits); colliders stay on the flat, physical chassis.
        var tilt = new GameObject("Tilt").transform; tilt.SetParent(root, false); tilt.localPosition = new Vector3(0, 0, Bottom);
        var content = new GameObject("Body").transform; content.SetParent(tilt, false); content.localPosition = new Vector3(0, 0, -Bottom);
        colliderRoot = root;
        var pal = Palette(spec, red);
        Build(content, spec, tool, pal);

        // Custom model: hide the standard chassis renderers (colliders stay: same footprint x size).
        foreach (var r in robot.GetComponentsInChildren<Renderer>(true))
            if (!r.transform.IsChildOf(root) && r.GetComponentInParent<Camera>() == null) r.enabled = spec.model == null;
        if (spec.model) Object.Instantiate(spec.model, content, false);
        else Detail(robot, content, spec, slot, red, pal);
        var fxc = robot.GetComponent<RobotFx>() ?? robot.AddComponent<RobotFx>();
        fxc.tilt = tilt;

        // Hitbox: the imported chassis' convex mesh colliders are replaced by a few boxes that hug the
        // new body (on the flat Loadout frame, so they never tilt), with the same hull material.
        Collider hull = null;
        foreach (var c in robot.GetComponentsInChildren<Collider>(true))
            if (!c.transform.IsChildOf(root) && c.GetComponentInParent<Camera>() == null) { if (hull == null) hull = c; c.enabled = false; }
        Hitbox(root, spec);
        if (hull)
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) c.sharedMaterial = hull.sharedMaterial;
    }

    // ---- hitbox ---------------------------------------------------------------------------------

    // Compound of boxes in the chassis frame (scaled with the robot by spec.size): the rear body with
    // wheels and fenders, two boxes per arm around the open puck pocket (1.2 wide, back wall at
    // y = PocketBack), and one box hugging each kit's top feature. Bottoms sit at the old ride height.
    public const float HitBottom = RobotParts.G;
    static void Hitbox(Transform root, RobotSpec spec)
    {
        float g = HitBottom;
        float armOut = spec.body == RobotBodyKit.Armor ? 0.99f : RobotParts.ArmOut;
        float rear = spec.tool == RobotTool.Booster ? 0.965f : RobotParts.Rear;
        Solid(root, "Hull Collider", new Vector3(-RobotParts.FenderOut, PocketBack, g), new Vector3(RobotParts.FenderOut, rear, RobotParts.Deck));
        for (int s = -1; s <= 1; s += 2)
        {
            Solid(root, "Arm Collider", new Vector3(s * ArmInner, -0.93f, g), new Vector3(s * armOut, -0.60f, 0.02f));
            Solid(root, "Arm Collider", new Vector3(s * ArmInner, -0.60f, g), new Vector3(s * armOut, PocketBack, 0.105f));
        }
        // Fills the open pocket for everything except the puck (pillars and boards used to wedge in there).
        Solid(root, "Pocket Plug", new Vector3(-ArmInner, -0.93f, g), new Vector3(ArmInner, PocketBack, 0.02f)).AddComponent<PuckPassThrough>();
        switch (spec.body)
        {
            case RobotBodyKit.Claw: Solid(root, "Top Collider", new Vector3(-0.27f, 0.49f, RobotParts.Deck), new Vector3(0.27f, 0.87f, 0.34f)); break;
            case RobotBodyKit.Racer: Solid(root, "Top Collider", new Vector3(-0.69f, 0.74f, RobotParts.Deck), new Vector3(0.69f, 0.905f, 0.46f)); break;
            case RobotBodyKit.Armor: Solid(root, "Top Collider", new Vector3(-0.44f, 0.49f, RobotParts.Deck), new Vector3(0.44f, 0.82f, 0.485f)); break;
            case RobotBodyKit.Cannon: Solid(root, "Top Collider", new Vector3(-0.26f, 0.415f, RobotParts.Deck), new Vector3(0.26f, 0.89f, 0.43f)); break;
            case RobotBodyKit.Coils: Solid(root, "Top Collider", new Vector3(-0.54f, 0.52f, RobotParts.Deck), new Vector3(0.54f, 0.80f, 0.40f)); break;
        }
    }

    static GameObject Solid(Transform root, string name, Vector3 a, Vector3 b)
    {
        var go = new GameObject(name, typeof(BoxCollider));
        go.transform.SetParent(root, false);
        var c = go.GetComponent<BoxCollider>();
        c.center = (a + b) / 2; c.size = new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), Mathf.Abs(b.z - a.z));
        return go;
    }

    // ---- materials ------------------------------------------------------------------------------

    static RobotParts.Palette Palette(RobotSpec spec, bool red)
    {
        var team = red ? new Color(0.78f, 0.06f, 0.08f) : new Color(0.06f, 0.24f, 0.95f);
        var led = red ? new Color(4.2f, 0.35f, 0.3f) : new Color(0.3f, 1.0f, 4.4f);
        var acc = spec.accent;
        return new RobotParts.Palette
        {
            paint = Mat(team, 0.55f, 0.82f),
            gun = Mat(new Color(0.30f, 0.31f, 0.35f), 0.4f, 0.6f),
            carbon = Mat(new Color(0.035f, 0.037f, 0.045f), 0.4f, 0.72f),
            steel = Mat(new Color(0.70f, 0.72f, 0.76f), 0.92f, 0.68f),
            glass = Mat(new Color(0.02f, 0.025f, 0.035f), 0.3f, 0.95f),
            led = Mat(team * 0.5f, 0f, 0.8f, led),
            glow = Mat(acc, 0f, 0.8f, acc * 2.0f),
            accent = Mat(acc, 0.2f, 0.7f, acc * 0.25f),
            rubber = Mat(new Color(0.045f, 0.045f, 0.05f), 0f, 0.35f),
            white = Mat(Color.white, 0f, 0.9f, new Color(3.2f, 3.2f, 3.0f)),
            copper = Mat(new Color(0.85f, 0.45f, 0.2f), 0.9f, 0.7f),
            flame = Mat(new Color(1f, 0.55f, 0.1f), 0, 0.5f, new Color(4f, 1.6f, 0.2f)),
            magnet = Mat(new Color(0.3f, 0.5f, 1f), 0.6f, 0.5f, new Color(0.4f, 0.8f, 2.5f)),
        };
    }

    static void Build(Transform root, RobotSpec spec, RobotTools tool, RobotParts.Palette p)
    {
        switch (spec.tool)
        {
            case RobotTool.Claw:
                // Lit jaw plates on the inner faces of the arm tips (visual only: solid fingers deflected
                // pucks unpredictably; the pocket stays 1.2 wide).
                RobotParts.Fingers(root, p);
                break;

            case RobotTool.Plow:
                // V blade across the front, wider than the robot; the center sits back so the puck stays in.
                for (int s = -1; s <= 1; s += 2)
                    Solid(root, "Blade", new Vector3(s * 0.55f, ArmTip - 0.12f, 0f), new Vector3(1.2f, 0.07f, 0.3f), Quaternion.Euler(0, 0, s * -14f), RobotParts.Blade, p.gun, p.glow, p.glow);
                RobotParts.Part(root, "Blade Brace", RobotParts.BladeBrace, Vector3.zero, p.gun);
                break;

            case RobotTool.Kicker:
                tool.kickPlate = RobotParts.Part(root, "Kick Plate", RobotParts.KickPlate, new Vector3(0, PocketBack - 0.015f, -0.065f), p.gun, p.glow);
                break;

            case RobotTool.Booster:
                tool.flames = new List<Renderer>();
                for (int s = -1; s <= 1; s += 2)
                {
                    RobotParts.Part(root, "Nozzle", RobotParts.Nozzle, new Vector3(s * 0.36f, 0.885f, 0.03f), p.gun, p.glow);
                    var fl = RobotParts.Part(root, "Flame", RobotParts.Flame, new Vector3(s * 0.36f, 0.955f, 0.03f), p.flame);
                    tool.flames.Add(fl.GetComponent<Renderer>()); fl.gameObject.SetActive(false);
                }
                break;

            case RobotTool.Magnets:
                // Pulsing pads on the arm tops (RobotFx pulses every renderer named "Magnet").
                for (int s = -1; s <= 1; s += 2)
                    foreach (var y in new[] { -0.42f, -0.12f })
                        RobotParts.Part(root, "Magnet", RobotParts.MagnetPad, new Vector3(s * RobotParts.ArmMid, y, RobotParts.ArmTop(y) + 0.001f), p.magnet);
                break;
        }
    }

    // Solid tool part: faceted visual on the tilting body, box collider on the flat chassis frame.
    static Transform Solid(Transform root, string name, Vector3 pos, Vector3 size, Quaternion rot, Mesh mesh, params Material[] mats)
    {
        var t = RobotParts.Part(root, name, mesh, pos, rot, mats);
        if (colliderRoot)
        {
            var c = new GameObject(name + " Collider", typeof(BoxCollider)).transform;
            c.SetParent(colliderRoot, false);
            c.localPosition = pos; c.localRotation = rot; c.localScale = size;
        }
        return t;
    }

    // ---- detailing ------------------------------------------------------------------------------

    static readonly string[] PaintParts = { "Body1 1", "Body1 4", "Body1 5", "Body1 9", "Body1 10" };
    static readonly string[] FrameParts = { "Body1", "Body1 8", "Cube" };
    static readonly string[] SteelParts = { "Body1 11", "Body1 12", "Body1 6", "Body1 7" };

    static void Detail(GameObject robot, Transform root, RobotSpec spec, int slot, bool red, RobotParts.Palette p)
    {
        // The imported chassis stays as the physical frame: keep its team paint (menus and tests read
        // it) but hide it; the faceted bodywork below replaces it.
        var parts = new Dictionary<string, Renderer>();
        var loadout = robot.transform.Find("Loadout");
        foreach (var r in robot.GetComponentsInChildren<MeshRenderer>(true))
            if (!r.transform.IsChildOf(loadout) && r.GetComponentInParent<Camera>() == null) parts[r.name] = r;
        void Paint(string[] names, Material m) { foreach (var n in names) if (parts.TryGetValue(n, out var r)) r.sharedMaterial = m; }
        Paint(PaintParts, p.paint); Paint(FrameParts, p.carbon); Paint(SteelParts, p.steel);
        Paint(new[] { "Body1 2", "Body1 3" }, p.rubber);
        foreach (var r in parts.Values) r.enabled = false;

        var fx = robot.GetComponent<RobotFx>() ?? robot.AddComponent<RobotFx>();
        fx.Reset(spec.size);
        fx.wheels.Clear(); fx.brakeLights.Clear(); fx.magnets.Clear(); fx.flames.Clear(); fx.boost = null;
        fx.brakeOn = RobotLoadoutMats.BrakeOn; fx.brakeOff = RobotLoadoutMats.BrakeOff;

        RobotParts.Chassis(root, spec, p, fx);

        // Tool visuals that animate.
        foreach (var f in root.GetComponentsInChildren<Renderer>(true)) if (f.name == "Flame") fx.flames.Add(f);
        foreach (var f in root.GetComponentsInChildren<Renderer>(true)) if (f.name == "Magnet") fx.magnets.Add(f);
        if (spec.tool == RobotTool.Booster) fx.boost = BoostParticles(root);
    }

    static ParticleSystem BoostParticles(Transform root)
    {
        var go = new GameObject("Boost Particles"); go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(0, Back + 0.25f, 0.06f);
        go.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);   // emit out of the back (+Y local)
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.25f; main.startSpeed = 500f; main.startSize = 30f; main.maxParticles = 200;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.2f), new Color(1f, 0.35f, 0.05f));
        var em = ps.emission; em.rateOverTime = 120f; em.enabled = false;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 12f; shape.radius = 0.12f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1f, 0.3f, 0.1f), 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        col.color = g;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.2f));
        var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = FxPool.Additive; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    static Transform colliderRoot;

    static readonly Dictionary<string, Material> mats = new();
    public static Material Mat(Color c, float metal, float smooth, Color emission = default)
    {
        string key = c + "|" + metal + "|" + smooth + "|" + emission;
        if (mats.TryGetValue(key, out var m) && m) return m;
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        m = new Material(sh) { name = "Robot part" };
        m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
        m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
        if (emission.maxColorComponent > 0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); }
        mats[key] = m;
        return m;
    }
}
