using UnityEditor;

public static class BuildMac
{
    public static void Release() => Build("Builds/HockeyUnity.app", BuildOptions.None);
    public static void Development() => Build("Builds/HockeyUnity-dev.app", BuildOptions.Development);

    static void Build(string path, BuildOptions opts)
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/MENU.unity" },
            locationPathName = path,
            target = BuildTarget.StandaloneOSX,
            options = opts,
        });
        UnityEngine.Debug.Log("BUILD " + path + " " + report.summary.result);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded && UnityEngine.Application.isBatchMode) EditorApplication.Exit(1);
    }
}
