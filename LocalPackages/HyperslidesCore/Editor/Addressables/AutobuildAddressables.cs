using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

class AutobuildAddressables
{
    /// <summary>
    /// Run a clean build before export.
    /// </summary>
    static public void PreExport()
    {
        PlayerSettings.iOS.locationUsageDescription = "Share location to adjust avatar visibility";

        Debug.Log("BuildAddressablesProcessor.PreExport start");

        AddressableAssetSettings.CleanPlayerContent(
        AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilder);
        AddressableAssetSettings.BuildPlayerContent();

        Debug.Log("BuildAddressablesProcessor.PreExport done");
    }

    //Uncomment to build addressables on build (currently bugged and needs investigation)
    /*
    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        BuildPlayerWindow.RegisterBuildPlayerHandler(BuildPlayerHandler);
    }

    private static void BuildPlayerHandler(BuildPlayerOptions options)
    {
        ////Uncomment if you want a popup before building
        //if (EditorUtility.DisplayDialog("Build with Addressables",
        //    "Do you want to build a clean addressables before export?",
        //    "Build with Addressables", "Skip"))
        //    PreExport();

        BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
    }
    */
}