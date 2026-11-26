using UnityEngine;

// Rink geometry. The scene is built for 1x1; the 3x3 rink is the same arena stretched about
// its center, so every gameplay position is written in 1x1 coordinates and mapped with W().
public static class Rink
{
    public static readonly Vector3 Center = new Vector3(634f, 0f, 400f);
    public static readonly Vector2 BigScale = new Vector2(1.4f, 1.3f);   // 3x3 rink, length x width
    public static Vector2 Scale = Vector2.one;

    public static Vector3 W(float x, float z, float y = 0f) =>
        new Vector3(Center.x + (x - Center.x) * Scale.x, y, Center.z + (z - Center.z) * Scale.y);

    public static Vector3 RedGoal => W(1656f, 400f);   // "gol": blue scores here
    public static Vector3 BlueGoal => W(-400f, 400f);  // "gol (1)": red scores here
    public static float MinX => W(-380f, 0).x;
    public static float MaxX => W(1636f, 0).x;
    public static float MinZ => W(0, -110f).z;
    public static float MaxZ => W(0, 910f).z;
}
