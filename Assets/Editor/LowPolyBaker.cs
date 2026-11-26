using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Builds the game's light "faceted" low-poly meshes and swaps them into the scene:
//  - crowd fans (idle + cheer) ~130 tris instead of the 1,732-tri Adam bake (≈3,700 fans on screen)
//  - stadium chair ~60 tris instead of the 367,610-tri CAD export (cadeira.fbx)
// Run: LowPolyBaker.BakeAll() (menu: Hockey/Bake Low-Poly Models). Re-running overwrites the assets.
public static class LowPolyBaker
{
    const string CrowdDir = "Assets/Crowd", LowDir = "Assets/3D/LowPoly";
    const string ChairsPath = "JOGANDO/cenario externo/bancadatop/cadeiras";

    [MenuItem("Hockey/Bake Low-Poly Models")]
    public static void BakeMenu() => Debug.Log(BakeAll());

    public static string BakeAll()
    {
        if (!AssetDatabase.IsValidFolder(LowDir)) AssetDatabase.CreateFolder("Assets/3D", "LowPoly");
        var log = new System.Text.StringBuilder();
        var idle = Save(Fan(false), CrowdDir + "/FanIdleLow.asset", log);
        var cheer = Save(Fan(true), CrowdDir + "/FanCheerLow.asset", log);
        var chair = Save(Chair(), LowDir + "/ChairLow.asset", log);

        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/MENU.unity");
        foreach (var cs in Object.FindObjectsByType<CrowdSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { cs.idleMesh = idle; cs.cheerMesh = cheer; EditorUtility.SetDirty(cs); log.AppendLine("crowd -> low-poly fans"); }
        var chairs = ShotTool.Find(ChairsPath);
        int swapped = 0;
        if (chairs)
            foreach (var mf in chairs.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh && mf.sharedMesh != chair) { mf.sharedMesh = chair; EditorUtility.SetDirty(mf); swapped++; }
        log.AppendLine("chairs swapped: " + swapped);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    static Mesh Save(Mesh m, string path, System.Text.StringBuilder log)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing) { existing.Clear(); EditorUtility.CopySerialized(m, existing); existing.name = m.name; m = existing; }
        else AssetDatabase.CreateAsset(m, path);
        EditorUtility.SetDirty(m);
        log.AppendLine(m.name + ": tris=" + m.triangles.Length / 3 + " verts=" + m.vertexCount + " size=" + m.bounds.size.ToString("F2"));
        return m;
    }

    // ---- models ---------------------------------------------------------------------------------

    // Stylized fan, metres, feet at y = 0, facing +Z (CrowdSpawner scales x110 and faces the rink).
    public static Mesh Fan(bool cheer)
    {
        var b = new FacetMesh();
        for (int s = -1; s <= 1; s += 2)
        {
            b.Seg(new Vector3(s * 0.09f, 0.06f, 0), new Vector3(s * 0.10f, 0.90f, 0), new Vector2(0.06f, 0.07f), new Vector2(0.08f, 0.085f));   // leg
            b.Box(new Vector3(s * 0.09f, 0.04f, 0.04f), new Vector3(0.12f, 0.08f, 0.25f));                                                         // shoe
            var shoulder = new Vector3(s * 0.225f, 1.37f, 0);
            if (cheer) b.Seg(shoulder, new Vector3(s * 0.40f, 1.98f, 0.05f), new Vector2(0.055f, 0.055f), new Vector2(0.045f, 0.045f));          // arms up
            else b.Seg(shoulder, new Vector3(s * 0.275f, 0.84f, 0.03f), new Vector2(0.055f, 0.055f), new Vector2(0.042f, 0.042f));             // arms down
        }
        b.Seg(new Vector3(0, 0.84f, 0), new Vector3(0, 1.43f, 0), new Vector2(0.15f, 0.095f), new Vector2(0.205f, 0.11f));                   // torso, wide shoulders
        b.Seg(new Vector3(0, 1.42f, 0), new Vector3(0, 1.50f, 0.01f), new Vector2(0.045f, 0.045f), new Vector2(0.04f, 0.04f));              // neck
        b.Lathe(Vector3.zero, new[] { new Vector2(0, 1.47f), new Vector2(0.075f, 1.50f), new Vector2(0.10f, 1.60f), new Vector2(0.09f, 1.71f), new Vector2(0.05f, 1.79f), new Vector2(0, 1.82f) }, 6, 30f); // head
        if (cheer) b.Box(new Vector3(0, 1.98f, 0.05f), new Vector3(0.86f, 0.11f, 0.025f));                                                    // team scarf held up
        return b.ToMesh(cheer ? "FanCheerLow" : "FanIdleLow");
    }

    // Stadium seat shell in cadeira.fbx's local frame (seat toward +X, back at x ≈ -0.1, up +Y, width along Z).
    public static Mesh Chair()
    {
        var b = new FacetMesh();
        const float w = 0.052f;
        b.Seg(new Vector3(-0.002f, 0.004f, 0), new Vector3(-0.088f, 0.012f, 0), new Vector2(w, 0.010f), new Vector2(w, 0.010f), Vector3.up);       // seat pan (front lip lower)
        b.Seg(new Vector3(-0.088f, 0.012f, 0), new Vector3(-0.104f, 0.040f, 0), new Vector2(w, 0.010f), new Vector2(w, 0.009f), Vector3.up);       // curved joint
        b.Seg(new Vector3(-0.104f, 0.040f, 0), new Vector3(-0.106f, 0.115f, 0), new Vector2(w, 0.009f), new Vector2(w * 0.96f, 0.008f), Vector3.right); // backrest
        b.Seg(new Vector3(-0.106f, 0.115f, 0), new Vector3(-0.103f, 0.131f, 0), new Vector2(w * 0.96f, 0.008f), new Vector2(w * 0.7f, 0.007f), Vector3.right); // rounded top
        b.Box(new Vector3(-0.05f, -0.002f, 0), new Vector3(0.03f, 0.008f, 0.07f));                                                                // mount
        return b.ToMesh("ChairLow");
    }
}
