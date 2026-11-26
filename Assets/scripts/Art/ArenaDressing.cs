using System.Collections.Generic;
using UnityEngine;

// "Neon esports" arena dressing, built at runtime around the generated rink (RinkBuilder):
//  - team-coloured LED strip along the top of the dasher boards (blue half / red half)
//  - dark ad panels with light text and neon underlines on the boards' straight runs
//  - a dark apron around the rink (replaces the scene's bright ground and old outer ring)
//  - two light trusses carrying the flood lights, with glowing lamp heads and team banners
//  - a glowing rim on the puck and neon-trimmed goal frames in team colours
//  - a darker stadium (less ambient and key light) so the neon reads; the floods keep the floor lit
// No colliders, no physics changes. Rebuilt whenever the rink size changes; ApplyTheme() tints it
// per arena (Clássica / Gelo / Fábrica).
public static class ArenaDressing
{
    public static readonly Color BlueGlow = new Color(0.08f, 0.42f, 1f) * 2.6f;
    public static readonly Color RedGlow = new Color(1f, 0.07f, 0.04f) * 2.6f;
    static readonly Color PuckGlow = new Color(0.1f, 0.85f, 1f) * 3.2f;

    static Transform root;
    static Material ledWhite, neonBlue, neonRed, neonWhite, lamp, panel, metal, apronMat, bannerBlue, bannerRed, puckRim;
    static Material accent;               // theme accent (ad underlines, truss lights)
    static readonly Dictionary<string, int> tris = new Dictionary<string, int>();
    public static IReadOnlyDictionary<string, int> Triangles => tris;   // for the art report
    public static int Theme { get; private set; }
    public static bool Enabled = true;   // false: plain arena (for before/after comparisons)

    // ---- entry points ---------------------------------------------------------------------------

    // Called by RinkBuilder.Rebuild after the boards and floor are regenerated.
    public static void Rebuild(IReadOnlyList<List<Vector3>> boardPieces, Transform parent)
    {
        Materials();
        if (root) { root.gameObject.SetActive(false); Object.Destroy(root.gameObject); }
        if (!Enabled)
        {
            foreach (var n in new[] { "Glow Rim", "Neon Trim" })
                foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>()) if (g.name == n && g.scene.IsValid()) Object.Destroy(g);
            return;
        }
        root = new GameObject("Arena Dressing").transform;
        root.SetParent(parent, false);
        root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);   // world-space meshes
        tris.Clear();

        HideOldRing();
        Apron();
        BoardNeon(boardPieces);
        AdPanels(boardPieces);
        Trusses();
        GoalTrim();
        PuckRim();
    }

    // Called by ArenaTheme after it sets the theme's lights and floor.
    public static void ApplyTheme(int theme, Light key, Light[] floods)
    {
        Theme = theme;
        Materials();
        if (!Enabled) return;
        var acc = theme == 1 ? new Color(0.45f, 0.95f, 1f) : theme == 2 ? new Color(1f, 0.62f, 0.12f) : new Color(0.85f, 0.9f, 1f);
        accent.SetColor("_BaseColor", acc * 0.6f); accent.SetColor("_EmissionColor", acc * 2.2f);
        lamp.SetColor("_EmissionColor", (theme == 2 ? new Color(1f, 0.8f, 0.55f) : theme == 1 ? new Color(0.85f, 0.95f, 1f) : new Color(1f, 0.97f, 0.9f)) * 3.2f);

        // Darker stadium: the bowl drops into shadow, the rink stays lit by the floods (the steel floor
        // of Fábrica is dark by itself, so it gets more flood light).
        RenderSettings.ambientSkyColor *= 0.5f;
        RenderSettings.ambientEquatorColor *= 0.5f;
        RenderSettings.ambientGroundColor *= 0.5f;
        if (key) key.intensity *= theme == 2 ? 1f : 0.8f;
        if (floods != null) foreach (var f in floods) if (f) f.intensity *= theme == 2 ? 2.4f : 1.6f;
        NightSky();
        DarkStands();
        // Fábrica's steel floor mostly reflects the sky; under a night sky it went black. Keep it
        // more diffuse (its runtime material, not an asset).
        var floor = RinkBuilder.Floor;
        if (theme == 2 && floor && floor.sharedMaterial && floor.sharedMaterial.name.StartsWith("Steel"))
        {
            floor.sharedMaterial.SetFloat("_Metallic", 0.35f); floor.sharedMaterial.SetFloat("_Smoothness", 0.5f);
            floor.sharedMaterial.SetColor("_BaseColor", new Color(0.98f, 1f, 1f));   // absolute: no build-up on re-apply
        }
    }

    // A night sky over the open roof: a darkened copy of the scene's skybox (the asset is untouched).
    static Material skySource, nightSky;
    static void NightSky()
    {
        if (RenderSettings.skybox == null) return;
        if (nightSky == null || RenderSettings.skybox != nightSky)
        {
            skySource = RenderSettings.skybox;
            nightSky = new Material(skySource) { name = "Night Sky (runtime)" };
            if (nightSky.HasProperty("_Exposure")) nightSky.SetFloat("_Exposure", 0.12f);
            if (nightSky.HasProperty("_SkyTint")) nightSky.SetColor("_SkyTint", new Color(0.18f, 0.2f, 0.32f));
            if (nightSky.HasProperty("_AtmosphereThickness")) nightSky.SetFloat("_AtmosphereThickness", 0.4f);
            if (nightSky.HasProperty("_Tint")) nightSky.SetColor("_Tint", new Color(0.12f, 0.13f, 0.2f));
            RenderSettings.skybox = nightSky;
        }
    }

    // The stands' bare concrete (between the crowd rows) drops into shadow: per-renderer material
    // copies, original colours remembered, so the scene's assets stay untouched.
    static readonly List<(Material m, Color c)> stands = new List<(Material, Color)>();
    static void DarkStands()
    {
        if (stands.Count > 0 && stands[0].m != null) return;
        stands.Clear();
        var b = Find("bancadatop");
        if (!b || !b.TryGetComponent(out Renderer r)) return;
        foreach (var m in r.materials)   // instances
        {
            var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
            stands.Add((m, c));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c * 0.38f);
        }
    }

    // ---- pieces ---------------------------------------------------------------------------------

    static GameObject Part(string name, Mesh mesh, params Material[] mats)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(root, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterials = mats;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tris[name] = mesh.triangles.Length / 3;
        return go;
    }

    static GameObject Find(string name)
    {
        foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
            if (g.name == name && g.scene.IsValid()) return g;
        return null;
    }

    // The scene's old black outer oval (with the logos) fights the new rounded boards: hide its
    // renderer (its collider still closes the stands).
    static void HideOldRing()
    {
        var p = Find("proteçao");
        if (p && p.TryGetComponent(out Renderer r)) r.enabled = false;
    }

    // Dark faceted apron from the outside of the boards out to the stands, with a thin neon edge.
    static void Apron()
    {
        float cx = Rink.Center.x, cz = Rink.Center.z, y = RinkBuilder.FloorY - 0.6f;
        float hl = RinkBuilder.HalfLength + RinkBuilder.Thickness, hw = RinkBuilder.HalfWidth + RinkBuilder.Thickness, r = RinkBuilder.CornerRadius + RinkBuilder.Thickness;
        const float outer = 900f;   // reaches under the stands' front
        var inner = Ring(cx, cz, hl, hw, r, y);
        var outRing = Ring(cx, cz, hl + outer, hw + outer, r + outer, y);
        var m = new FacetMesh();
        for (int i = 0; i < inner.Count; i++)
        {
            int j = (i + 1) % inner.Count;
            m.Flat(Vector3.up, inner[i], inner[j], outRing[j], outRing[i]);
        }
        Part("Apron", m.ToMesh("Arena Apron"), apronMat);
        // Hide the scene's bright ground plane under it.
        foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
            if (g.name == "Cube" && g.scene.IsValid() && g.transform.parent && g.transform.parent.name == "cenario externo" && g.TryGetComponent(out Renderer cr)) cr.enabled = false;
    }

    static List<Vector3> Ring(float cx, float cz, float hl, float hw, float r, float y)
    {
        var pts = new List<Vector3>();
        void Arc(float ox, float oz, float a0)
        {
            for (int i = 0; i <= 12; i++) { float a = (a0 + 90f * i / 12f) * Mathf.Deg2Rad; pts.Add(new Vector3(ox + Mathf.Cos(a) * r, y, oz + Mathf.Sin(a) * r)); }
        }
        Arc(cx + hl - r, cz + hw - r, 0); Arc(cx - hl + r, cz + hw - r, 90); Arc(cx - hl + r, cz - hw + r, 180); Arc(cx + hl - r, cz - hw + r, 270);
        return pts;
    }

    static Vector3 Outward(Vector3 a, Vector3 b)
    {
        var d = (b - a); d.y = 0; d.Normalize();
        var n = new Vector3(d.z, 0, -d.x);
        var mid = (a + b) * 0.5f; mid.y = 0;
        if (Vector3.Dot(n, mid - new Vector3(Rink.Center.x, 0, Rink.Center.z)) < 0) n = -n;
        return n;
    }

    // LED strip along the top of the dasher boards: blue on blue's half (-x), red on red's half (+x).
    static void BoardNeon(IReadOnlyList<List<Vector3>> pieces)
    {
        var m = new FacetMesh();
        float y = RinkBuilder.FloorY + RinkBuilder.DasherHeight + 1.8f;
        foreach (var pts in pieces)
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                var n = Outward(a, b);
                a += n * 3f; b += n * 3f; a.y = b.y = y; // on the dasher cap, just inside the glass
                // Split where the strip crosses the centre line so each half has one colour.
                float cx = Rink.Center.x;
                if ((a.x - cx) * (b.x - cx) < 0)
                {
                    var c = Vector3.Lerp(a, b, (cx - a.x) / (b.x - a.x));
                    Strip(m, a, c, a.x < cx ? 0 : 1); Strip(m, c, b, b.x < cx ? 0 : 1);
                }
                else Strip(m, a, b, (a.x + b.x) * 0.5f < cx ? 0 : 1);
            }
        Part("Board Neon", m.ToMesh("Board Neon"), neonBlue, neonRed);
    }

    static void Strip(FacetMesh m, Vector3 a, Vector3 b, int sub)
    {
        if ((b - a).sqrMagnitude < 0.25f) return;
        m.Sub(sub).Seg(a, b, new Vector2(4f, 2.2f), new Vector2(4f, 2.2f), Vector3.up);
    }

    // Dark ad panels on the inner face of the dasher boards (straight runs only), with light text
    // and a neon underline in the theme accent.
    static readonly string[] Ads = { "OMEGA HOCKEY", "ROBO LEAGUE", "NEON CUP", "OMEGA", "ARENA 2026", "POWERED BY ROBOS" };

    static void AdPanels(IReadOnlyList<List<Vector3>> pieces)
    {
        var m = new FacetMesh();
        var line = new FacetMesh();
        var text = new FacetMesh();
        int ad = 0;
        const float panelLen = 300f, gap = 40f, y0 = 14f, y1 = 50f;
        foreach (var pts in pieces)
        {
            // Walk the straight runs: consecutive segments with (nearly) the same direction.
            int i = 0;
            while (i + 1 < pts.Count)
            {
                var dir = (pts[i + 1] - pts[i]); dir.y = 0; dir.Normalize();
                int j = i + 1;
                while (j + 1 < pts.Count)
                {
                    var d2 = pts[j + 1] - pts[j]; d2.y = 0;
                    if (Vector3.Dot(d2.normalized, dir) < 0.999f) break;
                    j++;
                }
                var a = pts[i]; var b = pts[j];
                float len = Vector3.Distance(a, b);
                int count = Mathf.FloorToInt((len + gap) / (panelLen + gap));
                if (len > panelLen && count > 0)
                {
                    var n = Outward(a, b);
                    float used = count * panelLen + (count - 1) * gap, start = (len - used) / 2f;
                    for (int k = 0; k < count; k++)
                    {
                        float s0 = start + k * (panelLen + gap);
                        var p0 = a + dir * s0 - n * 2.6f; var p1 = a + dir * (s0 + panelLen) - n * 2.6f;
                        var c = (p0 + p1) / 2f;
                        var rot = Quaternion.LookRotation(n, Vector3.up);   // +z of the panel = into the wall
                        m.CBox(new Vector3(c.x, RinkBuilder.FloorY + (y0 + y1) / 2f, c.z), new Vector3(panelLen, y1 - y0, 3f), 1f, rot);
                        // Neon underline.
                        line.Seg(new Vector3(p0.x, RinkBuilder.FloorY + y0 + 3f, p0.z) - n * 1.8f + dir * 14f, new Vector3(p1.x, RinkBuilder.FloorY + y0 + 3f, p1.z) - n * 1.8f - dir * 14f,
                                 new Vector2(1.1f, 0.6f), new Vector2(1.1f, 0.6f), Vector3.up);
                        // LED dot-matrix text, readable from inside the rink.
                        var label = Ads[ad % Ads.Length];
                        float px = Mathf.Min(3.3f, (panelLen - 40f) / (label.Length * 6f));
                        var right = Vector3.Cross(Vector3.up, n);
                        PixelFont.Text(text, label, new Vector3(c.x, RinkBuilder.FloorY + (y0 + y1) / 2f + 2.5f, c.z) - n * 2.2f, right, Vector3.up, -n, px);
                        ad++;
                    }
                }
                i = j;
            }
        }
        Part("Ad Panels", m.ToMesh("Ad Panels"), panel);
        Part("Ad Text", text.ToMesh("Ad Text"), ledWhite);
        Part("Ad Underlines", line.ToMesh("Ad Underlines"), accent);
    }

    // Two light trusses along the rink, carrying the flood lights; lamp heads glow, team banners hang.
    static void Trusses()
    {
        var floods = new List<Light>();
        var lighting = Find("Lighting");
        if (lighting) foreach (var l in lighting.GetComponentsInChildren<Light>(true)) if (l.type == LightType.Spot) floods.Add(l);
        if (floods.Count == 0) return;
        float y = floods[0].transform.position.y + 55f;
        float minX = float.MaxValue, maxX = float.MinValue;
        var rows = new SortedDictionary<int, List<Light>>();
        foreach (var f in floods)
        {
            var p = f.transform.position; minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            int key = Mathf.RoundToInt(p.z / 50f);
            if (!rows.ContainsKey(key)) rows[key] = new List<Light>();
            rows[key].Add(f);
        }
        var truss = new FacetMesh(); var heads = new FacetMesh(); var lens = new FacetMesh(); var glow = new FacetMesh();
        float x0 = minX - 260f, x1 = maxX + 260f;
        const float w = 46f, h = 46f;   // truss cross-section
        foreach (var row in rows.Values)
        {
            float z = 0; foreach (var f in row) z += f.transform.position.z; z /= row.Count;
            // Chords.
            foreach (var oz in new[] { -w / 2, w / 2 })
                foreach (var oy in new[] { 0f, h })
                    truss.Tube(new Vector3(x0, y + oy, z + oz), new Vector3(x1, y + oy, z + oz), 3.2f, 6);
            // Zig-zag diagonals on both sides and on the bottom.
            int bays = Mathf.CeilToInt((x1 - x0) / 70f);
            for (int b = 0; b < bays; b++)
            {
                float xa = Mathf.Lerp(x0, x1, b / (float)bays), xb = Mathf.Lerp(x0, x1, (b + 1) / (float)bays);
                foreach (var oz in new[] { -w / 2, w / 2 })
                    truss.Tube(new Vector3(xa, y + (b % 2 == 0 ? 0 : h), z + oz), new Vector3(xb, y + (b % 2 == 0 ? h : 0), z + oz), 1.8f, 4);
                truss.Tube(new Vector3(xa, y, z - w / 2), new Vector3(xb, y, z + w / 2), 1.6f, 4);
            }
            // Accent light lines along both bottom chords (they also read against the night sky).
            foreach (var oz in new[] { -w / 2, w / 2 })
                glow.Seg(new Vector3(x0, y - 3.6f, z + oz), new Vector3(x1, y - 3.6f, z + oz), new Vector2(1.6f, 1.6f), new Vector2(1.6f, 1.6f), Vector3.up);
            // Hanging cables to the roof.
            foreach (var xc in new[] { x0 + 80f, (x0 + x1) / 2f, x1 - 80f })
                truss.Tube(new Vector3(xc, y + h, z), new Vector3(xc, y + h + 700f, z), 1.4f, 4);
            // Lamp heads aimed like the lights.
            foreach (var f in row)
            {
                var p = f.transform.position; var fw = f.transform.forward;
                var c = p - fw * 8f;
                heads.Lathe(c, -fw, new[] { new Vector2(0, -6), new Vector2(26, -6), new Vector2(30, 30), new Vector2(0, 34) }, 10);
                lens.Lathe(c + fw * 1f, fw, new[] { new Vector2(0, 0), new Vector2(25, 0), new Vector2(0, 1.5f) }, 10);
                glow.Lathe(c - fw * 2f, fw, new[] { new Vector2(27f, 0), new Vector2(31.5f, 0), new Vector2(31.5f, 3f), new Vector2(27f, 3f) }, 10);   // rim ring
                heads.Seg(p + Vector3.up * 30f, new Vector3(p.x, y, p.z), new Vector2(3, 3), new Vector2(3, 3));   // yoke
            }
            // Team banners hanging from the truss: blue over blue's half, red over red's half.
            BannerRow(z, y, x0, x1);
        }
        Part("Truss", truss.ToMesh("Light Truss"), metal);
        Part("Lamp Heads", heads.ToMesh("Lamp Heads"), metal);
        Part("Lamp Lenses", lens.ToMesh("Lamp Lenses"), lamp);
        Part("Truss Glow", glow.ToMesh("Truss Glow"), accent);
    }

    static void BannerRow(float z, float y, float x0, float x1)
    {
        var cloth = new FacetMesh(); var edge = new FacetMesh(); var text = new FacetMesh();
        float cx = Rink.Center.x;
        foreach (var side in new[] { -1, 1 })
        {
            // Two banners per half, between the lamps.
            for (int k = 0; k < 2; k++)
            {
                float x = cx + side * Mathf.Lerp(380f, (x1 - x0) * 0.42f, k);
                const float bw = 120f, bh = 230f;
                var top = new Vector3(x, y - 4f, z);
                var c = top + Vector3.down * bh / 2f;
                int sub = side < 0 ? 0 : 1;
                cloth.Sub(sub).Panel(c, new Vector3(bw, bh, 3f), 2f);
                // Pointed tail + neon edges.
                cloth.Sub(sub).Flat(Vector3.forward, c + new Vector3(-bw / 2, -bh / 2, 0), c + new Vector3(bw / 2, -bh / 2, 0), c + new Vector3(0, -bh / 2 - 40f, 0));
                cloth.Sub(sub).Flat(Vector3.back, c + new Vector3(-bw / 2, -bh / 2, 0), c + new Vector3(bw / 2, -bh / 2, 0), c + new Vector3(0, -bh / 2 - 40f, 0));
                foreach (var ox in new[] { -bw / 2, bw / 2 })
                    edge.Sub(sub).Seg(c + new Vector3(ox, bh / 2, 0), c + new Vector3(ox, -bh / 2, 0), new Vector2(2f, 2.4f), new Vector2(2f, 2.4f), Vector3.forward);
                foreach (var face in new[] { 1f, -1f })   // both faces; each only shows from its side
                {
                    var nrm = new Vector3(0, 0, -face);
                    var right = Vector3.Cross(Vector3.up, -nrm);
                    PixelFont.Text(text, "Ω", c + new Vector3(0, 40f, 0) + nrm * 2.2f, right, Vector3.up, nrm, 11f);
                    PixelFont.Text(text, side < 0 ? "AZUL" : "VERMELHO", c + new Vector3(0, -60f, 0) + nrm * 2.2f, right, Vector3.up, nrm, side < 0 ? 3.2f : 2.1f);
                }
            }
        }
        Part("Banners " + z.ToString("0"), cloth.ToMesh("Banners"), bannerBlue, bannerRed);
        Part("Banner Edges " + z.ToString("0"), edge.ToMesh("Banner Edges"), neonBlue, neonRed);
        Part("Banner Text " + z.ToString("0"), text.ToMesh("Banner Text"), ledWhite);
    }

    // Neon trim on the goal frames (posts and crossbar, facing the rink) and a glow line around the
    // net's base. Team colours: "gol" (+x) is red's goal, "gol (1)" (-x) is blue's goal.
    static void GoalTrim()
    {
        foreach (var (name, red) in new[] { ("gol Visual", true), ("gol (1) Visual", false) })
        {
            var vis = Find(name); if (!vis) continue;
            var old = vis.transform.Find("Neon Trim"); if (old) Object.Destroy(old.gameObject);
            Bounds Get(string part) { var t = vis.transform.Find(part); return t && t.TryGetComponent(out Renderer r) ? r.bounds : new Bounds(); }
            var pl = Get("Post L"); var pr = Get("Post R"); var bar = Get("Crossbar");
            // Dark gunmetal frame so the neon trim reads crisply (renderers only; colliders untouched).
            foreach (var part in new[] { "Post L", "Post R", "Crossbar", "Back Top", "Base L", "Base R", "Base Back" })
            { var t = vis.transform.Find(part); if (t && t.TryGetComponent(out Renderer pr2)) pr2.sharedMaterial = metal; }
            if (pl.size == Vector3.zero || pr.size == Vector3.zero) continue;
            float dir = vis.transform.position.x > Rink.Center.x ? -1f : 1f;   // toward the rink
            var m = new FacetMesh();
            float front(Bounds b) => dir < 0 ? b.min.x : b.max.x;
            foreach (var b in new[] { pl, pr })
                m.Tube(new Vector3(front(b) + dir * 1.2f, b.min.y + 2f, b.center.z), new Vector3(front(b) + dir * 1.2f, b.max.y, b.center.z), 3.4f, 6);
            if (bar.size != Vector3.zero)
                m.Tube(new Vector3(front(bar) + dir * 1.2f, bar.center.y, bar.min.z), new Vector3(front(bar) + dir * 1.2f, bar.center.y, bar.max.z), 3.4f, 6);
            // Base glow line: mouth line + around the net footprint.
            float mouth = vis.transform.position.x, back = mouth - dir * 222f, zl = pl.center.z, zr = pr.center.z;
            float yb = RinkBuilder.FloorY + 0.9f;
            var corners = new[] { new Vector3(mouth, yb, zl), new Vector3(back, yb, zl), new Vector3(back, yb, zr), new Vector3(mouth, yb, zr) };
            for (int i = 0; i < 4; i++) m.Seg(corners[i], corners[(i + 1) % 4], new Vector2(2.2f, 0.5f), new Vector2(2.2f, 0.5f), Vector3.up);
            var go = new GameObject("Neon Trim", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(vis.transform, true);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = new Vector3(1f / vis.transform.lossyScale.x, 1f / vis.transform.lossyScale.y, 1f / vis.transform.lossyScale.z);
            var mesh = m.ToMesh("Goal Neon " + (red ? "Red" : "Blue"));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = red ? neonRed : neonBlue;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tris["Goal Neon " + (red ? "Red" : "Blue")] = mesh.triangles.Length / 3;
        }
    }

    // A thin glowing band around the puck's edge so it can be tracked at speed.
    static void PuckRim()
    {
        var p = Find("PUCK"); if (!p) return;
        var old = p.transform.Find("Glow Rim"); if (old) Object.Destroy(old.gameObject);
        var mf = p.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) return;
        var b = mf.sharedMesh.bounds;   // puck-local
        float r = Mathf.Max(b.extents.x, b.extents.z);
        var m = new FacetMesh();
        // Band on the outer edge, middle third of the height (local units; the puck is scaled 60 x 8 x 60).
        float y0 = b.center.y - b.extents.y * 0.18f, y1 = b.center.y + b.extents.y * 0.18f;
        m.Lathe(new Vector3(b.center.x, 0, b.center.z), Vector3.up, new[] { new Vector2(r * 0.99f, y0), new Vector2(r * 1.025f, y0), new Vector2(r * 1.025f, y1), new Vector2(r * 0.99f, y1) }, 24);
        var go = new GameObject("Glow Rim", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(p.transform, false);
        var mesh = m.ToMesh("Puck Glow Rim");
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var rr = go.GetComponent<MeshRenderer>(); rr.sharedMaterial = puckRim; rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tris["Puck Glow Rim"] = mesh.triangles.Length / 3;
    }

    // ---- materials --------------------------------------------------------------------------------

    static void Materials()
    {
        if (neonBlue) return;
        neonBlue = Mat("Neon Blue", new Color(0.1f, 0.3f, 0.7f), 0f, 0.9f, BlueGlow);
        neonRed = Mat("Neon Red", new Color(0.7f, 0.08f, 0.05f), 0f, 0.9f, RedGlow);
        neonWhite = Mat("Neon White", Color.white, 0f, 0.9f, new Color(1f, 0.95f, 0.9f) * 2f);
        lamp = Mat("Lamp Lens", Color.white, 0f, 0.95f, new Color(1f, 0.97f, 0.9f) * 3.2f);
        panel = Mat("Ad Panel", new Color(0.035f, 0.04f, 0.06f), 0.4f, 0.75f);
        metal = Mat("Truss Metal", new Color(0.09f, 0.095f, 0.11f), 0.85f, 0.55f);
        apronMat = Mat("Apron", new Color(0.025f, 0.027f, 0.035f), 0.2f, 0.45f);
        bannerBlue = Mat("Banner Blue", new Color(0.03f, 0.08f, 0.22f), 0f, 0.35f, new Color(0.02f, 0.06f, 0.2f));
        bannerRed = Mat("Banner Red", new Color(0.22f, 0.025f, 0.02f), 0f, 0.35f, new Color(0.2f, 0.02f, 0.015f));
        puckRim = Mat("Puck Rim", new Color(0.1f, 0.8f, 1f), 0f, 0.9f, PuckGlow);
        ledWhite = Mat("LED White", new Color(0.9f, 0.92f, 1f), 0f, 0.8f, new Color(1f, 0.97f, 0.92f) * 1.8f);
        accent = Mat("Arena Accent", new Color(0.5f, 0.55f, 0.6f), 0f, 0.9f, new Color(0.85f, 0.9f, 1f) * 2.2f);
    }

    static Material Mat(string name, Color c, float metalness, float smooth, Color emission = default)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh) { name = name };
        m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
        m.SetFloat("_Metallic", metalness); m.SetFloat("_Smoothness", smooth);
        if (emission.maxColorComponent > 0)
        {
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        return m;
    }
}
