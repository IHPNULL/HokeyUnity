using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Off-screen renders of each game screen for review (no window needed).
public class ScreenProbe
{
    static GameObject Find(string path) => Resources.FindObjectsOfTypeAll<GameObject>().First(g =>
    {
        if (!g.scene.IsValid()) return false;
        string p = g.name; for (var t = g.transform.parent; t; t = t.parent) p = t.name + "/" + p;
        return p == path;
    });

    static void Shot(Camera cam, string file, int w = 1710, int h = 1074)
    {
        var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
        var prev = cam.targetTexture; var rect = cam.rect;
        cam.targetTexture = rt; cam.rect = new Rect(0, 0, 1, 1); cam.Render(); cam.targetTexture = prev; cam.rect = rect;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); RenderTexture.active = null;
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("PROBE_DIR") ?? System.IO.Path.GetTempPath(), file), tex.EncodeToPNG());
    }

    // Review tool, not a check: run explicitly with PROBE_DIR set.
    [UnityTest, Explicit]
    public IEnumerator Screens()
    {
        SceneManager.LoadScene("MENU");
        yield return null; yield return null;
        var cam = Find("MWNU/Main Camera").GetComponent<Camera>();
        Shot(cam, "menu.png");
        Find("MWNU/iniciar").SetActive(false); Find("MWNU/prejogo").SetActive(true);
        yield return null; yield return null;
        Shot(cam, "settings.png");
        Find("MWNU").SetActive(false); Find("JOGANDO").SetActive(true);
        yield return new WaitForSeconds(1.5f);
        Shot(Find("JOGANDO/unidades/unidadeverm/Camera (1)").GetComponent<Camera>(), "match_red.png");
        Shot(Find("JOGANDO/unidades/Unidadeazul/Camera (1)").GetComponent<Camera>(), "match_blue.png");
    }
}
