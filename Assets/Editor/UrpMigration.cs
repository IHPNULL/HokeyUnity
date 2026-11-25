using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-shot Built-in -> URP migration for Omega Hockey.
// Each phase is called separately (package install causes a domain reload) and returns a report.
public static partial class UrpMigration
{
    const string Scene = "Assets/Scenes/MENU.unity";
    const string UrpPackage = "com.unity.render-pipelines.universal";

    // ---------- Phase A: install URP ----------
    public static string InstallUrp()
    {
        UnityEditor.PackageManager.Client.Add(UrpPackage);
        return "requested " + UrpPackage + " (domain reload follows)";
    }

    // ---------- Phase B: extract FBX-embedded materials so they can be converted ----------
    public static string ExtractFbxMaterials()
    {
        var log = new StringBuilder();
        const string outDir = "Assets/Materials/Extracted";
        if (!AssetDatabase.IsValidFolder(outDir)) AssetDatabase.CreateFolder("Assets/Materials", "Extracted");
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("Standard Assets") || !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
            var embedded = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().ToArray();
            foreach (var m in embedded)
            {
                var safe = Path.GetFileNameWithoutExtension(path) + "_" + string.Concat(m.name.Split(Path.GetInvalidFileNameChars())).Replace("#", "");
                var dest = AssetDatabase.GenerateUniqueAssetPath(outDir + "/" + safe + ".mat");
                var err = AssetDatabase.ExtractAsset(m, dest);
                log.AppendLine("EXTRACT " + path + " :: " + m.name + " -> " + dest + (string.IsNullOrEmpty(err) ? "" : " ERROR " + err));
            }
            if (embedded.Length > 0) AssetDatabase.WriteImportSettingsIfDirty(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // ---------- Phase C: URP asset + renderer, assign to Graphics and every Quality level ----------
    public static string SetupPipeline()
    {
        var log = new StringBuilder();
        const string dir = "Assets/Settings";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets", "Settings");

        var rendererPath = dir + "/OmegaRenderer.asset";
        var rendererData = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(rendererPath);
        if (rendererData == null)
        {
            rendererData = ScriptableObject.CreateInstance<UnityEngine.Rendering.Universal.UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, rendererPath);
        }
        if (rendererData.postProcessData == null)
            rendererData.postProcessData = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.PostProcessData>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
        // SSAO for contact shading under robots/goals.
        if (!rendererData.rendererFeatures.Any(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion"))
        {
            var ssaoType = typeof(UnityEngine.Rendering.Universal.UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
            var ssao = (UnityEngine.Rendering.Universal.ScriptableRendererFeature)ScriptableObject.CreateInstance(ssaoType);
            ssao.name = "SSAO";
            AssetDatabase.AddObjectToAsset(ssao, rendererData);
            rendererData.rendererFeatures.Add(ssao);
            var so = new SerializedObject(rendererData);
            var map = so.FindProperty("m_RendererFeatureMap");
            map.arraySize++;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out _, out long fid);
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = fid;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorUtility.SetDirty(rendererData);

        var assetPath = dir + "/OmegaURP.asset";
        var urp = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(assetPath);
        if (urp == null)
        {
            urp = UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(urp, assetPath);
        }
        // The arena is ~2600 units long: shadow distance and depth must cover it.
        urp.shadowDistance = 4000f;
        urp.shadowCascadeCount = 4;
        urp.supportsHDR = true;
        urp.msaaSampleCount = 4;
        urp.supportsCameraDepthTexture = true;
        urp.supportsCameraOpaqueTexture = true;
        var uso = new SerializedObject(urp);
        void SetB(string n, bool v) { var p = uso.FindProperty(n); if (p != null) p.boolValue = v; else log.AppendLine("WARN no " + n); }
        void SetI(string n, int v) { var p = uso.FindProperty(n); if (p != null) p.intValue = v; else log.AppendLine("WARN no " + n); }
        SetB("m_MainLightShadowsSupported", true);
        SetI("m_MainLightShadowmapResolution", 4096);
        SetI("m_AdditionalLightsRenderingMode", 1); // per pixel
        SetI("m_AdditionalLightsPerObjectLimit", 8);
        SetB("m_AdditionalLightShadowsSupported", true);
        SetB("m_SoftShadowsSupported", true);
        uso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(urp);

        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = urp;
        int current = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = urp;
        }
        QualitySettings.SetQualityLevel(current, false);
        AssetDatabase.SaveAssets();

        log.AppendLine("graphics=" + (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline ? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.name : "NULL"));
        for (int i = 0; i < QualitySettings.names.Length; i++)
            log.AppendLine("quality " + i + " " + QualitySettings.names[i] + " = " + (QualitySettings.GetRenderPipelineAssetAt(i) ? QualitySettings.GetRenderPipelineAssetAt(i).name : "inherits"));
        log.AppendLine("renderers=" + urp.rendererDataList.Length + " postProcessData=" + (rendererData.postProcessData != null) + " features=" + string.Join(",", rendererData.rendererFeatures.Select(f => f ? f.name : "NULL")));
        return log.ToString();
    }

    // ---------- Phase D: convert materials explicitly (named target shader, properties copied) ----------
    public static string ConvertMaterials()
    {
        var log = new StringBuilder();
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) return "URP Lit shader not found";

        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("Standard Assets")) continue;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null) continue;
            var src = mat.shader.name;
            if (src.StartsWith("Universal Render Pipeline/")) { log.AppendLine("SKIP " + path + " already " + src); continue; }

            // Snapshot while still on the source shader.
            Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Vector2 scale = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 offset = mat.HasProperty("_MainTex") ? mat.GetTextureOffset("_MainTex") : Vector2.zero;
            Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            Texture normal = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            Texture metalMap = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
            Texture emisMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
            Color emis = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
            float gloss = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;
            int mode = mat.HasProperty("_Mode") ? (int)mat.GetFloat("_Mode") : 0;
            bool isGlass = src.StartsWith("FX/Glass");

            mat.shader = lit;
            if (mainTex) { mat.SetTexture("_BaseMap", mainTex); mat.SetTextureScale("_BaseMap", scale); mat.SetTextureOffset("_BaseMap", offset); }
            mat.SetColor("_BaseColor", color);
            if (normal) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
            if (metalMap) { mat.SetTexture("_MetallicGlossMap", metalMap); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", gloss);
            if (emisMap || emis.maxColorComponent > 0.01f)
            {
                if (emisMap) mat.SetTexture("_EmissionMap", emisMap);
                mat.SetColor("_EmissionColor", emis);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            // Standard _Mode: 0 opaque, 1 cutout, 2 fade, 3 transparent. Glass -> clear transparent.
            if (isGlass || mode == 2 || mode == 3)
            {
                if (isGlass) { var c = mat.GetColor("_BaseColor"); c.a = 0.18f; mat.SetColor("_BaseColor", c); mat.SetFloat("_Smoothness", 0.95f); }
                MakeTransparent(mat, premultiply: mode == 3 || isGlass);
            }
            else if (mode == 1)
            {
                mat.SetFloat("_AlphaClip", 1); mat.EnableKeyword("_ALPHATEST_ON");
                if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", mat.GetFloat("_Cutoff"));
            }
            EditorUtility.SetDirty(mat);

            bool texOk = mainTex == null || mat.GetTexture("_BaseMap") == mainTex;
            log.AppendLine((texOk ? "OK   " : "FAIL ") + path + " : " + src + " -> " + mat.shader.name + (mainTex ? " tex=" + mainTex.name : "") + " color=" + color + " mode=" + mode);
        }

        // Renderers using Unity's built-in Default-Material (Standard) would render magenta under URP.
        var defaultLitPath = "Assets/Materials/DefaultLit.mat";
        var defaultLit = AssetDatabase.LoadAssetAtPath<Material>(defaultLitPath);
        if (defaultLit == null) { defaultLit = new Material(lit) { name = "DefaultLit" }; AssetDatabase.CreateAsset(defaultLit, defaultLitPath); }
        var scene = EditorSceneManager.OpenScene(Scene);
        int replaced = 0;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var mats = r.sharedMaterials; bool changed = false;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && AssetDatabase.GetAssetPath(mats[i]) == "Resources/unity_builtin_extra" && mats[i].shader.name == "Standard")
                { mats[i] = defaultLit; changed = true; replaced++; log.AppendLine("DEFAULT-> " + r.name); }
            if (changed) { r.sharedMaterials = mats; EditorUtility.SetDirty(r); }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        log.AppendLine("default-material slots replaced: " + replaced);
        return log.ToString();
    }

    static void MakeTransparent(Material mat, bool premultiply)
    {
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", premultiply ? 1 : 0);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetFloat("_SrcBlend", (float)(premultiply ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.SrcAlpha));
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (premultiply) mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    // ---------- Phase E: lighting settings compatible with a realtime-lit URP scene ----------
    public static string ConfigureLighting()
    {
        var scene = EditorSceneManager.OpenScene(Scene);
        const string path = "Assets/Settings/OmegaLighting.lighting";
        var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
        if (ls == null) { ls = new LightingSettings { name = "OmegaLighting" }; AssetDatabase.CreateAsset(ls, path); }
        ls.realtimeGI = false;   // Enlighten realtime GI off: lighting is fully realtime + ambient
        ls.bakedGI = false;
        EditorUtility.SetDirty(ls);
        Lightmapping.lightingSettings = ls;
        Lightmapping.Clear();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        var saved = File.ReadAllText(Scene);
        var guid = AssetDatabase.AssetPathToGUID(path);
        return "lightingSettings=" + Lightmapping.lightingSettings.name + " sceneReferencesNew=" + saved.Contains(guid)
             + " lightingDataZero=" + saved.Contains("m_LightingDataAsset: {fileID: 0}");
    }

    // ---------- Phase F: drop unused Standard Assets (keep files still referenced) ----------
    public static string CleanStandardAssets(bool dryRun)
    {
        var log = new StringBuilder();
        const string sa = "Assets/Standard Assets";
        var roots = AssetDatabase.FindAssets("", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !p.StartsWith(sa) && !AssetDatabase.IsValidFolder(p)).ToArray();
        var keep = new HashSet<string>(AssetDatabase.GetDependencies(roots, true).Where(p => p.StartsWith(sa)));
        foreach (var k in keep) log.AppendLine("KEEP " + k);
        const string dest = "Assets/ThirdParty";
        if (!dryRun)
        {
            if (!AssetDatabase.IsValidFolder(dest)) AssetDatabase.CreateFolder("Assets", "ThirdParty");
            foreach (var k in keep)
            {
                var sub = Path.GetDirectoryName(k.Substring(sa.Length + 1)).Replace('\\', '/');
                var folder = dest;
                foreach (var part in sub.Split('/').Where(s => s.Length > 0))
                {
                    if (!AssetDatabase.IsValidFolder(folder + "/" + part)) AssetDatabase.CreateFolder(folder, part);
                    folder += "/" + part;
                }
                var err = AssetDatabase.MoveAsset(k, folder + "/" + Path.GetFileName(k));
                log.AppendLine("MOVE " + k + (string.IsNullOrEmpty(err) ? "" : " ERROR " + err));
            }
            log.AppendLine("DELETE " + sa + " -> " + AssetDatabase.DeleteAsset(sa));
            AssetDatabase.Refresh();
        }
        return log.ToString();
    }

    // ---------- Verification ----------
    public static string Verify()
    {
        var log = new StringBuilder();
        var rp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
        log.AppendLine("graphics=" + (rp ? rp.GetType().Name + ":" + rp.name : "NULL (Built-in)"));
        for (int i = 0; i < QualitySettings.names.Length; i++)
            log.AppendLine("quality " + QualitySettings.names[i] + "=" + (QualitySettings.GetRenderPipelineAssetAt(i) ? QualitySettings.GetRenderPipelineAssetAt(i).name : "inherits"));
        EditorSceneManager.OpenScene(Scene);
        var bad = new HashSet<string>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (var m in r.sharedMaterials)
                if (m != null && m.shader != null && !(m.shader.name.StartsWith("Universal Render Pipeline/") || m.shader.name.StartsWith("Sprites/") || m.shader.name.StartsWith("UI/") || m.shader.name.StartsWith("GUI/")))
                    bad.Add(r.name + " :: " + m.name + " :: " + m.shader.name + " :: " + AssetDatabase.GetAssetPath(m));
        log.AppendLine("non-URP materials in scene: " + bad.Count);
        foreach (var b in bad) log.AppendLine("  " + b);
        return log.ToString();
    }
}
