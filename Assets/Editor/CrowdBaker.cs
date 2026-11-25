using UnityEditor;
using UnityEngine;

// Bakes the rigged Adam crowd figure into static meshes (idle + cheer poses) for instanced crowd rendering.
public static class CrowdBaker
{
    const string Fbx = "Assets/UnityTechnologies/Adam Character Pack/Adam/Crowd_LOD1.FBX";
    const string OutDir = "Assets/Crowd";

    // Arm pose = local euler offsets applied to the upper arms / forearms (degrees).
    public static string Bake(Vector3 idleUpper, Vector3 idleFore, Vector3 cheerUpper, Vector3 cheerFore, string previewFile = null)
    {
        if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets", "Crowd");
        var log = new System.Text.StringBuilder();
        var idle = BakePose("FanIdle", idleUpper, idleFore, log);
        var cheer = BakePose("FanCheer", cheerUpper, cheerFore, log);
        AssetDatabase.SaveAssets();
        if (previewFile != null) log.AppendLine(Preview(idle, cheer, previewFile));
        return log.ToString();
    }

    public static string SetupScene()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/MENU.unity");
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        Material M(string n, Color c, float metal, float smooth)
        {
            var p = OutDir + "/" + n + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, p); }
            m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
            m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
        }
        var red = M("FanRed", new Color(0.78f, 0.1f, 0.08f), 0.3f, 0.45f);
        var blue = M("FanBlue", new Color(0.08f, 0.25f, 0.8f), 0.3f, 0.45f);
        var grey = M("FanGrey", new Color(0.55f, 0.55f, 0.58f), 0.5f, 0.5f);

        var old = ShotTool.Find("JOGANDO/Crowd"); if (old) Object.DestroyImmediate(old);
        var go = new GameObject("Crowd");
        go.transform.SetParent(ShotTool.Find("JOGANDO").transform, false);
        var cs = go.AddComponent<CrowdSpawner>();
        cs.idleMesh = AssetDatabase.LoadAssetAtPath<Mesh>(OutDir + "/FanIdle.asset");
        cs.cheerMesh = AssetDatabase.LoadAssetAtPath<Mesh>(OutDir + "/FanCheer.asset");
        cs.redMat = red; cs.blueMat = blue; cs.greyMat = grey;
        cs.stands = ShotTool.Find("JOGANDO/cenario externo/bancadatop").GetComponent<Collider>();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "crowd set up; stands collider=" + cs.stands;
    }

    static Mesh BakePose(string name, Vector3 upper, Vector3 fore, System.Text.StringBuilder log)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        var go = (GameObject)Object.Instantiate(prefab);
        try
        {
            go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
            foreach (var side in new[] { "L", "R" })
            {
                float mirror = side == "L" ? 1f : -1f;
                var ua = Deep(go.transform, "Bip01 " + side + " UpperArm");
                var fa = Deep(go.transform, "Bip01 " + side + " Forearm");
                if (ua) ua.localRotation *= Quaternion.Euler(upper.x, upper.y * mirror, upper.z * mirror);
                if (fa) fa.localRotation *= Quaternion.Euler(fore.x, fore.y * mirror, fore.z * mirror);
            }
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            var baked = new Mesh { name = name };
            smr.BakeMesh(baked, true);
            // Express vertices in root space with feet at y = 0, facing +Z.
            var m = go.transform.worldToLocalMatrix * smr.transform.localToWorldMatrix;
            var v = baked.vertices; var n = baked.normals;
            float minY = float.MaxValue;
            for (int i = 0; i < v.Length; i++) { v[i] = m.MultiplyPoint3x4(v[i]); n[i] = m.MultiplyVector(n[i]).normalized; minY = Mathf.Min(minY, v[i].y); }
            for (int i = 0; i < v.Length; i++) v[i].y -= minY;
            baked.vertices = v; baked.normals = n; baked.RecalculateBounds(); baked.RecalculateTangents();
            var path = OutDir + "/" + name + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(baked, path);
            log.AppendLine(name + " verts=" + v.Length + " bounds=" + baked.bounds.size.ToString("F2"));
            return baked;
        }
        finally { Object.DestroyImmediate(go); }
    }

    static Transform Deep(Transform t, string n)
    {
        if (t.name == n) return t;
        foreach (Transform c in t) { var r = Deep(c, n); if (r) return r; }
        return null;
    }

    static string Preview(Mesh idle, Mesh cheer, string file)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/UnityTechnologies/Adam Character Pack/Adam/Materials/Adam_LOD_02.mat");
        var root = new GameObject("__CrowdPreview");
        try
        {
            void Make(Mesh mesh, float x, float yaw)
            {
                var g = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                g.transform.SetParent(root.transform);
                g.transform.position = new Vector3(x, 5000, 0); g.transform.rotation = Quaternion.Euler(0, yaw, 0);
                g.GetComponent<MeshFilter>().sharedMesh = mesh; g.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            Make(idle, -1.2f, 180); Make(cheer, 0f, 180); Make(cheer, 1.2f, 90);
            var l = new GameObject("l").AddComponent<Light>(); l.transform.SetParent(root.transform); l.type = LightType.Directional; l.transform.rotation = Quaternion.Euler(40, 160, 0);
            return ShotTool.CaptureFrom(new Vector3(0, 5001, -4.2f), new Vector3(0, 5000.9f, 0), file, "", "", 40);
        }
        finally { Object.DestroyImmediate(root); }
    }
}
