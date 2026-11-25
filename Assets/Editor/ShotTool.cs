using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Editor helper: render a scene camera to a PNG without touching the saved scene.
// show: '|'-separated hierarchy paths to activate temporarily; hide: paths to deactivate temporarily.
public static class ShotTool
{
    public static string Capture(string cameraPath, string file, string show = "", string hide = "", int w = 1710, int h = 1074)
    {
        var toggled = new List<(GameObject go, bool was)>();
        void Set(string paths, bool active)
        {
            foreach (var p in paths.Split('|').Where(s => s.Length > 0))
            {
                var go = Find(p);
                if (go == null) throw new System.Exception("not found: " + p);
                toggled.Add((go, go.activeSelf));
                go.SetActive(active);
            }
        }
        try
        {
            Set(show, true); Set(hide, false);
            var cam = Find(cameraPath)?.GetComponent<Camera>() ?? throw new System.Exception("no camera at " + cameraPath);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var prevT = cam.targetTexture; var prevR = cam.rect;
            cam.targetTexture = rt; cam.rect = new Rect(0, 0, 1, 1);
            cam.Render();
            cam.targetTexture = prevT; cam.rect = prevR;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex); rt.Release();
            return "wrote " + file;
        }
        finally
        {
            for (int i = toggled.Count - 1; i >= 0; i--) toggled[i].go.SetActive(toggled[i].was);
        }
    }

    public static GameObject Find(string path) =>
        Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g =>
        {
            if (!g.scene.IsValid() || EditorUtility.IsPersistent(g)) return false;
            string p = g.name; for (var t = g.transform.parent; t; t = t.parent) p = t.name + "/" + p;
            return p == path;
        });
}
