using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds proper goal visuals around the existing scoring colliders ("gol", "gol (1)").
// The colliders are untouched (puck.cs scores on them); this only replaces the white stand-in boxes.
public static class GoalBuilder
{
    const string Scene = "Assets/Scenes/MENU.unity";
    const string NetTexPath = "Assets/Materials/Goal/NetGrid.png";

    public static string Run()
    {
        var log = new StringBuilder();
        var scene = EditorSceneManager.OpenScene(Scene);
        if (!AssetDatabase.IsValidFolder("Assets/Materials/Goal")) AssetDatabase.CreateFolder("Assets/Materials", "Goal");
        var netTex = MakeNetTexture();
        var lit = Shader.Find("Universal Render Pipeline/Lit");

        var net = Mat("Assets/Materials/Goal/Net.mat", lit, new Color(0.92f, 0.94f, 0.97f), 0f, 0.3f);
        net.SetTexture("_BaseMap", netTex);
        net.SetTextureScale("_BaseMap", new Vector2(1, 1));
        net.SetFloat("_AlphaClip", 1); net.SetFloat("_Cutoff", 0.5f); net.EnableKeyword("_ALPHATEST_ON");
        net.SetFloat("_Cull", 0); // both sides
        net.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        EditorUtility.SetDirty(net);

        var red = FrameMat("Assets/Materials/Goal/FrameRed.mat", lit, new Color(0.85f, 0.08f, 0.06f), new Color(2.2f, 0.12f, 0.05f));
        var blue = FrameMat("Assets/Materials/Goal/FrameBlue.mat", lit, new Color(0.05f, 0.25f, 0.9f), new Color(0.05f, 0.5f, 2.6f));
        var steel = Mat("Assets/Materials/Goal/Steel.mat", lit, new Color(0.75f, 0.76f, 0.78f), 0.9f, 0.7f);

        foreach (var name in new[] { "gol", "gol (1)" })
        {
            var goal = ShotTool.Find("JOGANDO/arena/" + name);
            if (goal == null) { log.AppendLine("missing " + name); continue; }
            var col = goal.GetComponent<BoxCollider>();

            // Remove the old white stand-in.
            var old = goal.transform.Find("visual");
            if (old) Object.DestroyImmediate(old.gameObject);
            var prev = ShotTool.Find("JOGANDO/arena/" + name + " Visual");
            if (prev) Object.DestroyImmediate(prev);

            // World-space bounds of the scoring box (computed from the collider, robust to the odd scale/rotation).
            var b = WorldBounds(col);
            float floorY = 10f;                                       // rink floor plane
            bool facesNegX = b.center.x > 634f;                        // which end of the rink
            float mouthX = facesNegX ? b.min.x : b.max.x;              // goal line = collider face toward the rink
            float dir = facesNegX ? 1f : -1f;                          // into the net
            float width = b.size.z, height = 110f, depth = Mathf.Min(b.size.x, 220f);
            float zc = b.center.z;
            var frameMat = facesNegX ? red : blue;                    // "gol" (+x end) is red's goal, "gol (1)" blue's

            var root = new GameObject(name + " Visual");
            root.transform.SetParent(goal.transform.parent, true);
            root.transform.position = new Vector3(mouthX, floorY, zc);

            float r = 9f; // post radius
            // Front posts + crossbar
            Bar(root, "Post L", new Vector3(0, height / 2, -width / 2), new Vector3(r * 2, height / 2, r * 2), frameMat, true);
            Bar(root, "Post R", new Vector3(0, height / 2, width / 2), new Vector3(r * 2, height / 2, r * 2), frameMat, true);
            Box(root, "Crossbar", new Vector3(0, height, 0), new Vector3(r * 2, r * 2, width + r * 2), frameMat);
            // Back frame (lower, deeper)
            float backH = height * 0.55f;
            Box(root, "Back Top", new Vector3(dir * depth, backH, 0), new Vector3(r, r, width), steel);
            Box(root, "Base L", new Vector3(dir * depth / 2, r / 2, -width / 2), new Vector3(depth, r, r), steel);
            Box(root, "Base R", new Vector3(dir * depth / 2, r / 2, width / 2), new Vector3(depth, r, r), steel);
            Box(root, "Base Back", new Vector3(dir * depth, r / 2, 0), new Vector3(r, r, width), steel);
            // Net panels (visual only; the scoring collider already stops the puck)
            Quad(root, "Net Back", new Vector3(dir * depth, backH / 2, 0), new Vector3(width, backH, 1), Quaternion.Euler(0, 90, 0), net, width / 30f, backH / 30f);
            var topLen = Mathf.Sqrt(depth * depth + (height - backH) * (height - backH));
            var topAngle = Mathf.Atan2(height - backH, depth) * Mathf.Rad2Deg;
            Quad(root, "Net Top", new Vector3(dir * depth / 2, (height + backH) / 2, 0), new Vector3(topLen, width, 1),
                 Quaternion.Euler(0, facesNegX ? 0 : 180, 0) * Quaternion.Euler(0, 0, -topAngle) * Quaternion.Euler(90, 0, 0), net, topLen / 30f, width / 30f);
            foreach (var side in new[] { -1f, 1f })
                SideNet(root, "Net Side " + (side < 0 ? "L" : "R"), side * width / 2, dir, depth, height, backH, net);
            // Goal light strip on the crossbar (emissive, flashes later on goals)
            Box(root, "Light Strip", new Vector3(dir * 2f, height + r * 1.2f, 0), new Vector3(4f, 3f, width * 0.9f), frameMat);

            log.AppendLine($"{name}: mouth x={mouthX:F0} dir={dir} width={width:F0} height={height:F0} depth={depth:F0} color={(facesNegX ? "red" : "blue")}");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    static Bounds WorldBounds(BoxCollider c)
    {
        var t = c.transform; var e = c.size * 0.5f;
        var b = new Bounds(t.TransformPoint(c.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
            b.Encapsulate(t.TransformPoint(c.center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z)));
        return b;
    }

    static void Bar(GameObject root, string n, Vector3 pos, Vector3 scale, Material m, bool cylinder)
    {
        var go = GameObject.CreatePrimitive(cylinder ? PrimitiveType.Cylinder : PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = n; go.transform.SetParent(root.transform, false);
        go.transform.localPosition = pos; go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
    }

    static void Box(GameObject root, string n, Vector3 pos, Vector3 scale, Material m) => Bar(root, n, pos, scale, m, false);

    static void Quad(GameObject root, string n, Vector3 pos, Vector3 scale, Quaternion rot, Material m, float tx, float ty)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = n; go.transform.SetParent(root.transform, false);
        go.transform.localPosition = pos; go.transform.localRotation = rot; go.transform.localScale = scale;
        var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        // Tile the net per panel size via a property block-free approach: bake UV scale into the mesh.
        var mesh = Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
        var uv = mesh.uv; for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(uv[i].x * tx, uv[i].y * ty);
        mesh.uv = uv; mesh.name = n + " Mesh";
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    static void SideNet(GameObject root, string n, float z, float dir, float depth, float height, float backH, Material m)
    {
        // Trapezoid: mouth edge (height) to back edge (backH).
        var mesh = new Mesh { name = n + " Mesh" };
        mesh.vertices = new[] { new Vector3(0, 0, z), new Vector3(0, height, z), new Vector3(dir * depth, backH, z), new Vector3(dir * depth, 0, z) };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, height / 30f), new Vector2(depth / 30f, backH / 30f), new Vector2(depth / 30f, 0) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var go = new GameObject(n, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(root.transform, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        SaveMesh(mesh);
    }

    static void SaveMesh(Mesh mesh)
    {
        var p = "Assets/Materials/Goal/" + mesh.name + ".asset";
        AssetDatabase.DeleteAsset(p);
        AssetDatabase.CreateAsset(mesh, p);
    }

    static Texture2D MakeNetTexture()
    {
        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, true);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                // Diamond net: two diagonal line families
                int a = (x + y) % 32, b = (x - y + 64) % 32;
                bool line = a < 3 || b < 3;
                tex.SetPixel(x, y, line ? Color.white : new Color(1, 1, 1, 0));
            }
        tex.Apply();
        File.WriteAllBytes(NetTexPath, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(NetTexPath);
        var imp = (TextureImporter)AssetImporter.GetAtPath(NetTexPath);
        imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(NetTexPath);
    }

    static Material Mat(string path, Shader s, Color c, float metal, float smooth)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(s); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(m); return m;
    }

    static Material FrameMat(string path, Shader s, Color c, Color emission)
    {
        var m = Mat(path, s, c, 0.6f, 0.8f);
        m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(m); return m;
    }
}
