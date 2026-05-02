#if UNITY_VISIONOS

using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

/// <summary>
/// Use this script to add relevant frameworks to the Xcode project and overwrite parts of files
/// </summary>
class PostBuildXcode
{
    [PostProcessBuild( 900 )]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        Debug.Log("Start Xcode project related configuration of SDK with target: " + target + " at " + pathToBuiltProject);
        EditProj(pathToBuiltProject, target.ToString() == "VisionOS");
        Debug.Log("Complete the Xcode project configuration of the SDK！");        
    }

    static void EditProj(string pathToBuiltProject, bool isVisionOS = false)
    {
        string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);

        if (isVisionOS)
            projectPath = projectPath.Replace("Unity-iPhone", "Unity-VisionOS");


        PBXProject pbxProject = new PBXProject();
        pbxProject.ReadFromString(File.ReadAllText(projectPath));

        string mainTarget = pbxProject.GetUnityMainTargetGuid();
        string unityTarget = pbxProject.GetUnityFrameworkTargetGuid();

        pbxProject.SetBuildProperty(mainTarget, "SUPPORTED_PLATFORMS", "xrsimulator xros");
        pbxProject.AddFrameworkToProject(unityTarget, "GroupActivities.framework", false);
        pbxProject.WriteToFile(projectPath);

        var entitlementFilePath = "Entitlements.entitlements";
        var manager = new ProjectCapabilityManager(projectPath, entitlementFilePath, null, mainTarget);

        manager.AddGroupActivities();
        manager.AddWirelessAccessoryConfiguration();
        // manager.AddIncreasedMemoryLimit();

        AddToPolySpatialAppFile(pathToBuiltProject);
        AddToUnityVisionOSSettings(pathToBuiltProject);

        manager.WriteToFile();
    }

    /// <summary>
    /// Adding following line to the file
    /// => @StateObject var groupActivityManager = GroupActivityManager()
    /// and adding this to mainScene.
    /// => .environmentObject(groupActivityManager)
    /// </summary>
    /// <param name="projectPath"></param>
    static void AddToPolySpatialAppFile(string buildPath)
    {
        string filePath = buildPath + "/MainApp/UnityPolySpatialApp.swift";

        if (File.Exists(filePath))
        {
            string content = File.ReadAllText(filePath);
            content = "import UnityFramework\n" + content;
            content = content.Replace("var dismissImmersiveSpace", "var dismissImmersiveSpace\n@StateObject var groupActivityManager = GroupActivityManager()");
            content = content.Replace("mainScene", "mainScene.environmentObject(groupActivityManager)");
            File.WriteAllText(filePath, content);
        }
        else
            Debug.Log(buildPath + " => " + filePath + " does not exist!");
    }

    /// <summary>
    /// Adding following line to the file. TODO change this later to be triggered from within the app
    /// => .onAppear(){ if groupActivityManager.isSessionActive {} else { groupActivityManager.startGroupActivity()} }
    /// </summary>
    /// <param name="projectPath"></param>
    static void AddToUnityVisionOSSettings(string buildPath)
    {
        string filePath = buildPath + "/MainApp/UnityVisionOSSettings.swift";

        if (File.Exists(filePath))
        {
            string content = File.ReadAllText(filePath);
            content = ReplaceFirstOccurrence(content, "KeyboardTextField", ".onAppear(){ if groupActivityManager.isSessionActive {} else { groupActivityManager.startGroupActivity()} }\nKeyboardTextField");
            File.WriteAllText(filePath, content);
        }
        else
            Debug.Log(buildPath + " => " + filePath + " does not exist!");
    }

    static string ReplaceFirstOccurrence(string source, string find, string replace)
    {
        int position = source.IndexOf(find);

        if (position < 0)
            return source;

        return source.Substring(0, position) + replace + source.Substring(position + find.Length);
    }
}
#endif