using UnityEditor;
using UnityEditor.Build.Reporting;

// Release builds for every desktop platform (macOS, Windows, Linux), Mono backend.
// BuildRelease.All("1.4.0") stamps the version, builds into Builds/<platform>/ and switches back to macOS.
public static class BuildRelease
{
    public static string All(string version)
    {
        PlayerSettings.bundleVersion = version;
        var log = new System.Text.StringBuilder();
        log.AppendLine(One(BuildTarget.StandaloneOSX, "Builds/macOS/HockeyUnity.app"));
        log.AppendLine(One(BuildTarget.StandaloneWindows64, "Builds/Windows/HockeyUnity.exe"));
        log.AppendLine(One(BuildTarget.StandaloneLinux64, "Builds/Linux/HockeyUnity.x86_64"));
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);
        return log.ToString();
    }

    static string One(BuildTarget target, string path)
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/MENU.unity" },
            locationPathName = path,
            target = target,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        });
        var s = report.summary;
        string line = "BUILD " + target + " " + s.result + " " + (s.totalSize / (1024 * 1024)) + " MB, " + s.totalErrors + " errors -> " + path;
        UnityEngine.Debug.Log(line);
        if (s.result != BuildResult.Succeeded && UnityEngine.Application.isBatchMode) EditorApplication.Exit(1);
        return line;
    }
}
