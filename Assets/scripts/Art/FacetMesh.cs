using System.Collections.Generic;
using UnityEngine;

// Flat-shaded ("faceted") low-poly mesh builder shared by the editor bakers and runtime props.
// Every face gets its own vertices and a flat normal; winding is fixed automatically so faces point
// away from the shape's centre (or along an explicit normal for flat decals). Several submeshes
// (one material each) are supported: call Sub(i) before adding geometry.
public class FacetMesh
{
    readonly List<Vector3> v = new(), n = new();
    readonly List<Vector2> uv = new();
    readonly List<List<int>> subs = new() { new List<int>() };
    List<int> t;

    public FacetMesh() { t = subs[0]; }

    public FacetMesh Sub(int i)
    {
        while (subs.Count <= i) subs.Add(new List<int>());
        t = subs[i];
        return this;
    }

    void Emit(Vector3 nrm, Vector3[] p)
    {
        int i0 = v.Count;
        foreach (var q in p) { v.Add(q); n.Add(nrm); uv.Add(new Vector2(q.x + q.z, q.y)); }
        for (int i = 1; i < p.Length - 1; i++) { t.Add(i0); t.Add(i0 + i); t.Add(i0 + i + 1); }
    }

    // Optional: pick each face's submesh from its outward normal (e.g. paint the upward faces).
    // Set it while adding geometry, then clear it with PaintBy(null).
    System.Func<Vector3, int> paintBy;
    public FacetMesh PaintBy(System.Func<Vector3, int> f) { paintBy = f; return this; }

    // Newell normal: robust for slightly non-planar or partly degenerate polygons.
    static Vector3 Normal(Vector3[] p)
    {
        var nrm = Vector3.zero;
        for (int i = 0; i < p.Length; i++) { var a = p[i]; var b = p[(i + 1) % p.Length]; nrm += new Vector3((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y)); }
        return nrm;
    }

    // Convex polygon facing away from `centre`.
    public void Poly(Vector3 centre, params Vector3[] p)
    {
        var nrm = Normal(p);
        if (nrm.sqrMagnitude < 1e-14f) return;   // degenerate (zero area)
        var faceC = Vector3.zero; foreach (var q in p) faceC += q; faceC /= p.Length;
        if (Vector3.Dot(nrm, faceC - centre) < 0) { System.Array.Reverse(p); nrm = -nrm; }
        if (paintBy != null) { var keep = t; Sub(paintBy(nrm.normalized)); Emit(nrm.normalized, p); t = keep; }
        else Emit(nrm.normalized, p);
    }

    static Vector3 Centroid(Vector3[] r) { var c = Vector3.zero; foreach (var q in r) c += q; return c / r.Length; }

    // Solid through a sequence of convex rings (same vertex count, same order), capped at both ends.
    // Each side face points away from the axis between its two rings, so tapered and bent lofts work.
    public void Loft(params Vector3[][] rings)
    {
        int n = rings[0].Length;
        for (int i = 0; i < rings.Length - 1; i++)
        {
            var c = (Centroid(rings[i]) + Centroid(rings[i + 1])) / 2;
            for (int k = 0; k < n; k++)
            {
                int j = (k + 1) % n;
                var quad = new List<Vector3>(4);
                foreach (var q in new[] { rings[i][k], rings[i][j], rings[i + 1][j], rings[i + 1][k] })
                    if (quad.Count == 0 || (quad[^1] - q).sqrMagnitude > 1e-12f) quad.Add(q);
                if (quad.Count > 1 && (quad[0] - quad[^1]).sqrMagnitude < 1e-12f) quad.RemoveAt(quad.Count - 1);
                if (quad.Count >= 3) Poly(c, quad.ToArray());
            }
        }
        Poly(Centroid(rings[1]), (Vector3[])rings[0].Clone());
        Poly(Centroid(rings[^2]), (Vector3[])rings[^1].Clone());
    }

    // Box with every edge chamfered by `ch`, rotated by `rot` around its centre.
    public void CBox(Vector3 c, Vector3 size, float ch, Quaternion rot)
    {
        float hx = size.x / 2, hy = size.y / 2, hz = size.z / 2;
        ch = Mathf.Min(ch, hx * 0.9f, hy * 0.9f, hz * 0.9f);
        Vector3[] Oct(float x, float iy, float iz, float cut)
        {
            float y = hy - iy, z = hz - iz;
            var r = new[] { new Vector3(x, -y + cut, -z), new Vector3(x, y - cut, -z), new Vector3(x, y, -z + cut), new Vector3(x, y, z - cut),
                            new Vector3(x, y - cut, z), new Vector3(x, -y + cut, z), new Vector3(x, -y, z - cut), new Vector3(x, -y, -z + cut) };
            for (int i = 0; i < r.Length; i++) r[i] = c + rot * r[i];
            return r;
        }
        Loft(Oct(-hx, ch, ch, ch * 0.5f), Oct(-hx + ch, 0, 0, ch), Oct(hx - ch, 0, 0, ch), Oct(hx, ch, ch, ch * 0.5f));
    }
    public void CBox(Vector3 c, Vector3 size, float ch) => CBox(c, size, ch, Quaternion.identity);

    // Faceted cylinder from a to b.
    public void Tube(Vector3 a, Vector3 b, float r, int seg, float startDeg = 0)
    {
        float len = (b - a).magnitude;
        Lathe(a, b - a, new[] { new Vector2(0, 0), new Vector2(r, 0), new Vector2(r, len), new Vector2(0, len) }, seg, startDeg);
    }

    // Convex polygon facing along `normal` (decals, display segments).
    public void Flat(Vector3 normal, params Vector3[] p)
    {
        var nrm = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
        if (Vector3.Dot(nrm, normal) < 0) System.Array.Reverse(p);
        Emit(normal.normalized, p);
    }

    // Tapered box between two points; r = half extents (side, depth) at each end.
    public void Seg(Vector3 a, Vector3 b, Vector2 ra, Vector2 rb, Vector3? depthHint = null)
    {
        var up = (b - a).normalized;
        var fwd = Vector3.ProjectOnPlane(depthHint ?? Vector3.forward, up);
        if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(Vector3.right, up);
        fwd.Normalize();
        var right = Vector3.Cross(up, fwd);
        Vector3 C(Vector3 c, Vector2 r, int sx, int sz) => c + right * (r.x * sx) + fwd * (r.y * sz);
        var q = new[] { C(a, ra, -1, -1), C(a, ra, 1, -1), C(a, ra, 1, 1), C(a, ra, -1, 1), C(b, rb, -1, -1), C(b, rb, 1, -1), C(b, rb, 1, 1), C(b, rb, -1, 1) };
        var centre = (a + b) / 2;
        Poly(centre, q[0], q[1], q[2], q[3]); Poly(centre, q[4], q[5], q[6], q[7]);
        for (int i = 0; i < 4; i++) { int j = (i + 1) % 4; Poly(centre, q[i], q[j], q[j + 4], q[i + 4]); }
    }

    public void Box(Vector3 c, Vector3 size) =>
        Seg(c - Vector3.up * size.y / 2, c + Vector3.up * size.y / 2, new Vector2(size.x, size.z) / 2, new Vector2(size.x, size.z) / 2);

    // Box with its 8 edges along X and Y chamfered by `ch` toward the ±Z faces (reads as a bevelled panel).
    public void Panel(Vector3 c, Vector3 size, float ch)
    {
        float hx = size.x / 2, hy = size.y / 2, hz = size.z / 2;
        Vector3[] Ring(float z, float inset) => new[]
        {
            c + new Vector3(-hx + inset, -hy + inset, z), c + new Vector3(hx - inset, -hy + inset, z),
            c + new Vector3(hx - inset, hy - inset, z), c + new Vector3(-hx + inset, hy - inset, z)
        };
        var f0 = Ring(-hz, ch); var m0 = Ring(-hz + ch, 0); var m1 = Ring(hz - ch, 0); var f1 = Ring(hz, ch);
        Poly(c, f0[0], f0[1], f0[2], f0[3]); Poly(c, f1[0], f1[1], f1[2], f1[3]);
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            Poly(c, f0[i], f0[j], m0[j], m0[i]);
            Poly(c, m0[i], m0[j], m1[j], m1[i]);
            Poly(c, m1[i], m1[j], f1[j], f1[i]);
        }
    }

    // Surface of revolution around +Y; profile = (radius, height), a radius of 0 closes that end.
    public void Lathe(Vector3 o, Vector2[] prof, int seg, float startDeg) => Lathe(o, Vector3.up, prof, seg, startDeg);

    // Surface of revolution around `axis` through `o`; profile = (radius, distance along the axis).
    public void Lathe(Vector3 o, Vector3 axis, Vector2[] prof, int seg, float startDeg = 0)
    {
        var q = Quaternion.FromToRotation(Vector3.up, axis.normalized);
        var centre = o + q * Vector3.up * (prof[0].y + prof[^1].y) / 2;
        Vector3 P(Vector2 pr, int k) { float a = (startDeg + 360f * k / seg) * Mathf.Deg2Rad; return o + q * new Vector3(Mathf.Cos(a) * pr.x, pr.y, Mathf.Sin(a) * pr.x); }
        for (int i = 0; i < prof.Length - 1; i++)
            for (int k = 0; k < seg; k++)
            {
                Vector3 a0 = P(prof[i], k), a1 = P(prof[i], k + 1), b0 = P(prof[i + 1], k), b1 = P(prof[i + 1], k + 1);
                if (prof[i].x <= 0) Poly(centre, a0, b0, b1);
                else if (prof[i + 1].x <= 0) Poly(centre, a0, a1, b0);
                else Poly(centre, a0, a1, b1, b0);
            }
    }

    public int Triangles { get { int c = 0; foreach (var s in subs) c += s.Count / 3; return c; } }

    public Mesh ToMesh(string name, Mesh into = null)
    {
        var m = into ? into : new Mesh();
        m.Clear();
        m.name = name;
        if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
        m.subMeshCount = subs.Count;
        for (int i = 0; i < subs.Count; i++) m.SetTriangles(subs[i], i);
        m.RecalculateBounds(); m.RecalculateTangents();
        return m;
    }
}

// 7-segment LED digits drawn as flat hexagonal bars into a FacetMesh (one submesh for lit
// segments, one for unlit ones, so the dark "ghost" segments show like a real LED board).
public static class SevenSegment
{
    // Bits a..g = 1,2,4,8,16,32,64 (a top, b top-right, c bottom-right, d bottom, e bottom-left, f top-left, g middle).
    static readonly int[] Masks = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };
    public const int Dash = 0x40;

    // Digit with its bottom-left at `o` in the plane spanned by `right`/`up`, facing `normal`.
    public static void Digit(FacetMesh m, int mask, int litSub, int dimSub, Vector3 o, Vector3 right, Vector3 up, Vector3 normal, float w, float h, float thick, float slant = 0.12f)
    {
        float gap = thick * 0.18f;
        Vector3 P(float x, float y) => o + right * (x + y * slant) + up * y;
        void Bar(int bit, float x0, float y0, float x1, float y1)   // segment from (x0,y0) to (x1,y1), pointed ends
        {
            m.Sub((mask & bit) != 0 ? litSub : dimSub);
            var d = new Vector2(x1 - x0, y1 - y0).normalized; var s = new Vector2(-d.y, d.x) * (thick / 2);
            Vector2 a = new Vector2(x0, y0) + d * gap, b = new Vector2(x1, y1) - d * gap;
            m.Flat(normal, P(a.x, a.y), P(a.x + d.x * thick / 2 + s.x, a.y + d.y * thick / 2 + s.y), P(b.x - d.x * thick / 2 + s.x, b.y - d.y * thick / 2 + s.y),
                           P(b.x, b.y), P(b.x - d.x * thick / 2 - s.x, b.y - d.y * thick / 2 - s.y), P(a.x + d.x * thick / 2 - s.x, a.y + d.y * thick / 2 - s.y));
        }
        float h2 = h / 2;
        Bar(1, 0, h, w, h);       // a
        Bar(2, w, h, w, h2);      // b
        Bar(4, w, h2, w, 0);      // c
        Bar(8, 0, 0, w, 0);       // d
        Bar(16, 0, h2, 0, 0);     // e
        Bar(32, 0, h, 0, h2);     // f
        Bar(64, 0, h2, w, h2);    // g
    }

    public static int MaskOf(int digit) => Masks[Mathf.Clamp(digit, 0, 9)];

    // Square dot (colon, indicators) centred at (x, y) in the same plane.
    public static void Dot(FacetMesh m, int sub, Vector3 o, Vector3 right, Vector3 up, Vector3 normal, float x, float y, float size)
    {
        m.Sub(sub);
        float r = size / 2;
        m.Flat(normal, o + right * (x - r) + up * (y - r), o + right * (x + r) + up * (y - r), o + right * (x + r) + up * (y + r), o + right * (x - r) + up * (y + r));
    }
}
