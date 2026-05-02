using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Initialization;
using System;
using System.IO;
using System.Linq;

namespace NSYNK.HyperSlides.EditorScripts
{
    /// <summary>
    /// This class is used to build the remote addressables group.
    /// It allows you to build the addressables for a specific group and upload them to a remote server.
    /// </summary>
    public class CustomAddressablesBuild
    {
        [MenuItem("Tools/Build Remote Addressables Group")]
        public static void BuildSingleAddressablesGroup()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

            string targetGroupName = "Remote Assets";
            string activeProfileId = settings.activeProfileId;

            AddressableAssetGroup targetGroup = settings.groups.FirstOrDefault(g => g.Name == targetGroupName);

            if (targetGroup == null)
            {
                Debug.LogError($"Addressables group '{targetGroupName}' not found. Please create it.");
                return;
            }

            Debug.Log($"Building remote addressables for group: {targetGroupName}");

            foreach (var group in settings.groups)
                if (group != targetGroup)
                    group.SetDirty(AddressableAssetSettings.ModificationEvent.GroupRemoved, true, true);

            AddressableAssetSettings.BuildPlayerContent();

            foreach (var group in settings.groups)
                group.SetDirty(AddressableAssetSettings.ModificationEvent.GroupAdded, true, true);
        }
    }
}