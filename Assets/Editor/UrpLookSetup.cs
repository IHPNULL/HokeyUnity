using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// One-shot: stadium lighting rig, reflection probe, post-processing volume and material polish for URP.
public static class UrpLookSetup
{
    const string Scene = "Assets/Scenes/MENU.unity";
    const string ProfilePath = "Assets/Settings/StadiumVolume.asset";

    public static string Run()
    {
        var log = new StringBuilder();
        var scene = EditorSceneManager.OpenScene(Scene);
        var jogando = ShotTool.Find("JOGANDO");
        var arenaCenter = new Vector3(634f, 10f, 400f); // floor plane center (measured)

        // --- Lights ---
        var oldSpot = ShotTool.Find("JOGANDO/Directional Light");
        if (oldSpot) { oldSpot.name = "Old Baked Spot (disabled)"; oldSpot.SetActive(false); log.AppendLine("disabled baked-only spot"); }

        var rig = ShotTool.Find("JOGANDO/Lighting");
        if (rig) Object.DestroyImmediate(rig);
        rig = new GameObject("Lighting");
        rig.transform.SetParent(jogando.transform, false);

        var key = new GameObject("Key Light").AddComponent<Light>();
        key.transform.SetParent(rig.transform, false);
        key.type = LightType.Directional;
        key.transform.rotation = Quaternion.Euler(58f, -30f, 0f);
        key.color = new Color(1f, 0.96f, 0.9f);
        key.intensity = 1.6f;
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.75f;
        key.lightmapBakeType = LightmapBakeType.Realtime;

        // Floodlights along the rink, two rows.
        int n = 0;
        foreach (float x in new[] { -250f, 634f, 1518f })
            foreach (float z in new[] { 150f, 650f })
            {
                var s = new GameObject("Flood " + (++n)).AddComponent<Light>();
                s.transform.SetParent(rig.transform, false);
                s.type = LightType.Spot;
                s.transform.position = new Vector3(x, 900f, z);
                s.transform.LookAt(new Vector3(x, 10f, 400f));
                s.spotAngle = 80f; s.innerSpotAngle = 50f;
                s.range = 2200f;
                s.intensity = 2.2e5f; // physical-ish falloff over ~900 units
                s.color = new Color(0.92f, 0.95f, 1f);
                s.shadows = (n == 2 || n == 5) ? LightShadows.Soft : LightShadows.None;
                s.lightmapBakeType = LightmapBakeType.Realtime;
            }
        log.AppendLine("lights: key + " + n + " floods");

        // Ambient: cool trilight fill so shadowed sides aren't black.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.36f, 0.40f, 0.48f);
        RenderSettings.ambientEquatorColor = new Color(0.22f, 0.22f, 0.25f);
        RenderSettings.ambientGroundColor = new Color(0.08f, 0.07f, 0.07f);
        RenderSettings.sun = key;

        // --- Reflection probe over the rink (realtime, rendered once on awake) ---
        var probeGo = new GameObject("Rink Reflection Probe");
        probeGo.transform.SetParent(rig.transform, false);
        probeGo.transform.position = arenaCenter + Vector3.up * 120f;
        var probe = probeGo.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        probe.size = new Vector3(2700f, 600f, 1200f);
        probe.boxProjection = true;
        probe.resolution = 256;
        probe.farClipPlane = 6000f;

        // --- Post-processing volume ---
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath); }
        var bloom = Add<Bloom>(profile);
        bloom.threshold.Override(1.0f); bloom.intensity.Override(0.7f); bloom.scatter.Override(0.65f);
        var tone = Add<Tonemapping>(profile);
        tone.mode.Override(TonemappingMode.ACES);
        var color = Add<ColorAdjustments>(profile);
        color.postExposure.Override(0.35f); color.contrast.Override(18f); color.saturation.Override(12f);
        var vig = Add<Vignette>(profile);
        vig.intensity.Override(0.28f); vig.smoothness.Override(0.45f);
        var dof = Add<DepthOfField>(profile);
        dof.mode.Override(DepthOfFieldMode.Off); // present but off; enabled for menu blur later
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(ProfilePath);

        var volGo = ShotTool.Find("Global Volume") ?? new GameObject("Global Volume");
        var vol = volGo.GetComponent<Volume>() ?? volGo.AddComponent<Volume>();
        vol.isGlobal = true; vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath); vol.priority = 0;

        // URP asset: let probes blend / box-project.
        var urp = (UniversalRenderPipelineAsset)GraphicsSettings.defaultRenderPipeline;
        var uso = new SerializedObject(urp);
        uso.FindProperty("m_ReflectionProbeBlending").boolValue = true;
        uso.FindProperty("m_ReflectionProbeBoxProjection").boolValue = true;
        uso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(urp);

        // Cameras: post-processing + SMAA on every scene camera.
        foreach (var cam in Resources.FindObjectsOfTypeAll<Camera>().Where(c => !EditorUtility.IsPersistent(c) && c.gameObject.scene.IsValid()))
        {
            var data = cam.GetComponent<UniversalAdditionalCameraData>() ?? cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            EditorUtility.SetDirty(data);
            log.AppendLine("camera " + cam.name + " post=on SMAA");
        }

        // --- Materials ---
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var rubber = MakeMat("Assets/Materials/Rubber.mat", lit, new Color(0.04f, 0.04f, 0.045f), 0f, 0.35f);
        int fixedNull = 0;
        foreach (var r in jogando.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials; bool ch = false;
            for (int i = 0; i < mats.Length; i++) if (mats[i] == null) { mats[i] = rubber; ch = true; fixedNull++; }
            if (ch) r.sharedMaterials = mats;
        }
        log.AppendLine("null material slots -> Rubber: " + fixedNull);

        Tune("Assets/Materials/download.mat", 0f, 0.62f);               // rink floor wood: glossy coat
        Tune("Assets/Materials/download (1).mat", 0f, 0.62f);
        Tune("Assets/3D/unidade/Materials/download (1).mat", 0f, 0.6f);
        Tune("Assets/3D/unidade/Materials/ce0ece26ee1348f2b1c453f314dc0a6e_18.mat", 0.45f, 0.72f); // robot paint
        Tune("Assets/Materials/ce0ece26ee1348f2b1c453f314dc0a6e_18.mat", 0.45f, 0.72f);
        Tune("Assets/3D/unidade/Materials/Chrome-red-painted-swatch-203-FB.mat", 0.6f, 0.8f);
        Tune("Assets/Materials/Chrome-red-painted-swatch-203-FB.mat", 0.6f, 0.8f);
        Tune("Assets/Materials/Extracted/qfull v3_Steel - Satin.mat", 0.9f, 0.6f);
        Tune("Assets/Materials/PuckStandIn.mat", 0f, 0.55f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        var reloaded = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        log.AppendLine("profile overrides persisted: " + string.Join(",", reloaded.components.Where(c => c != null).Select(c => c.GetType().Name)));
        log.AppendLine("probe blending=" + urp.reflectionProbeBlending + " boxProj=" + urp.reflectionProbeBoxProjection);
        return log.ToString();
    }

    static T Add<T>(VolumeProfile p) where T : VolumeComponent
    {
        if (p.TryGet<T>(out var c) && c != null) return c;
        c = p.Add<T>(true); c.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(c, p); EditorUtility.SetDirty(c);
        return c;
    }

    static Material MakeMat(string path, Shader s, Color c, float metal, float smooth)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(s); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(m); return m;
    }

    static void Tune(string path, float metal, float smooth)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) return;
        m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(m);
    }
}
