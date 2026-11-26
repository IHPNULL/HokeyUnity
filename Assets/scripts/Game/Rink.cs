using UnityEngine;

// Rink geometry. The scene's rink is stretched about its center (1x1 a bit, 3x3 more), so every
// gameplay position is written in the scene's original coordinates and mapped with W().
// Goals, pillars and round markings keep their real size (KeepSize).
public static class Rink
{
    public static readonly Vector3 Center = new Vector3(634f, 0f, 400f);
    public static readonly Vector2 NormalScale = new Vector2(1.25f, 1.2f);   // 1x1 rink, length x width
    public static readonly Vector2 BigScale = new Vector2(1.6f, 1.45f);      // 3x3 rink (fits inside the stands)
    public static Vector2 Scale = Vector2.one;

    public static Vector3 W(float x, float z, float y = 0f) =>
        new Vector3(Center.x + (x - Center.x) * Scale.x, y, Center.z + (z - Center.z) * Scale.y);

    public static Vector3 RedGoal => W(1656f, 400f);   // "gol": blue scores here
    public static Vector3 BlueGoal => W(-400f, 400f);  // "gol (1)": red scores here
    public static float MinX => W(-380f, 0).x;
    public static float MaxX => W(1636f, 0).x;
    public static float MinZ => W(0, -110f).z;
    public static float MaxZ => W(0, 910f).z;

    // Objects that keep their real size when the rink stretches (goals, pillars, round markings).
    // Register while the rink is unstretched; ApplyKeeps() after every stretch. keepX / keepZ pick
    // which world directions to undo (a line keeps its thickness but still spans the rink).
    struct Keep { public Transform t; public Vector3 baseScale; public bool keepX, keepZ; }
    static readonly System.Collections.Generic.List<Keep> keeps = new System.Collections.Generic.List<Keep>();

    public static void KeepSize(Transform t, bool keepX = true, bool keepZ = true)
    {
        if (!t) return;
        keeps.RemoveAll(k => !k.t || k.t == t);
        keeps.Add(new Keep { t = t, baseScale = t.localScale, keepX = keepX, keepZ = keepZ });
        ApplyKeeps();
    }

    public static void ApplyKeeps()
    {
        keeps.RemoveAll(k => !k.t);
        foreach (var k in keeps)
        {
            var s = k.baseScale;
            for (int i = 0; i < 3; i++)
            {
                var axis = k.t.rotation * (i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward);
                if (k.keepX && Mathf.Abs(axis.x) > 0.9f) s[i] /= Scale.x;
                if (k.keepZ && Mathf.Abs(axis.z) > 0.9f) s[i] /= Scale.y;
            }
            k.t.localScale = s;
        }
    }
}
