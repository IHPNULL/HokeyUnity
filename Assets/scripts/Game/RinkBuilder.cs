using System.Collections.Generic;
using UnityEngine;

// The rink's floor and boards, generated for the current rink size: a rounded rectangle with
// big corner radii (like a real hockey rink), white dasher boards with glass above, and an
// opening at each end that fits the goal's net exactly. Replaces the scene's stretched board
// mesh ("policarbonato") and square floor ("Plane"), which stay only as the floor's collider.
public static class RinkBuilder
{
    public const float FloorY = 10f, WallHeight = 140f, DasherHeight = 46f, Thickness = 18f;
    public const float GoalGapHalf = 282f;   // net side walls: 270 + 12
    public const float BehindMouth = 57f;    // end boards stand this far behind the goal line

    public static float HalfLength { get; private set; }
    public static float HalfWidth { get; private set; }
    public static float CornerRadius { get; private set; }
    public static Renderer Floor => floorMF ? floorMF.GetComponent<Renderer>() : null;

    static Transform root;
    static MeshFilter floorMF, glassMF, dasherMF, kickMF;
    static MeshCollider wallCol;

    public static void Setup(Transform parent, Material floorMat, Material glassMat, PhysicsMaterial boards)
    {
        root = new GameObject("Rink Boards").transform;
        root.SetParent(parent, false);
        root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);   // meshes are built in world coordinates
        floorMF = Part("Rink Floor", floorMat);
        glassMF = Part("Glass", glassMat);
        dasherMF = Part("Dasher Boards", Lit(new Color(0.93f, 0.94f, 0.96f), 0.1f, 0.55f));
        kickMF = Part("Kick Plate", Lit(new Color(0.95f, 0.78f, 0.12f), 0.1f, 0.5f));
        floorMF.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        wallCol = glassMF.gameObject.AddComponent<MeshCollider>();
        wallCol.sharedMaterial = boards;
    }

    static MeshFilter Part(string name, Material m)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(root, false);
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        return go.GetComponent<MeshFilter>();
    }

    static Material Lit(Color c, float metal, float smooth)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        m.SetColor("_BaseColor", c); m.SetColor("_Color", c); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
        return m;
    }

    // Rebuild for Rink.Scale (call after the arena is stretched).
    public static void Rebuild()
    {
        if (root == null) return;
        HalfLength = 1022f * Rink.Scale.x + BehindMouth;
        HalfWidth = 537f * Rink.Scale.y;
        // As round as possible while the end boards still have room for the goal opening.
        CornerRadius = Mathf.Min(HalfWidth - GoalGapHalf - 20f, HalfWidth * 0.62f);

        var pieces = new List<List<Vector3>> { Subdivide(Polyline(true)), Subdivide(Polyline(false)) };
        glassMF.sharedMesh = Wall(pieces, 0f, WallHeight, 0f, Thickness, 0.25f);
        dasherMF.sharedMesh = Wall(pieces, 0f, DasherHeight, -1.5f, Thickness + 3f, 1f);
        kickMF.sharedMesh = Wall(pieces, 0f, 12f, -2.5f, Thickness + 5f, 1f);
        wallCol.sharedMesh = null; wallCol.sharedMesh = glassMF.sharedMesh;
        floorMF.sharedMesh = FloorMesh();
        ArenaDressing.Rebuild(pieces, root.parent);   // neon strip, ad panels, trusses, goal and puck trim
    }

    // Long straight runs split into short pieces: PhysX dislikes very long collider triangles.
    static List<Vector3> Subdivide(List<Vector3> pts, float maxLen = 160f)
    {
        var o = new List<Vector3> { pts[0] };
        for (int i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1]; var b = pts[i];
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / maxLen));
            for (int k = 1; k <= n; k++) o.Add(Vector3.Lerp(a, b, k / (float)n));
        }
        return o;
    }

    // Inner edge of the boards, split by the two goal openings. upper = the +z half.
    static List<Vector3> Polyline(bool upper)
    {
        float cx = Rink.Center.x, cz = Rink.Center.z, hl = HalfLength, hw = HalfWidth, r = CornerRadius, g = GoalGapHalf;
        var pts = new List<Vector3>();
        void Arc(float ox, float oz, float a0, float a1)
        {
            const int steps = 24;
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)steps) * Mathf.Deg2Rad;
                pts.Add(new Vector3(ox + Mathf.Cos(a) * r, 0, oz + Mathf.Sin(a) * r));
            }
        }
        if (upper)
        {
            pts.Add(new Vector3(cx + hl, 0, cz + g));
            Arc(cx + hl - r, cz + hw - r, 0f, 90f);
            Arc(cx - hl + r, cz + hw - r, 90f, 180f);
            pts.Add(new Vector3(cx - hl, 0, cz + g));
        }
        else
        {
            pts.Add(new Vector3(cx - hl, 0, cz - g));
            Arc(cx - hl + r, cz - hw + r, 180f, 270f);
            Arc(cx + hl - r, cz - hw + r, 270f, 360f);
            pts.Add(new Vector3(cx + hl, 0, cz - g));
        }
        return pts;
    }

    // Extruded wall along each polyline: inner face, outer face, top, and the two end caps.
    static Mesh Wall(List<List<Vector3>> pieces, float y0, float y1, float inset, float thick, float uvScale)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        var c = new Vector3(Rink.Center.x, 0, Rink.Center.z);
        void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Vector3 normal, float u0, float u1, float v0, float v1)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(cc); v.Add(d);
            for (int k = 0; k < 4; k++) n.Add(normal);
            uv.Add(new Vector2(u0, v0)); uv.Add(new Vector2(u1, v0)); uv.Add(new Vector2(u1, v1)); uv.Add(new Vector2(u0, v1));
            // Both windings so the wall shows from either side (thin glass).
            t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2);
            t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }
        float Y0 = FloorY + y0, Y1 = FloorY + y1;
        foreach (var pts in pieces)
        {
            float len = 0;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                var p0 = pts[i]; var p1 = pts[i + 1];
                var dir = (p1 - p0); float seg = dir.magnitude; if (seg < 0.01f) continue; dir /= seg;
                var outN = new Vector3(dir.z, 0, -dir.x);
                if (Vector3.Dot(outN, (p0 + p1) * 0.5f - c) < 0) outN = -outN;
                var a0 = p0 + outN * inset; var a1 = p1 + outN * inset;          // inner face line
                var b0 = p0 + outN * (inset + thick); var b1 = p1 + outN * (inset + thick);
                float u0 = len / 200f * uvScale, u1 = (len + seg) / 200f * uvScale; len += seg;
                Quad(new Vector3(a0.x, Y0, a0.z), new Vector3(a1.x, Y0, a1.z), new Vector3(a1.x, Y1, a1.z), new Vector3(a0.x, Y1, a0.z), -outN, u0, u1, 0, 1);
                Quad(new Vector3(b0.x, Y0, b0.z), new Vector3(b1.x, Y0, b1.z), new Vector3(b1.x, Y1, b1.z), new Vector3(b0.x, Y1, b0.z), outN, u0, u1, 0, 1);
                Quad(new Vector3(a0.x, Y1, a0.z), new Vector3(a1.x, Y1, a1.z), new Vector3(b1.x, Y1, b1.z), new Vector3(b0.x, Y1, b0.z), Vector3.up, u0, u1, 0, 0.1f);
            }
            // End caps at the goal openings.
            foreach (var (p, q) in new[] { (pts[0], pts[1]), (pts[pts.Count - 1], pts[pts.Count - 2]) })
            {
                var dir = (q - p).normalized; var outN = new Vector3(dir.z, 0, -dir.x);
                if (Vector3.Dot(outN, p - c) < 0) outN = -outN;
                var a = p + outN * inset; var b = p + outN * (inset + thick);
                Quad(new Vector3(a.x, Y0, a.z), new Vector3(b.x, Y0, b.z), new Vector3(b.x, Y1, b.z), new Vector3(a.x, Y1, a.z), -dir, 0, 0.1f, 0, 1);
            }
        }
        var mesh = new Mesh { name = "Rink wall", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        return mesh;
    }

    // Floor: the rounded rectangle (out to the boards' outer face) plus the areas under the nets.
    static Mesh FloorMesh()
    {
        float cx = Rink.Center.x, cz = Rink.Center.z, hl = HalfLength + Thickness, hw = HalfWidth + Thickness, r = CornerRadius + Thickness;
        var ring = new List<Vector3>();
        void Arc(float ox, float oz, float a0)
        {
            for (int i = 0; i <= 24; i++) { float a = (a0 + 90f * i / 24f) * Mathf.Deg2Rad; ring.Add(new Vector3(ox + Mathf.Cos(a) * r, FloorY, oz + Mathf.Sin(a) * r)); }
        }
        Arc(cx + hl - r, cz + hw - r, 0); Arc(cx - hl + r, cz + hw - r, 90); Arc(cx - hl + r, cz - hw + r, 180); Arc(cx + hl - r, cz - hw + r, 270);
        var v = new List<Vector3> { new Vector3(cx, FloorY, cz) }; v.AddRange(ring);
        var t = new List<int>();
        for (int i = 1; i < v.Count; i++) { int j = i + 1 < v.Count ? i + 1 : 1; t.Add(0); t.Add(j); t.Add(i); }
        // Under each net (behind the end boards).
        foreach (float s in new[] { 1f, -1f })
        {
            float x0 = cx + s * (HalfLength - 5f), x1 = cx + s * (HalfLength + 260f);
            int i = v.Count;
            v.Add(new Vector3(x0, FloorY, cz - 320f)); v.Add(new Vector3(x1, FloorY, cz - 320f)); v.Add(new Vector3(x1, FloorY, cz + 320f)); v.Add(new Vector3(x0, FloorY, cz + 320f));
            if (s > 0) { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
            else { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
        }
        float minX = cx - HalfLength - 260f, sizeX = 2f * (HalfLength + 260f), minZ = cz - hw, sizeZ = 2f * hw;
        var uv = new List<Vector2>(); foreach (var p in v) uv.Add(new Vector2((p.x - minX) / sizeX, (p.z - minZ) / sizeZ));
        var n = new List<Vector3>(); foreach (var _ in v) n.Add(Vector3.up);
        var mesh = new Mesh { name = "Rink floor" };
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        return mesh;
    }

    // Keep a point inside the rounded boards (margin = distance from the boards).
    public static Vector3 ClampInside(Vector3 p, float margin)
    {
        if (root == null || HalfLength <= 0) return p;
        float cx = Rink.Center.x, cz = Rink.Center.z;
        float hl = HalfLength - margin, hw = HalfWidth - margin, r = Mathf.Max(1f, CornerRadius - margin);
        float x = Mathf.Clamp(p.x - cx, -hl, hl), z = Mathf.Clamp(p.z - cz, -hw, hw);
        float ix = hl - r, iz = hw - r;   // inner rectangle of the corner centers
        if (Mathf.Abs(x) > ix && Mathf.Abs(z) > iz)
        {
            var o = new Vector2(Mathf.Sign(x) * ix, Mathf.Sign(z) * iz);
            var d = new Vector2(x, z) - o;
            if (d.magnitude > r) { d = d.normalized * r; x = o.x + d.x; z = o.y + d.y; }
        }
        return new Vector3(cx + x, p.y, cz + z);
    }
}
