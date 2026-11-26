using System.Collections.Generic;
using UnityEngine;

// Turns the standard chassis into a RobotSpec: stats on moverp1 / Rigidbody, body scale, the
// tool, and the detailing: team paint and materials, spinning wheels, LED strips, head and
// brake lights, slot number and name, and per-robot bodywork (all built from primitives in
// the chassis' local frame).
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
        Build(content, spec, tool);

        // Custom model: hide the standard chassis renderers (colliders stay: same footprint x size).
        foreach (var r in robot.GetComponentsInChildren<Renderer>(true))
            if (!r.transform.IsChildOf(root) && r.GetComponentInParent<Camera>() == null) r.enabled = spec.model == null;
        if (spec.model) Object.Instantiate(spec.model, content, false);
        else Detail(robot, content, spec, slot, red);
        var fxc = robot.GetComponent<RobotFx>() ?? robot.AddComponent<RobotFx>();
        fxc.tilt = tilt;

        // Tool colliders: same frictionless hull material as the chassis.
        Collider hull = null;
        foreach (var c in robot.GetComponentsInChildren<Collider>(true)) if (!c.transform.IsChildOf(root)) { hull = c; break; }
        if (hull)
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) c.sharedMaterial = hull.sharedMaterial;
    }

    static void Build(Transform root, RobotSpec spec, RobotTools tool)
    {
        var dark = Mat(new Color(0.16f, 0.17f, 0.19f), 0.7f, 0.55f);
        var steel = Mat(new Color(0.62f, 0.64f, 0.68f), 0.85f, 0.6f);
        var accent = Mat(spec.accent, 0.2f, 0.6f, spec.accent * 0.6f);

        // Accent stripe along the body top: tells robots apart at a glance.
        for (int s = -1; s <= 1; s += 2)
            Box(root, "Stripe", new Vector3(s * 0.31f, (PocketBack + Back) / 2f, Top + 0.04f), new Vector3(0.07f, Back - PocketBack - 0.06f, 0.03f), accent, false);

        switch (spec.tool)
        {
            case RobotTool.Claw:
                // Short inward fingers at the arm tips (the pocket narrows but the puck can still leave:
                // Robockey's "no more than 20% of the puck" spirit).
                for (int s = -1; s <= 1; s += 2)
                {
                    var f = Box(root, "Finger", new Vector3(s * (ArmInner - 0.07f), ArmTip + 0.08f, Bottom + 0.06f), new Vector3(0.16f, 0.08f, 0.1f), steel, false);   // visual: solid fingers deflected pucks unpredictably
                    f.localRotation = Quaternion.Euler(0, 0, s * 25f);
                }
                break;

            case RobotTool.Plow:
                // V blade across the front, wider than the robot; the center sits back so the puck stays in.
                for (int s = -1; s <= 1; s += 2)
                {
                    var b = Box(root, "Blade", new Vector3(s * 0.55f, ArmTip - 0.12f, 0f), new Vector3(1.2f, 0.07f, 0.3f), steel, true);
                    b.localRotation = Quaternion.Euler(0, 0, s * -14f);
                }
                Box(root, "Blade Brace", new Vector3(0, ArmTip + 0.05f, Top - 0.05f), new Vector3(1.4f, 0.06f, 0.06f), dark, false);
                break;

            case RobotTool.Kicker:
                tool.kickPlate = Box(root, "Kick Plate", new Vector3(0, PocketBack - 0.05f, 0f), new Vector3(0.9f, 0.08f, 0.26f), accent, false);
                Box(root, "Solenoid", new Vector3(0, PocketBack + 0.08f, 0.05f), new Vector3(0.22f, 0.2f, 0.18f), dark, false);
                break;

            case RobotTool.Booster:
                tool.flames = new List<Renderer>();
                for (int s = -1; s <= 1; s += 2)
                {
                    var n = Cyl(root, "Nozzle", new Vector3(s * 0.38f, Back + 0.06f, 0.06f), new Vector3(0.22f, 0.12f, 0.22f), dark);
                    var fl = Cyl(root, "Flame", new Vector3(s * 0.38f, Back + 0.2f, 0.06f), new Vector3(0.15f, 0.14f, 0.15f), Mat(new Color(1f, 0.55f, 0.1f), 0, 0.5f, new Color(4f, 1.6f, 0.2f)));
                    tool.flames.Add(fl.GetComponent<Renderer>()); fl.gameObject.SetActive(false);
                }
                break;

            case RobotTool.Magnets:
                var glow = Mat(new Color(0.3f, 0.5f, 1f), 0.6f, 0.5f, new Color(0.4f, 0.8f, 2.5f));
                foreach (var p in new[] { new Vector2(-0.6f, -0.4f), new Vector2(0.6f, -0.4f), new Vector2(-0.45f, 0.6f), new Vector2(0.45f, 0.6f) })
                    Cyl(root, "Magnet", new Vector3(p.x, p.y, Bottom + 0.035f), new Vector3(0.3f, 0.03f, 0.3f), glow);
                break;
        }
    }

    // ---- detailing ------------------------------------------------------------------------------

    static readonly string[] PaintParts = { "Body1 1", "Body1 4", "Body1 5", "Body1 9", "Body1 10" };
    static readonly string[] FrameParts = { "Body1", "Body1 8", "Cube" };
    static readonly string[] SteelParts = { "Body1 11", "Body1 12", "Body1 6", "Body1 7" };
    static readonly string[] WheelParts = { "Body1 2", "Body1 3", "Body1 6", "Body1 7" };   // tires + hubs

    static void Detail(GameObject robot, Transform root, RobotSpec spec, int slot, bool red)
    {
        var team = red ? new Color(0.78f, 0.07f, 0.08f) : new Color(0.07f, 0.24f, 0.82f);
        var led = red ? new Color(3.2f, 0.25f, 0.2f) : new Color(0.25f, 0.8f, 3.4f);
        var paint = Mat(team, 0.55f, 0.82f);
        var carbon = Mat(new Color(0.07f, 0.075f, 0.085f), 0.35f, 0.65f);
        var steel = Mat(new Color(0.70f, 0.72f, 0.76f), 0.9f, 0.62f);
        var tire = Tread();
        var parts = new Dictionary<string, Renderer>();
        var loadout = robot.transform.Find("Loadout");
        foreach (var r in robot.GetComponentsInChildren<MeshRenderer>(true))
            if (!r.transform.IsChildOf(loadout) && r.GetComponentInParent<Camera>() == null) parts[r.name] = r;
        void Paint(string[] names, Material m) { foreach (var n in names) if (parts.TryGetValue(n, out var r)) r.sharedMaterial = m; }
        Paint(PaintParts, paint); Paint(FrameParts, carbon); Paint(SteelParts, steel);
        if (parts.TryGetValue("Body1 2", out var t2)) t2.sharedMaterial = tire;
        if (parts.TryGetValue("Body1 3", out var t3)) t3.sharedMaterial = tire;

        var fx = robot.GetComponent<RobotFx>() ?? robot.AddComponent<RobotFx>();
        fx.Reset(spec.size);
        fx.wheels.Clear(); fx.brakeLights.Clear(); fx.magnets.Clear(); fx.flames.Clear(); fx.boost = null;

        // Wheels: visual copies under a pivot at each tire's center (the chassis colliders stay put).
        var tires = new List<Renderer>();
        foreach (var n in new[] { "Body1 2", "Body1 3" }) if (parts.TryGetValue(n, out var r)) tires.Add(r);
        foreach (var t in tires)
        {
            var pivot = new GameObject("Wheel Pivot").transform;
            pivot.SetParent(root, false);
            pivot.position = t.bounds.center; pivot.localRotation = Quaternion.identity;
            foreach (var n in WheelParts)
            {
                if (!parts.TryGetValue(n, out var src) || Vector3.Distance(src.bounds.center, t.bounds.center) > 20f * spec.size) continue;
                var copy = new GameObject(n + " (spin)", typeof(MeshFilter), typeof(MeshRenderer));
                copy.GetComponent<MeshFilter>().sharedMesh = src.GetComponent<MeshFilter>().sharedMesh;
                copy.GetComponent<MeshRenderer>().sharedMaterials = src.sharedMaterials;
                copy.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
                copy.transform.localScale = src.transform.lossyScale;   // parent is unscaled here; SetParent below keeps world size
                copy.transform.SetParent(pivot, true);
                src.enabled = false;
            }
            fx.wheels.Add(pivot);
        }

        // The rest of the chassis: visual copies under the tilting body (originals keep their colliders).
        foreach (var kv in parts)
        {
            var src = kv.Value;
            if (!src.enabled) continue;                 // wheels already copied
            src.enabled = false;
            if (kv.Key == "Cube") continue;             // the small square block on top: hidden
            var copy = new GameObject(kv.Key + " (body)", typeof(MeshFilter), typeof(MeshRenderer));
            copy.GetComponent<MeshFilter>().sharedMesh = src.GetComponent<MeshFilter>().sharedMesh;
            copy.GetComponent<MeshRenderer>().sharedMaterials = src.sharedMaterials;
            copy.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
            copy.transform.localScale = src.transform.lossyScale;
            copy.transform.SetParent(root, true);
        }

        // LED strips along the outside of both arms, headlights on the body front, brake lights at the back.
        var ledMat = Mat(team * 0.5f, 0f, 0.8f, led);
        for (int s = -1; s <= 1; s += 2)
        {
            Box(root, "LED Strip", new Vector3(s * 0.925f, -0.15f, -0.02f), new Vector3(0.02f, 1.2f, 0.035f), ledMat, false);
            Box(root, "Headlight", new Vector3(s * 0.28f, PocketBack - 0.012f, 0.12f), new Vector3(0.16f, 0.02f, 0.06f), Mat(Color.white, 0f, 0.9f, new Color(3f, 3f, 2.7f)), false);
            var b = Box(root, "Brake Light", new Vector3(s * 0.34f, Back + 0.012f, 0.1f), new Vector3(0.2f, 0.02f, 0.05f), BrakeOff, false);
            fx.brakeLights.Add(b.GetComponent<Renderer>());
        }
        fx.brakeOn = BrakeOn; fx.brakeOff = BrakeOff;


        BodyKit(root, spec, team, steel, carbon, fx);

        // Tool visuals that animate.
        foreach (var f in root.GetComponentsInChildren<Renderer>(true)) if (f.name == "Flame") fx.flames.Add(f);
        foreach (var f in root.GetComponentsInChildren<Renderer>(true)) if (f.name == "Magnet") fx.magnets.Add(f);
        if (spec.tool == RobotTool.Booster) fx.boost = BoostParticles(root);
    }

    static void BodyKit(Transform root, RobotSpec spec, Color team, Material steel, Material carbon, RobotFx fx)
    {
        var accent = Mat(spec.accent, 0.3f, 0.7f, spec.accent * 0.35f);
        switch (spec.body)
        {
            case RobotBodyKit.Racer:   // Faísca: tail fin, rear wing on struts, lightning accents
                Box(root, "Fin", new Vector3(0, 0.70f, Top + 0.12f), new Vector3(0.04f, 0.32f, 0.22f), accent, false);
                for (int s = -1; s <= 1; s += 2) Box(root, "Spoiler Strut", new Vector3(s * 0.42f, 0.86f, Top + 0.1f), new Vector3(0.04f, 0.05f, 0.2f), carbon, false);
                Box(root, "Spoiler", new Vector3(0, 0.88f, Top + 0.21f), new Vector3(1.3f, 0.16f, 0.03f), carbon, false).localRotation = Quaternion.Euler(-8f, 0, 0);
                for (int s = -1; s <= 1; s += 2)
                {
                    Box(root, "Bolt Accent", new Vector3(s * 0.76f, -0.35f, 0.07f), new Vector3(0.05f, 0.3f, 0.02f), accent, false).localRotation = Quaternion.Euler(0, 0, s * 25f);
                    Box(root, "Bolt Accent", new Vector3(s * 0.76f, -0.6f, 0.04f), new Vector3(0.05f, 0.25f, 0.02f), accent, false).localRotation = Quaternion.Euler(0, 0, -s * 25f);
                }
                break;

            case RobotBodyKit.Armor:   // Trator: side armor with bolts, roll bar
                for (int s = -1; s <= 1; s += 2)
                {
                    Box(root, "Armor Plate", new Vector3(s * 0.955f, -0.15f, 0.0f), new Vector3(0.06f, 1.25f, 0.22f), carbon, false);
                    for (int i = 0; i < 5; i++)
                        Cyl(root, "Armor Bolt", new Vector3(s * 0.99f, -0.7f + i * 0.28f, 0.03f), new Vector3(0.05f, 0.012f, 0.05f), steel).localRotation = Quaternion.Euler(0, 0, 90);
                    Box(root, "Roll Bar Post", new Vector3(s * 0.45f, 0.62f, Top + 0.13f), new Vector3(0.05f, 0.05f, 0.26f), steel, false);
                }
                Box(root, "Roll Bar", new Vector3(0, 0.62f, Top + 0.26f), new Vector3(0.95f, 0.05f, 0.05f), steel, false);
                break;

            case RobotBodyKit.Cannon:  // Canhão: barrel along the spine with a muzzle ring
                Cyl(root, "Barrel", new Vector3(0, 0.5f, Top + 0.1f), new Vector3(0.15f, 0.3f, 0.15f), carbon);
                Cyl(root, "Muzzle", new Vector3(0, 0.2f, Top + 0.1f), new Vector3(0.21f, 0.03f, 0.21f), steel);
                Cyl(root, "Barrel Band", new Vector3(0, 0.62f, Top + 0.1f), new Vector3(0.19f, 0.02f, 0.19f), accent);
                for (int s = -1; s <= 1; s += 2) Cyl(root, "Kick Piston", new Vector3(s * 0.25f, PocketBack + 0.12f, 0.02f), new Vector3(0.05f, 0.1f, 0.05f), steel);
                break;

            case RobotBodyKit.Coils:   // Ímã: copper coils on top
                var copper = Mat(new Color(0.85f, 0.45f, 0.2f), 0.9f, 0.7f);
                for (int s = -1; s <= 1; s += 2)
                    for (int i = 0; i < 4; i++)
                        Cyl(root, "Coil", new Vector3(s * 0.38f, 0.64f, Top + 0.03f + i * 0.045f), new Vector3(0.3f - (i % 2) * 0.04f, 0.02f, 0.3f - (i % 2) * 0.04f), i % 2 == 0 ? copper : carbon).localRotation = Quaternion.Euler(90, 0, 0);
                break;

            case RobotBodyKit.Claw:    // Ômega: piston arms driving the claw tips
                for (int s = -1; s <= 1; s += 2)
                {
                    Cyl(root, "Claw Piston", new Vector3(s * 0.72f, -0.45f, 0.12f), new Vector3(0.06f, 0.22f, 0.06f), steel);
                    Box(root, "Claw Tip", new Vector3(s * (ArmInner + 0.02f), ArmTip + 0.02f, Bottom + 0.08f), new Vector3(0.12f, 0.1f, 0.12f), steel, false).localRotation = Quaternion.Euler(0, 0, s * 30f);
                }
                break;
        }
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

    static void Text(Transform root, string name, string text, Vector3 pos, float charSize, Color color)
    {
        var go = new GameObject(name); go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        // Lie flat on the top (normal = chassis up), letters' up = chassis nose (-Y): upright from behind.
        go.transform.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.down);
        var tm = go.AddComponent<TextMesh>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.font = font; go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        tm.fontSize = 64; tm.characterSize = charSize; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold; tm.color = color; tm.text = text;
    }

    static Transform colliderRoot;
    static Material brakeOn, brakeOff, tread;
    static Material BrakeOn => brakeOn ? brakeOn : brakeOn = Mat(new Color(1f, 0.1f, 0.08f), 0f, 0.8f, new Color(4f, 0.3f, 0.2f));
    static Material BrakeOff => brakeOff ? brakeOff : brakeOff = Mat(new Color(0.35f, 0.04f, 0.04f), 0f, 0.8f);

    static Material Tread()
    {
        if (tread) return tread;
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Tread" };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                bool groove = (y % 8) < 2 || ((x + y / 2) % 16) < 2;
                float g = groove ? 0.04f : 0.11f + Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.03f;
                px[y * N + x] = new Color(g, g, g * 1.05f);
            }
        tex.SetPixels(px); tex.Apply(true);
        tread = Mat(Color.white, 0f, 0.25f);
        tread = new Material(tread) { name = "Tire" };
        tread.SetTexture("_BaseMap", tex); tread.SetTexture("_MainTex", tex); tread.SetTextureScale("_BaseMap", new Vector2(3, 3));
        return tread;
    }

    static Transform Box(Transform root, string name, Vector3 pos, Vector3 size, Material mat, bool collider)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(root, false);
        g.transform.localPosition = pos; g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        if (collider && colliderRoot)
        {
            // Solid parts collide from the flat chassis frame so the tilt never moves a collider.
            var c = new GameObject(name + " Collider", typeof(BoxCollider)).transform;
            c.SetParent(colliderRoot, false);
            c.localPosition = pos; c.localScale = size;
            g.AddComponent<ColliderFollower>().Init(c);   // keeps rotations set after creation in sync
        }
        return g.transform;
    }

    // Cylinder primitive's axis is its local Y; lay it along the chassis' up (Z) unless it's a nozzle.
    static Transform Cyl(Transform root, string name, Vector3 pos, Vector3 size, Material mat)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(root, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = name == "Magnet" ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        return g.transform;
    }

    static readonly Dictionary<string, Material> mats = new();
    static Material Mat(Color c, float metal, float smooth, Color emission = default)
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
