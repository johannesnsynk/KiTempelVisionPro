using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class VisionOsBuildWithTemporaryDefine
{
    private const string TempDefine = "NO_LIVEKIT_MODE";

    [MenuItem("Build/VisionOS Export (temp NO_LIVEKIT_MODE)")]
    public static void BuildVisionOsWithTemporaryDefine()
    {
        string exportPath = EditorUtility.SaveFolderPanel(
            "Choose visionOS Xcode export folder",
            "",
            "VisionOSBuild"
        );

        if (string.IsNullOrWhiteSpace(exportPath))
        {
            Debug.Log("VisionOS export canceled.");
            return;
        }

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("No enabled scenes found in Build Settings.");
            return;
        }

        bool defineAdded = false;

        try
        {
            defineAdded = AddDefineIfMissing(TempDefine);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exportPath,
                target = BuildTarget.VisionOS,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log("VisionOS export succeeded: " + report.summary.outputPath);
            }
            else
            {
                Debug.LogError("VisionOS export failed with result: " + report.summary.result);
            }
        }
        finally
        {
            if (defineAdded)
                RemoveDefine(TempDefine);
        }
    }

    private static bool AddDefineIfMissing(string define)
    {
        string[] defines = GetVisionOsDefines();

        if (defines.Contains(define, StringComparer.Ordinal))
            return false;

        string updated = string.Join(";", defines.Append(define));
        SetVisionOsDefines(updated);
        Debug.Log("Added temporary visionOS define: " + define);
        return true;
    }

    private static void RemoveDefine(string define)
    {
        string[] defines = GetVisionOsDefines();
        string updated = string.Join(";", defines.Where(d => !string.Equals(d, define, StringComparison.Ordinal)));
        SetVisionOsDefines(updated);
        Debug.Log("Removed temporary visionOS define: " + define);
    }

    private static string[] GetVisionOsDefines()
    {
        string current;
#if UNITY_2021_2_OR_NEWER
        current = PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.VisionOS);
#else
        current = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS);
#endif

        return (current ?? string.Empty)
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(d => d.Trim())
            .Where(d => d.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static void SetVisionOsDefines(string defines)
    {
#if UNITY_2021_2_OR_NEWER
        PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.VisionOS, defines);
#else
        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS, defines);
#endif
    }
}
