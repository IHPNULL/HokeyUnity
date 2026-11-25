using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-shot collision fixes for the arena (robot hulls, bounce materials, solid goal frames,
// scoring triggers inside the nets, contact offset for this world's scale).
public static class ColliderFix
{
    const string Scene = "Assets/Scenes/MENU.unity";

    public static string Run()
    {
        var log = new StringBuilder();
        var scene = EditorSceneManager.OpenScene(Scene);

        // 1. Robot hulls were triggers: the puck slid into the robots.
        foreach (var robot in new[] { "JOGANDO/unidades/unidadeverm", "JOGANDO/unidades/Unidadeazul" })
            foreach (var c in ShotTool.Find(robot).GetComponentsInChildren<Collider>(true))
                if (c.isTrigger) { c.isTrigger = false; log.AppendLine("hull solid: " + robot + "/" + c.name); }

        // 2. Bounce: puck and boards.
        var puckMat = PhysMat("Assets/Materials/Physics/Puck.physicsMaterial", 0.02f, 0.02f, 0.7f, PhysicsMaterialCombine.Minimum, PhysicsMaterialCombine.Maximum);
        var boardMat = PhysMat("Assets/Materials/Physics/Boards.physicsMaterial", 0.1f, 0.1f, 0.6f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Average);
        var puck = ShotTool.Find("JOGANDO/arena/PUCK");
        puck.GetComponent<Collider>().sharedMaterial = puckMat;
        var prb = puck.GetComponent<Rigidbody>();
        prb.linearDamping = 0.25f; prb.angularDamping = 0.5f;
        prb.interpolation = RigidbodyInterpolation.Interpolate;
        foreach (var n in new[] { "JOGANDO/arena/policarbonato", "JOGANDO/cenario externo/proteçao" })
            ShotTool.Find(n).GetComponent<Collider>().sharedMaterial = boardMat;
        foreach (var n in new[] { "JOGANDO/unidades/unidadeverm", "JOGANDO/unidades/Unidadeazul" })
            ShotTool.Find(n).GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.Interpolate;
        log.AppendLine("puck bounce 0.7, boards 0.6, puck drag 0.25, interpolation on");

        // 3. Goals: solid frame + nets, scoring trigger inside the net.
        foreach (var name in new[] { "gol", "gol (1)" })
        {
            var vis = ShotTool.Find("JOGANDO/arena/" + name + " Visual");
            var goal = ShotTool.Find("JOGANDO/arena/" + name);
            foreach (var part in new[] { "Post L", "Post R" })
            {
                var t = vis.transform.Find(part);
                if (!t.GetComponent<Collider>()) t.gameObject.AddComponent<CapsuleCollider>(); // primitive cylinder -> capsule fits
            }
            foreach (var part in new[] { "Crossbar", "Back Top", "Base L", "Base R", "Base Back" })
            {
                var t = vis.transform.Find(part);
                if (!t.GetComponent<Collider>()) t.gameObject.AddComponent<BoxCollider>();
            }
            // Nets: thin solid boxes so the puck stays in and robots can't drive through.
            var mouth = vis.transform.position;                    // goal line, floor level, center z
            float dir = mouth.x > 634f ? 1f : -1f;
            const float width = 540f, height = 110f, depth = 220f, wall = 12f;
            Box(vis, "Net Side L Col", new Vector3(dir * depth / 2, height / 2, -width / 2 - wall / 2), new Vector3(depth, height, wall));
            Box(vis, "Net Side R Col", new Vector3(dir * depth / 2, height / 2, width / 2 + wall / 2), new Vector3(depth, height, wall));
            Box(vis, "Net Back Col", new Vector3(dir * (depth + wall / 2), height / 2, 0), new Vector3(wall, height, width));
            Box(vis, "Net Top Col", new Vector3(dir * depth / 2, height + wall / 2, 0), new Vector3(depth, wall, width));

            // Scoring volume: trigger from just past the goal line to the back of the net.
            const float pastLine = 32f; // ~ puck radius: the whole puck is over the line
            Object.DestroyImmediate(goal.GetComponent<BoxCollider>());
            goal.transform.SetPositionAndRotation(new Vector3(mouth.x + dir * (pastLine + (depth - pastLine) / 2f), mouth.y + height / 2f, mouth.z), Quaternion.identity);
            goal.transform.localScale = Vector3.one;
            var trig = goal.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.size = new Vector3(depth - pastLine, height, width - 20f);
            log.AppendLine(name + ": frame+nets solid, scoring trigger x " + (mouth.x + dir * pastLine).ToString("F0") + ".." + (mouth.x + dir * depth).ToString("F0"));
        }

        // 4. Contact offset sized for this world (objects move ~28 units per physics step).
        Physics.defaultContactOffset = 1f;
        log.AppendLine("defaultContactOffset=" + Physics.defaultContactOffset);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    static void Box(GameObject root, string n, Vector3 localPos, Vector3 size)
    {
        var old = root.transform.Find(n); if (old) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject(n);
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = localPos;
        go.AddComponent<BoxCollider>().size = size;
    }

    static PhysicsMaterial PhysMat(string path, float dyn, float stat, float bounce, PhysicsMaterialCombine friction, PhysicsMaterialCombine bounceCombine)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials/Physics")) AssetDatabase.CreateFolder("Assets/Materials", "Physics");
        var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (m == null) { m = new PhysicsMaterial(); AssetDatabase.CreateAsset(m, path); }
        m.dynamicFriction = dyn; m.staticFriction = stat; m.bounciness = bounce;
        m.frictionCombine = friction; m.bounceCombine = bounceCombine;
        EditorUtility.SetDirty(m);
        return m;
    }
}
