using System.Collections.Generic;
using UnityEngine;

// Neon-esports bodywork for the robots: faceted low-poly parts built with FacetMesh in the chassis'
// local frame (-Y = nose, +Z = up, X = side; see RobotLoadout). Dark gunmetal / carbon bodies with
// team paint on the upward faces, team-colored LED trim, and each robot's accent color on its kit.
// Meshes depend only on the shape, so each is built once and shared by every robot of both teams
// (the team look lives in the materials). Names that tests or RobotFx look for are kept.
public static class RobotParts
{
    // Floor contact: the bottom of the hull colliders (the chassis rides on it).
    public const float G = -0.177f;
    public const float HullHalf = 0.70f, ArmIn = 0.60f, ArmOut = 0.935f, FenderOut = 0.955f, Rear = 0.905f, Deck = 0.27f;
    public const float ArmRidge = 0.80f, ArmMid = 0.72f;   // arm top: flat from the inner edge to the ridge, bevel outside
    public const float WheelR = 0.17f, WheelY = 0.67f, WheelX = 0.835f;

    public class Palette { public Material paint, gun, carbon, steel, glass, led, glow, accent, rubber, white, copper, flame, magnet; }

    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    static Vector2 V2(float a, float b) => new Vector2(a, b);
    static readonly Quaternion Id = Quaternion.identity;

    // ---- mesh cache / part objects ---------------------------------------------------------------

    static readonly Dictionary<string, Mesh> cache = new();
    public static Mesh M(string key, System.Action<FacetMesh> build)
    {
        if (cache.TryGetValue(key, out var m) && m) return m;
        var f = new FacetMesh(); build(f);
        m = f.ToMesh("Robot " + key); cache[key] = m;
        return m;
    }

    public static Transform Part(Transform parent, string name, Mesh mesh, Vector3 pos, params Material[] mats) => Part(parent, name, mesh, pos, Id, mats);
    public static Transform Part(Transform parent, string name, Mesh mesh, Vector3 pos, Quaternion rot, params Material[] mats)
    {
        var g = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localRotation = rot;
        g.GetComponent<MeshFilter>().sharedMesh = mesh;
        g.GetComponent<MeshRenderer>().sharedMaterials = mats;
        return g.transform;
    }

    static int Up(Vector3 n) => n.z > 0.55f ? 1 : 0;   // submesh 1 = paint on the upward faces

    static Vector3[] Inset(Vector3[] r, float d)
    {
        var c = Vector3.zero; foreach (var q in r) c += q; c /= r.Length;
        var o = new Vector3[r.Length];
        for (int i = 0; i < r.Length; i++) { var to = c - r[i]; o[i] = r[i] + to.normalized * Mathf.Min(d, to.magnitude * 0.5f); }
        return o;
    }
    static Vector3[] RingYZ(float x, Vector2[] yz) { var r = new Vector3[yz.Length]; for (int i = 0; i < yz.Length; i++) r[i] = V(x, yz[i].x, yz[i].y); return r; }
    static Vector3[] RingXZ(float y, Vector2[] xz, float sx = 1) { var r = new Vector3[xz.Length]; for (int i = 0; i < xz.Length; i++) r[i] = V(xz[i].x * sx, y, xz[i].y); return r; }

    // Profile (y,z) extruded across X with chamfered side edges.
    static void SlabX(FacetMesh f, Vector2[] yz, float x0, float x1, float ch) =>
        f.Loft(Inset(RingYZ(x0, yz), ch), RingYZ(x0 + ch, yz), RingYZ(x1 - ch, yz), Inset(RingYZ(x1, yz), ch));

    // Arm top height along the arm (the wedge rises from the nose to the body).
    public static float ArmTop(float y) => y < -0.82f ? Mathf.Lerp(G + 0.085f, 0f, (y + 0.925f) / 0.105f)
        : y < -0.55f ? Mathf.Lerp(0f, 0.065f, (y + 0.82f) / 0.27f) : Mathf.Lerp(0.065f, 0.10f, (y + 0.55f) / 0.97f);

    // ---- common chassis ---------------------------------------------------------------------------

    public static void Chassis(Transform root, RobotSpec spec, Palette p, RobotFx fx)
    {
        var kit = spec.body;
        // Rear body: a chamfered wedge from the pocket's back wall (y = 0.40) to the tail.
        Part(root, "Hull", M("hull", f =>
        {
            f.PaintBy(Up);
            Vector3[] St(float y, float zs, float zt, float hw) => RingXZ(y, new[] { V2(-hw, G + 0.01f), V2(hw, G + 0.01f), V2(hw, zs), V2(hw - 0.10f, zt), V2(-hw + 0.10f, zt), V2(-hw, zs) });
            f.Loft(St(0.40f, 0.03f, 0.075f, HullHalf), St(0.52f, 0.11f, 0.19f, HullHalf), St(0.78f, 0.13f, 0.205f, HullHalf), St(0.885f, 0.05f, 0.12f, 0.66f));
            f.PaintBy(null);
            f.Sub(2);   // carbon: rear bumper, front skid lip, intake slots on the front wall
            f.CBox(V(0, 0.885f, -0.06f), V(1.15f, 0.03f, 0.1f), 0.01f);
            f.CBox(V(0, 0.415f, G + 0.03f), V(1.30f, 0.05f, 0.05f), 0.012f);
            for (int s = -1; s <= 1; s += 2) f.CBox(V(s * 0.5f, 0.397f, -0.07f), V(0.22f, 0.01f, 0.06f), 0.004f);
        }), Vector3.zero, p.gun, p.paint, p.carbon);

        // Side arms: low wedges reaching forward around the puck pocket.
        Part(root, "Arms", M("arms", f =>
        {
            f.PaintBy(Up);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3[] St(float y, float top, float xi, float xo) => RingXZ(y, new[] { V2(xi, G + 0.005f), V2(xo - 0.025f, G + 0.005f), V2(xo, G + 0.05f), V2(xo - 0.03f, top - 0.05f), V2(ArmRidge, top), V2(xi + 0.04f, top), V2(xi, top - 0.03f) }, s);
                f.Loft(St(-0.925f, G + 0.085f, 0.62f, 0.84f), St(-0.82f, 0f, ArmIn, 0.90f), St(-0.55f, 0.065f, ArmIn, 0.925f), St(0.42f, 0.10f, ArmIn, ArmOut));
            }
            f.PaintBy(null);
        }), Vector3.zero, p.gun, p.paint);

        // Fenders over the rear wheels.
        Part(root, "Fenders", M("fenders", f =>
        {
            f.PaintBy(Up);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3[] St(float y, float zb, float zt) => RingXZ(y, new[] { V2(0.69f, zb), V2(FenderOut, zb), V2(FenderOut, zt - 0.03f), V2(0.88f, zt), V2(0.69f, zt - 0.015f) }, s);
                f.Loft(St(0.44f, 0.02f, 0.06f), St(0.53f, 0.18f, 0.225f), St(0.80f, 0.18f, 0.225f), St(0.89f, 0.04f, 0.09f));
            }
            f.PaintBy(null);
        }), Vector3.zero, p.gun, p.paint);

        // Cockpit pod (each kit has its own proportions).
        if (kit != RobotBodyKit.Coils && kit != RobotBodyKit.Cannon)
        {
            float wb = kit == RobotBodyKit.Armor ? 0.31f : kit == RobotBodyKit.Racer ? 0.21f : 0.26f;
            float wt = kit == RobotBodyKit.Armor ? 0.23f : kit == RobotBodyKit.Racer ? 0.11f : 0.16f;
            float top = kit == RobotBodyKit.Armor ? 0.31f : kit == RobotBodyKit.Racer ? 0.265f : 0.30f;
            Part(root, "Canopy", M("canopy" + kit, f =>
            {
                Vector3[] St(float y, float zt, float k) => RingXZ(y, new[] { V2(-wb * k, 0.17f), V2(wb * k, 0.17f), V2(wb * k - 0.02f, zt - 0.05f), V2(wt * k, zt), V2(-wt * k, zt), V2(-wb * k + 0.02f, zt - 0.05f) });
                f.Loft(St(0.49f, 0.215f, 0.9f), St(0.60f, top, 1f), St(0.79f, top, 1f), St(0.87f, 0.22f, 0.9f));
                // Visor: a lit slit on the front slope.
                f.Sub(1);
                var a = V(0, 0.49f, 0.215f); var b = V(0, 0.60f, top); var nrm = Vector3.Cross(b - a, Vector3.right).normalized; if (nrm.y > 0) nrm = -nrm;
                Vector3 S(float t, float x) => Vector3.Lerp(a, b, t) + V(x, 0, 0) + nrm * 0.004f;
                float vw = wt * 0.95f;
                f.Flat(nrm, S(0.35f, -vw * 0.92f), S(0.35f, vw * 0.92f), S(0.75f, vw), S(0.75f, -vw));
            }), Vector3.zero, p.glass, p.led);
        }

        // Wheels: spinning pivots at the rear corners (RobotFx rolls them around local X).
        for (int s = -1; s <= 1; s += 2)
        {
            var pivot = new GameObject("Wheel Pivot").transform;
            pivot.SetParent(root, false); pivot.localPosition = V(s * WheelX, WheelY, G + WheelR); pivot.localRotation = Id;
            Part(pivot, "Wheel", Wheel(s), Vector3.zero, p.rubber, p.steel, p.carbon, p.led);
            fx.wheels.Add(pivot);
        }

        // Team LED: strips along the arms (the tests' "LED Strip"), deck and fender trim.
        for (int s = -1; s <= 1; s += 2)
            Part(root, "LED Strip", M("ledstrip" + s, f =>
            {
                f.Seg(V(s * 0.898f, -0.80f, G + 0.042f), V(s * 0.922f, -0.55f, G + 0.042f), V2(0.006f, 0.012f), V2(0.006f, 0.012f), Vector3.up);
                f.Seg(V(s * 0.922f, -0.55f, G + 0.042f), V(s * 0.932f, 0.42f, G + 0.042f), V2(0.006f, 0.012f), V2(0.006f, 0.012f), Vector3.up);
            }), Vector3.zero, p.led);
        Part(root, "LED Trim", M("ledtrim", f =>
        {
            for (int s = -1; s <= 1; s += 2)
            {
                f.Seg(V(s * 0.60f, 0.52f, 0.193f), V(s * 0.60f, 0.78f, 0.208f), V2(0.008f, 0.004f), V2(0.008f, 0.004f), Vector3.forward);
                f.Seg(V(s * 0.957f, 0.53f, 0.18f), V(s * 0.957f, 0.80f, 0.18f), V2(0.004f, 0.007f), V2(0.004f, 0.007f), Vector3.up);
                // Rear arm edge: light line along the inner top edge of each arm.
                f.Seg(V(s * 0.652f, -0.50f, ArmTop(-0.50f) + 0.003f), V(s * 0.652f, 0.38f, ArmTop(0.38f) + 0.003f), V2(0.006f, 0.004f), V2(0.006f, 0.004f), Vector3.forward);
            }
            // Light bar across the back.
            f.Seg(V(-0.55f, 0.887f, 0.085f), V(0.55f, 0.887f, 0.085f), V2(0.004f, 0.006f), V2(0.004f, 0.006f), Vector3.forward);
        }), Vector3.zero, p.led);

        // Robot-accent chevrons on the arm tops pointing at the nose.
        if (kit != RobotBodyKit.Armor)
            Part(root, "Chevrons", M("chevrons", f =>
            {
                for (int s = -1; s <= 1; s += 2)
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 0.08f + i * 0.1f, xc = s * ArmMid, w = 0.07f, d = 0.035f, th = 0.03f;
                        float z = ArmTop(y) + 0.004f;
                        f.Flat(Vector3.forward, V(xc - w, y + d, z), V(xc, y - d, z), V(xc, y - d + th, z), V(xc - w, y + d + th, z));
                        f.Flat(Vector3.forward, V(xc, y - d, z), V(xc + w, y + d, z), V(xc + w, y + d + th, z), V(xc, y - d + th, z));
                    }
            }), Vector3.zero, p.glow);

        // Head and brake lights.
        for (int s = -1; s <= 1; s += 2)
        {
            Part(root, "Headlight", M("headlight" + s, f =>
            {
                f.Flat(Vector3.down, V(s * 0.16f, 0.394f, 0.045f), V(s * 0.46f, 0.394f, 0.03f), V(s * 0.44f, 0.394f, 0.005f), V(s * 0.18f, 0.394f, 0.015f));
            }), Vector3.zero, p.white);
            var b = Part(root, "Brake Light", M("brake" + s, f =>
            {
                f.CBox(V(s * 0.40f, 0.888f, 0.03f), V(0.26f, 0.012f, 0.045f), 0.004f);
            }), Vector3.zero, RobotLoadoutMats.BrakeOff);
            fx.brakeLights.Add(b.GetComponent<Renderer>());
        }

        Kit(root, spec, p);
    }

    static Mesh Wheel(int s) => M("wheel" + s, f =>
    {
        var ax = V(s, 0, 0); const float w = 0.06f;
        // Tire with chamfered shoulders + tread blocks (rubber).
        f.Sub(0);
        f.Lathe(Vector3.zero, ax, new[] { V2(0, -w), V2(0.135f, -w), V2(WheelR - 0.006f, -w + 0.022f), V2(WheelR - 0.006f, w - 0.022f), V2(0.135f, w), V2(0, w) }, 12, 15f);
        for (int k = 0; k < 10; k++)
        {
            float a = k * 36f * Mathf.Deg2Rad; var dir = V(0, Mathf.Cos(a), Mathf.Sin(a));
            f.Seg(dir * 0.158f, dir * (WheelR + 0.004f), V2(0.026f, w - 0.012f), V2(0.02f, w - 0.02f), ax);
        }
        // Hub (steel) with dark recess and spokes.
        f.Sub(1);
        f.Lathe(ax * (w - 0.004f), ax, new[] { V2(0, 0), V2(0.11f, 0), V2(0.11f, 0.01f), V2(0.085f, 0.018f), V2(0, 0.018f) }, 10, 18f);
        f.Sub(2);
        f.Lathe(ax * (w + 0.012f), ax, new[] { V2(0, 0), V2(0.07f, 0), V2(0.04f, 0.012f), V2(0, 0.014f) }, 6, 30f);
        f.Sub(1);
        for (int k = 0; k < 5; k++)
        {
            float a = (k * 72f + 18f) * Mathf.Deg2Rad; var dir = V(0, Mathf.Cos(a), Mathf.Sin(a));
            f.Seg(ax * (w + 0.016f) + dir * 0.03f, ax * (w + 0.012f) + dir * 0.10f, V2(0.012f, 0.006f), V2(0.016f, 0.005f), ax);
        }
        // LED ring on the sidewall.
        f.Sub(3);
        const int N = 12;
        for (int k = 0; k < N; k++)
        {
            float a0 = k * Mathf.PI * 2 / N, a1 = (k + 1) * Mathf.PI * 2 / N;
            Vector3 D(float a) => V(0, Mathf.Cos(a), Mathf.Sin(a));
            var h = ax * (w + 0.002f);
            f.Flat(ax, h + D(a0) * 0.116f, h + D(a1) * 0.116f, h + D(a1) * 0.13f, h + D(a0) * 0.13f);
        }
    });

    // ---- per-robot bodywork -------------------------------------------------------------------------

    static void Kit(Transform root, RobotSpec spec, Palette p)
    {
        switch (spec.body)
        {
            case RobotBodyKit.Claw:    // Ômega: hydraulic claw arms, mandibles, sensor crown
                for (int s = -1; s <= 1; s += 2)
                {
                    float x = s * ArmMid;
                    Part(root, "Claw Piston", M("piston" + s, f =>
                    {
                        f.Sub(0);
                        f.Tube(V(x, 0.28f, 0.135f), V(x, -0.20f, 0.135f), 0.034f, 8, 22.5f);
                        f.CBox(V(x, 0.30f, 0.12f), V(0.09f, 0.06f, 0.07f), 0.012f);
                        f.CBox(V(x, -0.64f, 0.09f), V(0.08f, 0.06f, 0.09f), 0.012f);
                        f.Sub(1);
                        f.Tube(V(x, -0.20f, 0.135f), V(x, -0.62f, 0.135f), 0.017f, 8, 22.5f);
                        f.Sub(2);
                        f.Lathe(V(x, -0.17f, 0.135f), Vector3.down, new[] { V2(0.04f, 0), V2(0.04f, 0.03f) }, 8, 22.5f);
                        f.Lathe(V(x, 0.18f, 0.135f), Vector3.down, new[] { V2(0.037f, 0), V2(0.037f, 0.015f) }, 8, 22.5f);
                    }), Vector3.zero, p.gun, p.steel, p.glow);
                    Part(root, "Claw Tip", M("clawtip" + s, f =>
                    {
                        f.Sub(0);
                        f.Seg(V(s * 0.80f, -0.66f, 0.05f), V(s * 0.70f, -0.925f, -0.07f), V2(0.045f, 0.03f), V2(0.01f, 0.008f), Vector3.up);
                        f.Sub(1);
                        f.Seg(V(s * 0.80f, -0.66f, 0.083f), V(s * 0.71f, -0.90f, -0.04f), V2(0.008f, 0.004f), V2(0.003f, 0.003f), Vector3.up);
                    }), Vector3.zero, p.steel, p.glow);
                }
                Part(root, "Sensor", M("sensor", f =>
                {
                    f.Sub(0);
                    f.Lathe(V(0, 0.70f, 0.295f), Vector3.forward, new[] { V2(0, 0), V2(0.095f, 0), V2(0.08f, 0.03f), V2(0, 0.035f) }, 6, 30f);
                    f.Sub(1);
                    f.Lathe(V(0, 0.70f, 0.32f), Vector3.forward, new[] { V2(0, 0), V2(0.045f, 0), V2(0.03f, 0.02f), V2(0, 0.022f) }, 6, 30f);
                }), Vector3.zero, p.carbon, p.glow);
                break;

            case RobotBodyKit.Racer:   // Faísca: dorsal fin, rear wing, lightning bolts
                Part(root, "Fin", M("fin", f =>
                {
                    f.Sub(0);
                    Vector3[] St(float y, float zt, float t) => RingXZ(y, new[] { V2(-t, 0.25f), V2(t, 0.25f), V2(t * 0.4f, zt), V2(-t * 0.4f, zt) });
                    f.Loft(St(0.56f, 0.27f, 0.006f), St(0.80f, 0.43f, 0.016f), St(0.885f, 0.455f, 0.016f));
                    f.Sub(1);
                    f.Seg(V(0, 0.58f, 0.278f), V(0, 0.80f, 0.433f), V2(0.005f, 0.006f), V2(0.006f, 0.006f), Vector3.right);
                    f.Seg(V(0, 0.80f, 0.433f), V(0, 0.885f, 0.458f), V2(0.006f, 0.006f), V2(0.006f, 0.006f), Vector3.right);
                }), Vector3.zero, p.accent, p.glow);
                Part(root, "Spoiler", M("spoiler", f =>
                {
                    f.Sub(0);
                    SlabX(f, new[] { V2(0.745f, 0.425f), V2(0.77f, 0.443f), V2(0.895f, 0.452f), V2(0.90f, 0.44f), V2(0.86f, 0.427f), V2(0.78f, 0.415f) }, -0.66f, 0.66f, 0.008f);
                    f.Sub(2);
                    for (int s = -1; s <= 1; s += 2) f.CBox(V(s * 0.675f, 0.82f, 0.405f), V(0.02f, 0.17f, 0.12f), 0.006f);
                    f.Sub(1);
                    f.Seg(V(-0.64f, 0.898f, 0.447f), V(0.64f, 0.898f, 0.447f), V2(0.004f, 0.006f), V2(0.004f, 0.006f), Vector3.forward);
                }), Vector3.zero, p.carbon, p.glow, p.accent);
                Part(root, "Spoiler Strut", M("struts", f =>
                {
                    for (int s = -1; s <= 1; s += 2) f.Seg(V(s * 0.30f, 0.80f, 0.19f), V(s * 0.34f, 0.84f, 0.425f), V2(0.012f, 0.03f), V2(0.01f, 0.025f), Vector3.up);
                }), Vector3.zero, p.gun);
                Part(root, "Bolt Accent", M("bolts", f =>
                {
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float xc = s * ArmMid;
                        var pts = new[] { V2(xc + s * 0.05f, -0.60f), V2(xc - s * 0.04f, -0.40f), V2(xc + s * 0.03f, -0.36f), V2(xc - s * 0.05f, -0.12f) };
                        for (int i = 0; i < pts.Length - 1; i++)
                        {
                            Vector3 P(Vector2 q, float dx) => V(q.x + dx, q.y, ArmTop(q.y) + 0.004f);
                            float t0 = i == 0 ? 0.002f : 0.016f, t1 = i == pts.Length - 2 ? 0.002f : 0.016f;
                            f.Flat(Vector3.forward, P(pts[i], -t0), P(pts[i], t0), P(pts[i + 1], t1), P(pts[i + 1], -t1));
                        }
                    }
                }), Vector3.zero, p.glow);
                break;

            case RobotBodyKit.Armor:   // Trator: armored skirts with bolts, roll cage with beacons
                for (int s = -1; s <= 1; s += 2)
                    Part(root, "Armor Plate", M("armor" + s, f =>
                    {
                        f.PaintBy(n => n.z > 0.55f ? 1 : 0);
                        Vector3[] St(float y, float k) => RingXZ(y, new[] { V2(0.90f, G + 0.012f), V2(0.965f, G + 0.02f), V2(0.985f, 0.03f), V2(0.975f, 0.10f * k), V2(0.93f, 0.125f * k), V2(0.90f, 0.125f * k) }, s);
                        f.Loft(St(-0.84f, 0.3f), St(-0.74f, 1f), St(0.36f, 1f), St(0.44f, 0.6f));
                        f.PaintBy(null);
                        f.Sub(2);
                        for (int i = 0; i < 5; i++)
                            f.Lathe(V(s * 0.983f, -0.62f + i * 0.24f, 0.035f), V(s, 0, 0), new[] { V2(0, 0), V2(0.022f, 0), V2(0.016f, 0.012f), V2(0, 0.012f) }, 6, 0);
                        f.Sub(3);
                        for (int i = 0; i < 4; i++)
                        {
                            float y = -0.52f + i * 0.24f;
                            f.Flat(V(s, 0, 0.3f), V(s * 0.987f, y, -0.01f), V(s * 0.987f, y + 0.07f, -0.01f), V(s * 0.983f, y + 0.12f, 0.07f), V(s * 0.983f, y + 0.05f, 0.07f));
                        }
                    }), Vector3.zero, p.gun, p.accent, p.steel, p.glow);
                Part(root, "Roll Bar", M("rollbar", f =>
                {
                    f.Sub(0);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        f.Tube(V(s * 0.42f, 0.80f, 0.18f), V(s * 0.40f, 0.76f, 0.43f), 0.022f, 6);
                        f.Tube(V(s * 0.40f, 0.76f, 0.43f), V(s * 0.34f, 0.50f, 0.19f), 0.018f, 6);
                    }
                    f.Tube(V(-0.42f, 0.76f, 0.43f), V(0.42f, 0.76f, 0.43f), 0.022f, 6);
                    f.Sub(1);
                    for (int s = -1; s <= 1; s += 2) f.CBox(V(s * 0.2f, 0.76f, 0.462f), V(0.1f, 0.05f, 0.04f), 0.01f);
                }), Vector3.zero, p.steel, p.glow);
                Part(root, "Exhaust", M("exhaust", f =>
                {
                    for (int s = -1; s <= 1; s += 2) f.Tube(V(s * 0.55f, 0.80f, 0.19f), V(s * 0.55f, 0.80f, 0.34f), 0.028f, 6);
                }), Vector3.zero, p.carbon);
                break;

            case RobotBodyKit.Cannon:  // Canhão: turret with a long cannon along the spine
                Part(root, "Turret", M("turret", f =>
                {
                    f.PaintBy(Up);
                    f.Lathe(V(0, 0.70f, 0.17f), Vector3.forward, new[] { V2(0, 0), V2(0.27f, 0), V2(0.27f, 0.05f), V2(0.22f, 0.10f), V2(0, 0.10f) }, 6, 0);
                    f.PaintBy(null);
                    f.Sub(2);
                    f.Lathe(V(0, 0.70f, 0.222f), Vector3.forward, new[] { V2(0.246f, 0), V2(0.246f, 0.012f) }, 6, 0);
                }), Vector3.zero, p.gun, p.paint, p.led);
                Part(root, "Barrel", M("barrel", f =>
                {
                    f.Sub(0);
                    f.Lathe(V(0, 0.89f, 0.34f), Vector3.down, new[] { V2(0, 0), V2(0.12f, 0), V2(0.12f, 0.14f), V2(0.085f, 0.18f), V2(0.085f, 0.37f), V2(0.11f, 0.39f), V2(0.11f, 0.47f), V2(0.07f, 0.472f), V2(0, 0.472f) }, 8, 22.5f);
                    f.Sub(1);
                    f.Lathe(V(0, 0.89f, 0.34f), Vector3.down, new[] { V2(0.089f, 0.22f), V2(0.089f, 0.25f) }, 8, 22.5f);
                    f.Lathe(V(0, 0.89f, 0.34f), Vector3.down, new[] { V2(0.089f, 0.29f), V2(0.089f, 0.32f) }, 8, 22.5f);
                    // Glowing bore.
                    var c = V(0, 0.89f - 0.4725f, 0.34f);
                    var ring = new Vector3[8];
                    for (int k = 0; k < 8; k++) { float a = (22.5f + k * 45f) * Mathf.Deg2Rad; ring[k] = c + V(Mathf.Cos(a) * 0.06f, 0, Mathf.Sin(a) * 0.06f); }
                    f.Flat(Vector3.down, ring);
                }), Vector3.zero, p.gun, p.glow);
                break;

            case RobotBodyKit.Coils:   // Ímã: twin electromagnet coils around a reactor core
                Part(root, "Core", M("core", f =>
                {
                    f.Sub(0);
                    f.Lathe(V(0, 0.66f, 0.17f), Vector3.forward, new[] { V2(0, 0), V2(0.15f, 0), V2(0.15f, 0.05f), V2(0.11f, 0.09f), V2(0, 0.09f) }, 6, 30f);
                    f.Sub(1);
                    f.Lathe(V(0, 0.66f, 0.26f), Vector3.forward, new[] { V2(0, 0), V2(0.07f, 0), V2(0.05f, 0.03f), V2(0, 0.03f) }, 6, 30f);
                }), Vector3.zero, p.gun, p.glow);
                for (int s = -1; s <= 1; s += 2)
                {
                    var o = V(s * 0.40f, 0.66f, 0f);
                    Part(root, "Coil", M("coil" + s, f =>
                    {
                        f.Sub(0);
                        f.Lathe(o, Vector3.forward, new[] { V2(0, 0.17f), V2(0.14f, 0.17f), V2(0.14f, 0.21f) }, 10, 18f);
                        f.Sub(1);
                        var prof = new List<Vector2> { V2(0.14f, 0.21f) };
                        for (int i = 0; i < 6; i++) { prof.Add(V2(0.118f, 0.218f + i * 0.024f)); prof.Add(V2(0.128f, 0.23f + i * 0.024f)); }
                        prof.Add(V2(0.118f, 0.36f));
                        f.Lathe(o, Vector3.forward, prof.ToArray(), 10, 18f);
                        f.Sub(0);
                        f.Lathe(o, Vector3.forward, new[] { V2(0.118f, 0.36f), V2(0.125f, 0.365f), V2(0.125f, 0.38f), V2(0, 0.38f) }, 10, 18f);
                        f.Sub(2);
                        f.Lathe(o + V(0, 0, 0.38f), Vector3.forward, new[] { V2(0, 0), V2(0.075f, 0), V2(0.05f, 0.02f), V2(0, 0.022f) }, 10, 18f);
                    }), Vector3.zero, p.gun, p.copper, p.glow);
                }
                Part(root, "Arc", M("arc", f =>
                {
                    f.Seg(V(-0.33f, 0.66f, 0.39f), V(0.33f, 0.66f, 0.39f), V2(0.006f, 0.006f), V2(0.006f, 0.006f), Vector3.up);
                }), Vector3.zero, p.glow);
                break;
        }
    }

    // ---- tool visuals (RobotLoadout.Build) -----------------------------------------------------------

    // Ômega's claw fingers: lit jaw plates on the inner faces of the arm tips.
    public static void Fingers(Transform root, Palette p)
    {
        for (int s = -1; s <= 1; s += 2)
            Part(root, "Finger", M("finger" + s, f =>
            {
                f.Sub(0);
                f.CBox(V(s * 0.618f, -0.80f, G + 0.065f), V(0.035f, 0.16f, 0.1f), 0.01f);
                f.Sub(1);
                for (int i = 0; i < 3; i++) f.Flat(V(-s, 0, 0), V(s * 0.599f, -0.86f + i * 0.05f, G + 0.03f), V(s * 0.599f, -0.835f + i * 0.05f, G + 0.03f), V(s * 0.599f, -0.835f + i * 0.05f, G + 0.10f), V(s * 0.599f, -0.86f + i * 0.05f, G + 0.10f));
            }), Vector3.zero, p.steel, p.glow);
    }

    public static Mesh Blade => M("blade", f =>
    {
        f.Sub(0);
        f.CBox(Vector3.zero, V(1.2f, 0.07f, 0.3f), 0.018f);
        f.Sub(1);
        f.Seg(V(-0.58f, -0.037f, 0.13f), V(0.58f, -0.037f, 0.13f), V2(0.004f, 0.008f), V2(0.004f, 0.008f), Vector3.up);
        f.Sub(2);
        for (int i = 0; i < 4; i++) f.Flat(Vector3.down, V(-0.5f + i * 0.28f, -0.036f, -0.1f), V(-0.42f + i * 0.28f, -0.036f, -0.1f), V(-0.36f + i * 0.28f, -0.036f, 0.06f), V(-0.44f + i * 0.28f, -0.036f, 0.06f));
    });

    public static Mesh BladeBrace => M("bladebrace", f =>
    {
        for (int s = -1; s <= 1; s += 2) f.Seg(V(s * 0.76f, -0.88f, 0.0f), V(s * 0.76f, -1.06f, 0.03f), V2(0.03f, 0.03f), V2(0.025f, 0.025f), Vector3.up);
    });

    public static Mesh KickPlate => M("kickplate", f =>
    {
        f.Sub(0);
        f.CBox(Vector3.zero, V(0.9f, 0.03f, 0.19f), 0.01f);
        for (int s = -1; s <= 1; s += 2) f.Tube(V(s * 0.25f, 0.01f, 0), V(s * 0.25f, 0.40f, 0), 0.025f, 6);
        f.Sub(1);
        f.Flat(Vector3.down, V(-0.4f, -0.017f, -0.015f), V(0.4f, -0.017f, -0.015f), V(0.38f, -0.017f, 0.015f), V(-0.38f, -0.017f, 0.015f));
    });

    public static Mesh Nozzle => M("nozzle", f =>
    {
        f.Sub(0);
        f.Lathe(Vector3.zero, Vector3.up, new[] { V2(0, 0), V2(0.07f, 0), V2(0.085f, 0.035f), V2(0.075f, 0.075f), V2(0.06f, 0.075f) }, 8, 22.5f);
        f.Sub(1);
        var ring = new Vector3[8];
        for (int k = 0; k < 8; k++) { float a = (22.5f + k * 45f) * Mathf.Deg2Rad; ring[k] = V(Mathf.Cos(a) * 0.058f, 0.07f, Mathf.Sin(a) * 0.058f); }
        f.Flat(Vector3.up, ring);
    });

    public static Mesh Flame => M("flame", f =>
    {
        f.Lathe(Vector3.zero, Vector3.up, new[] { V2(0, 0), V2(0.06f, 0), V2(0.07f, 0.05f), V2(0, 0.32f) }, 8, 22.5f);
    });

    public static Mesh MagnetPad => M("magnet", f =>
    {
        f.Lathe(Vector3.zero, Vector3.forward, new[] { V2(0, 0), V2(0.075f, 0), V2(0.075f, 0.012f), V2(0.058f, 0.022f), V2(0, 0.022f) }, 6, 30f);
    });
}

// Brake light materials shared between the loadout and the parts (RobotFx swaps them).
public static class RobotLoadoutMats
{
    static Material on, off;
    public static Material BrakeOn => on ? on : on = RobotLoadout.Mat(new Color(1f, 0.1f, 0.08f), 0f, 0.8f, new Color(4f, 0.3f, 0.2f));
    public static Material BrakeOff => off ? off : off = RobotLoadout.Mat(new Color(0.35f, 0.04f, 0.04f), 0f, 0.8f);
}
