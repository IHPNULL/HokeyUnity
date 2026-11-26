using System.Collections.Generic;
using UnityEngine;

// Turns the standard chassis into a RobotSpec: stats on moverp1 / Rigidbody, body scale, the
// tool (built from primitives in the chassis' local frame) and an accent stripe.
// Re-applying is safe: the original scale and mass are remembered and the tool is rebuilt.
//
// Chassis local frame (the imported model): -Y = nose, +Z = up, X = side. The model is U-shaped:
// the side arms reach y = -0.9, the body front is at y = +0.40, leaving a puck pocket ~1.2 wide.
public static class RobotLoadout
{
    public const float PocketBack = 0.40f, ArmTip = -0.90f, ArmInner = 0.60f, Top = 0.26f, Bottom = -0.17f, Back = 0.87f;

    public static void Apply(GameObject robot, RobotSpec spec)
    {
        if (robot == null || spec == null) return;
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
        if (old) { old.gameObject.SetActive(false); Object.Destroy(old.gameObject); old.name = "Loadout (old)"; }
        var root = new GameObject("Loadout").transform;
        root.SetParent(robot.transform, false);
        Build(root, spec, tool);

        // Custom model: hide the standard chassis renderers (colliders stay: same footprint x size).
        foreach (var r in robot.GetComponentsInChildren<Renderer>(true))
            if (!r.transform.IsChildOf(root) && r.GetComponentInParent<Camera>() == null) r.enabled = spec.model == null;
        if (spec.model) Object.Instantiate(spec.model, root, false);

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
        Box(root, "Stripe", new Vector3(0, (PocketBack + Back) / 2f, Top + 0.012f), new Vector3(0.22f, Back - PocketBack - 0.06f, 0.02f), accent, false);

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

    static Transform Box(Transform root, string name, Vector3 pos, Vector3 size, Material mat, bool collider)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        if (!collider) Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(root, false);
        g.transform.localPosition = pos; g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = mat;
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
