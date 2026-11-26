using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The three arenas, applied to the one rink at runtime (no extra scenes or assets):
//   0 Clássica - the original wood rink.
//   1 Gelo     - ice floor with rink markings, cold light, the puck glides further, robots skid.
//   2 Fábrica  - steel plate floor, warm industrial light, four pillars in the midfield.
public static partial class ArenaTheme
{
    public static int Current { get; private set; }

    static bool cached;
    static Renderer floor; static Material woodMat, iceMat, steelMat;
    static Collider floorCol; static PhysicsMaterial woodPhys, icePhys;
    static Light key; static Light[] floods; static Color keyColor, floodColor; static float keyInt, floodInt;
    static Color amSky, amEq, amGround;
    static ColorAdjustments grade; static Color gradeFilter; static float gradeExposure;
    static Rigidbody puckRb; static float puckDrag;
    static GameObject iceLines, pillars;

    static readonly Vector3 Center = new Vector3(634f, 10f, 400f);

    static partial void ApplyImpl(int index)
    {
        if (!Cache()) return;
        Current = Mathf.Clamp(index, 0, Names.Length - 1);

        // Defaults (Clássica).
        floor.sharedMaterial = woodMat; floorCol.sharedMaterial = woodPhys;
        key.color = keyColor; key.intensity = keyInt;
        foreach (var f in floods) { f.color = floodColor; f.intensity = floodInt; }
        RenderSettings.ambientSkyColor = amSky; RenderSettings.ambientEquatorColor = amEq; RenderSettings.ambientGroundColor = amGround;
        if (grade) { grade.colorFilter.value = gradeFilter; grade.postExposure.value = gradeExposure; }
        puckRb.linearDamping = puckDrag;
        moverp1.Grip = 1f;
        iceLines.SetActive(false); pillars.SetActive(false);

        if (Current == 1)
        {
            floor.sharedMaterial = iceMat; floorCol.sharedMaterial = icePhys;
            key.color = new Color(0.84f, 0.92f, 1f);
            foreach (var f in floods) f.color = new Color(0.80f, 0.90f, 1f);
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.66f, 0.82f);
            if (grade) grade.colorFilter.value = new Color(0.92f, 0.97f, 1f);
            puckRb.linearDamping = 0.08f;
            moverp1.Grip = 0.10f;
            iceLines.SetActive(true);
        }
        else if (Current == 2)
        {
            floor.sharedMaterial = steelMat;
            key.color = new Color(1f, 0.80f, 0.58f); key.intensity = keyInt * 0.85f;
            foreach (var f in floods) { f.color = new Color(1f, 0.74f, 0.45f); f.intensity = floodInt * 0.9f; }
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.40f, 0.34f);
            RenderSettings.ambientEquatorColor = amEq * 0.85f;
            if (grade) grade.colorFilter.value = new Color(1f, 0.94f, 0.86f);
            pillars.SetActive(true);
        }
        ArenaDressing.ApplyTheme(Current, key, floods);   // neon accents + darker bowl
    }

    static GameObject FindPath(string path)
    {
        foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!g.scene.IsValid()) continue;
            string p = g.name; for (var t = g.transform.parent; t; t = t.parent) p = t.name + "/" + p;
            if (p == path) return g;
        }
        return null;
    }

    static bool Cache()
    {
        if (cached && floor) return true;
        var plane = FindPath("JOGANDO/arena/Plane"); if (!plane) return false;
        woodMat = plane.GetComponent<Renderer>().sharedMaterial;
        floor = RinkBuilder.Floor ? RinkBuilder.Floor : plane.GetComponent<Renderer>();   // the generated rounded floor
        floorCol = plane.GetComponent<Collider>(); woodPhys = floorCol.sharedMaterial;
        icePhys = new PhysicsMaterial("Ice") { dynamicFriction = 0.03f, staticFriction = 0.03f, bounciness = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };
        key = FindPath("JOGANDO/Lighting/Key Light").GetComponent<Light>();
        floods = FindPath("JOGANDO/Lighting").GetComponentsInChildren<Light>(true);
        floods = System.Array.FindAll(floods, l => l != key && l.type == LightType.Spot);
        keyColor = key.color; keyInt = key.intensity;
        floodColor = floods.Length > 0 ? floods[0].color : Color.white; floodInt = floods.Length > 0 ? floods[0].intensity : 1f;
        amSky = RenderSettings.ambientSkyColor; amEq = RenderSettings.ambientEquatorColor; amGround = RenderSettings.ambientGroundColor;
        var volGo = FindPath("Global Volume");
        // .profile = a per-session copy, so play mode never edits the asset on disk.
        if (volGo && volGo.GetComponent<Volume>().profile.TryGet(out grade)) { gradeFilter = grade.colorFilter.value; gradeExposure = grade.postExposure.value; }
        puckRb = (FindPath("JOGANDO/arena/PUCK") ?? FindPath("JOGANDO/PUCK")).GetComponent<Rigidbody>(); puckDrag = puckRb.linearDamping;

        iceMat = new Material(woodMat) { name = "Ice (runtime)" };
        iceMat.SetTexture("_BaseMap", IceTexture()); iceMat.SetTextureScale("_BaseMap", new Vector2(5, 2));
        iceMat.SetColor("_BaseColor", Color.white); iceMat.SetFloat("_Smoothness", 0.93f); iceMat.SetFloat("_Metallic", 0f);

        steelMat = new Material(woodMat) { name = "Steel plate (runtime)" };
        steelMat.SetTexture("_BaseMap", PlateTexture()); steelMat.SetTextureScale("_BaseMap", new Vector2(36, 15));
        steelMat.SetColor("_BaseColor", new Color(0.78f, 0.80f, 0.84f)); steelMat.SetFloat("_Smoothness", 0.55f); steelMat.SetFloat("_Metallic", 0.75f);

        var arena = FindPath("JOGANDO/arena").transform;
        iceLines = BuildIceLines(arena);
        pillars = BuildPillars(arena);
        cached = true;
        return true;
    }

    // ---- Gelo -------------------------------------------------------------------------------

    static GameObject BuildIceLines(Transform arena)
    {
        var root = new GameObject("Gelo Markings"); root.transform.SetParent(arena, false);
        var red = LineMat(new Color(0.85f, 0.10f, 0.12f)); var blue = LineMat(new Color(0.10f, 0.30f, 0.85f));
        Strip(root.transform, "Center Line", Center.x, 16f, red);
        Strip(root.transform, "Blue Line A", Center.x - 330f, 22f, blue);
        Strip(root.transform, "Blue Line B", Center.x + 330f, 22f, blue);
        Strip(root.transform, "Goal Line A", -400f, 6f, red);
        Strip(root.transform, "Goal Line B", 1656f, 6f, red);
        Ring(root.transform, Center, 150f, 8f, blue);
        root.SetActive(false);
        return root;
    }

    static Material LineMat(Color c)
    {
        var m = new Material(woodMat) { name = "Line" };
        m.SetTexture("_BaseMap", null); m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0.9f); m.SetFloat("_Metallic", 0f);
        return m;
    }

    static void Strip(Transform parent, string name, float x, float width, Material m)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = name;
        Object.Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.SetPositionAndRotation(new Vector3(x, Center.y + 0.6f, Center.z), Quaternion.Euler(90, 0, 0));
        q.transform.localScale = new Vector3(width, 1000f, 1);
        Rink.KeepSize(q.transform, keepX: true, keepZ: false);   // thickness stays, length spans the rink
        var r = q.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
    }

    static void Ring(Transform parent, Vector3 c, float radius, float width, Material m)
    {
        const int seg = 64;
        var verts = new Vector3[seg * 2]; var tris = new int[seg * 6];
        for (int i = 0; i < seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            verts[i * 2] = d * (radius - width / 2); verts[i * 2 + 1] = d * (radius + width / 2);
            int n = (i + 1) % seg, t = i * 6;
            tris[t] = i * 2; tris[t + 1] = n * 2; tris[t + 2] = i * 2 + 1;
            tris[t + 3] = i * 2 + 1; tris[t + 4] = n * 2; tris[t + 5] = n * 2 + 1;
        }
        var mesh = new Mesh { vertices = verts, triangles = tris }; mesh.RecalculateNormals();
        var go = new GameObject("Center Circle", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false); go.transform.position = c + Vector3.up * 0.6f;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        Rink.KeepSize(go.transform);   // stays round
        var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
    }

    static Texture2D IceTexture()
    {
        const int N = 256;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Ice" };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.025f, y * 0.025f) * 0.6f + Mathf.PerlinNoise(x * 0.11f + 30, y * 0.11f) * 0.4f;
                float v = 0.86f + n * 0.09f;
                px[y * N + x] = new Color(v * 0.94f, v * 0.98f, Mathf.Min(1f, v * 1.04f));
            }
        var rng = new System.Random(7);
        for (int s = 0; s < 60; s++)   // skate scratches
        {
            float x0 = rng.Next(N), y0 = rng.Next(N), ang = (float)rng.NextDouble() * Mathf.PI, len = 20 + rng.Next(70);
            for (int i = 0; i < len; i++)
            {
                int x = ((int)(x0 + Mathf.Cos(ang) * i) % N + N) % N, y = ((int)(y0 + Mathf.Sin(ang) * i) % N + N) % N;
                px[y * N + x] = Color.Lerp(px[y * N + x], Color.white, 0.55f);
            }
        }
        tex.SetPixels(px); tex.Apply(true);
        return tex;
    }

    // ---- Fábrica ----------------------------------------------------------------------------

    static GameObject BuildPillars(Transform arena)
    {
        var root = new GameObject("Fabrica Pillars"); root.transform.SetParent(arena, false);
        var boards = FindPath("JOGANDO/arena/policarbonato").GetComponent<Collider>().sharedMaterial;
        var steel = new Material(woodMat) { name = "Pillar" };
        steel.SetTexture("_BaseMap", HazardTexture()); steel.SetTextureScale("_BaseMap", new Vector2(3, 1));
        steel.SetColor("_BaseColor", Color.white); steel.SetFloat("_Smoothness", 0.5f); steel.SetFloat("_Metallic", 0.4f);
        foreach (var p in new[] { new Vector2(334, 160), new Vector2(334, 640), new Vector2(934, 160), new Vector2(934, 640) })
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder); c.name = "Pillar";
            c.transform.SetParent(root.transform, false);
            c.transform.position = new Vector3(p.x, Center.y + 60f, p.y);
            c.transform.localScale = new Vector3(80f, 60f, 80f);   // 80 wide, 120 tall
            Object.Destroy(c.GetComponent<Collider>());
            var col = c.AddComponent<CapsuleCollider>(); col.radius = 0.5f; col.height = 2f; col.sharedMaterial = boards;
            c.GetComponent<Renderer>().sharedMaterial = steel;
            Rink.KeepSize(c.transform);   // stays round; spreads out with the rink
        }
        root.SetActive(false);
        return root;
    }

    static Texture2D PlateTexture()
    {
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Plate" };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                // Two raised lozenges per tile, at +-45 degrees (diamond plate).
                float u = (x % 32) / 32f - 0.5f, v = (y % 32) / 32f - 0.5f;
                bool flip = ((x / 32) + (y / 32)) % 2 == 0;
                float a = flip ? (u + v) : (u - v), b = flip ? (u - v) : (u + v);
                float bump = Mathf.Clamp01(1f - Mathf.Abs(a) * 5.5f - Mathf.Abs(b) * 1.5f);
                float g = 0.34f + bump * 0.22f + (Mathf.PerlinNoise(x * 0.3f, y * 0.3f) - 0.5f) * 0.05f;
                float edge = bump > 0 && bump < 0.25f ? -0.08f : 0f;
                px[y * N + x] = new Color(g + edge, g + edge, g + edge + 0.01f);
            }
        tex.SetPixels(px); tex.Apply(true);
        return tex;
    }

    static Texture2D HazardTexture()
    {
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Hazard" };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
                px[y * N + x] = ((x + y) / 16) % 2 == 0 ? new Color(0.95f, 0.70f, 0.08f) : new Color(0.08f, 0.08f, 0.09f);
        tex.SetPixels(px); tex.Apply(true);
        return tex;
    }
}
