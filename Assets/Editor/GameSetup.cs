using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// One-shot: replaces the old sprite menu (MWNU) and text HUD (JOGANDO/Canvas, WIN) with the
// "Game" root: MatchManager + GameUI + an orbiting menu camera over the arena.
public static class GameSetup
{
    public static string Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MENU.unity");
        var log = new System.Text.StringBuilder();

        foreach (var r in scene.GetRootGameObjects())
            if (r.name == "Game") Object.DestroyImmediate(r);

        var game = new GameObject("Game");
        game.AddComponent<MatchManager>();
        game.AddComponent<GameUI>();

        var camGo = new GameObject("Menu Camera");
        camGo.transform.SetParent(game.transform, false);
        var cam = camGo.AddComponent<Camera>();
        var blueCam = ShotTool.Find("JOGANDO/unidades/Unidadeazul").GetComponentInChildren<Camera>(true);
        cam.CopyFrom(blueCam);
        cam.rect = new Rect(0, 0, 1, 1); cam.fieldOfView = 50f; cam.nearClipPlane = 5f; cam.farClipPlane = 20000f;
        camGo.transform.position = new Vector3(634, 630, -1050); camGo.transform.LookAt(new Vector3(634, 50, 400));
        camGo.AddComponent<AudioListener>();
        var src = blueCam.GetComponent<UniversalAdditionalCameraData>();
        var dst = camGo.AddComponent<UniversalAdditionalCameraData>();
        dst.renderPostProcessing = true;
        dst.antialiasing = src ? src.antialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        dst.antialiasingQuality = AntialiasingQuality.High;
        log.AppendLine("Game root + Menu Camera");

        var mwnu = ShotTool.Find("MWNU"); mwnu.SetActive(false);
        log.AppendLine("MWNU disabled (old sprite menu)");

        var oldHud = ShotTool.Find("JOGANDO/Canvas");
        if (oldHud) { Object.DestroyImmediate(oldHud); log.AppendLine("old HUD canvas (WIN, marcador texts) removed"); }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return log.ToString();
    }
}
