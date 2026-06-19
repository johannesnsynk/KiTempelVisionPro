#if UNITY_VISIONOS || UNITY_IOS

using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using UnityEngine;

namespace NSYNK.HyperSlides.EditorScripts
{
    /// <summary>
    /// Use this script to add relevant frameworks to the Xcode project and overwrite parts of files
    /// </summary>
    static class PostBuildXcode
    {
        private const string SHARE_DEEP_LINK_SCHEME = "hyperslides";
        private const string MAIN_ENTITLEMENTS_FILE_NAME = "Entitlements.entitlements";
        private const string PACKAGE_NAME = "com.nsynk.hyperslides-core";
        private const string HOST_APP_SETTINGS_SOURCE_PATH = "Assets/Settings/AppSettings.plist";
        private const string FRAMEWORK_APP_SETTINGS_FILE_NAME = "AppSettings.plist";

        private static PBXProject pbxProject;
        private static string projectPath;
        private static string mainTarget;
        private static string pathToBuiltProject;

        private static readonly string[] HYPERSLIDES_FRAMEWORK_ORIGIN_PATHS = {
            "Packages/com.nsynk.hyperslides-core/Plugins/iOS",
            "Packages/com.nsynk.hyperslides-core/Assets/Plugins/iOS",
            "com.nsynk.hyperslides-core/Plugins/iOS",
            "Assets/HyperslidesCore/Assets/Plugins/iOS"
        };

        [PostProcessBuild(900)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            Debug.Log("Start Xcode project related configuration of SDK with target: " + target + " at " + pathToBuiltProject, typeof(PostBuildXcode));

            ConfigureXcodeProject(pathToBuiltProject, target.ToString() == "VisionOS");

            Debug.Log("Complete the Xcode project configuration of the SDK！", typeof(PostBuildXcode));
        }

        /// <summary>
        /// Configures the Xcode project by adding necessary capabilities, targets, and URL schemes for the Hyperslides framework.
        /// </summary>
        static void ConfigureXcodeProject(string pathToBuiltProject, bool isVisionOS = false)
        {
            try
            {
                PostBuildXcode.pathToBuiltProject = pathToBuiltProject;

                LoadXcodeProject(isVisionOS, out pbxProject, out projectPath, out mainTarget);

                ConfigureProjectBuildSettings(isVisionOS);
                WriteProjectToDisk("base project settings");

                string resolvedAppGroup = ConfigureMainTargetCapabilities(isVisionOS);

                EnsureMainTargetEntitlements(resolvedAppGroup);
                ConfigureShareExtension();
                ApplyHostAppSettingsOverride();
                EnsureMainAppDeepLinkScheme();
            }
            catch (System.Exception e)
            {
                Debug.LogError("Post-build Xcode configuration failed: " + e.Message, typeof(PostBuildXcode));
                throw;
            }
        }

        /// <summary>
        /// Loads the Xcode project from the specified path and retrieves the main target GUID.
        /// </summary>
        /// <param name="pathToBuiltProject">The file path to the built Xcode project.</param>
        /// <param name="isVisionOS">Indicates whether the target is VisionOS.</param>
        /// <param name="pbxProject">The loaded PBXProject instance.</param>
        /// <param name="projectPath">The path to the Xcode project file.</param>
        /// <param name="mainTarget">The GUID of the main target in the Xcode project.</param>
        private static void LoadXcodeProject(bool isVisionOS, out PBXProject pbxProject, out string projectPath, out string mainTarget)
        {
            projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);

            if (isVisionOS)
                projectPath = projectPath.Replace("Unity-iPhone", "Unity-VisionOS");

            pbxProject = new PBXProject();
            pbxProject.ReadFromString(File.ReadAllText(projectPath));
            mainTarget = pbxProject.GetUnityMainTargetGuid();
        }

        /// <summary>
        /// Configures build settings for the Xcode project, such as supported platforms for VisionOS. 
        /// </summary>
        /// <param name="pbxProject">The PBXProject instance representing the Xcode project.</param>
        /// <param name="isVisionOS">Indicates whether the target is VisionOS.</param>
        private static void ConfigureProjectBuildSettings(bool isVisionOS)
        {
            if (!isVisionOS)
                return;

            pbxProject.SetBuildProperty(mainTarget, "SUPPORTED_PLATFORMS", "xrsimulator xros");
        }

        /// <summary>
        /// Configures the main target capabilities by adding necessary entitlements such as App Groups, and writes the changes to disk.
        /// </summary>
        /// <param name="isVisionOS">Indicates whether the target is VisionOS.</param>
        /// <returns>The resolved App Group identifier.</returns>
        private static string ConfigureMainTargetCapabilities(bool isVisionOS)
        {
            ProjectCapabilityManager manager = new ProjectCapabilityManager(projectPath, MAIN_ENTITLEMENTS_FILE_NAME, null, mainTarget);
            string appBundleId = ResolveMainAppBundleId();
            string resolvedAppGroup = "group." + appBundleId;

            manager.AddAppGroups(new string[] { resolvedAppGroup });
            Debug.Log("Added App Group to entitlements via postprocess: " + resolvedAppGroup, typeof(PostBuildXcode));

            if (isVisionOS)
            {
                string[] accessGroups = new string[] { "QTA97NJKTJ.sharedkeychain" };
                manager.AddWirelessAccessoryConfiguration();
                manager.AddKeychainSharing(accessGroups);
            }

            manager.WriteToFile();
            Debug.Log("Wrote capability manager changes to disk.", typeof(PostBuildXcode));
            return resolvedAppGroup;
        }

        /// <summary>
        /// Ensures that the main target's entitlements file contains the specified App Group, and if not, adds it.
        /// </summary> <param name="resolvedAppGroup">The App Group identifier to ensure in the entitlements.</param> 
        private static void EnsureMainTargetEntitlements(string resolvedAppGroup)
        {
            if (string.IsNullOrEmpty(resolvedAppGroup))
                return;

            EnsureMainTargetAppGroupCapability(MAIN_ENTITLEMENTS_FILE_NAME, resolvedAppGroup);
        }

        /// <summary>
        /// Ensures that the specified entitlements file contains the given App Group, and if not, adds it to the entitlements.
        /// </summary>
        private static void ConfigureShareExtension()
        {
            AddShareExtensionTarget();
            WriteProjectToDisk("share extension target and file references");
        }

        /// <summary>
        /// Ensures that the specified entitlements file contains the given App Group, and if not, adds it to the entitlements.
        /// </summary>
        private static void EnsureMainAppDeepLinkScheme()
        {
            EnsureMainAppUrlScheme(SHARE_DEEP_LINK_SCHEME);
        }

        /// <summary>
        /// Replaces the generated Settings.bundle/AppSettings.plist with a host-provided override plist if present.
        /// </summary>
        private static void ApplyHostAppSettingsOverride()
        {
            if (!File.Exists(HOST_APP_SETTINGS_SOURCE_PATH))
            {
                Debug.Log("No host AppSettings override found at: " + HOST_APP_SETTINGS_SOURCE_PATH + ". Keeping framework default plist.", typeof(PostBuildXcode));
                return;
            }

            string[] settingsBundles = Directory.GetDirectories(pathToBuiltProject, "Settings.bundle", SearchOption.AllDirectories);
            if (settingsBundles == null || settingsBundles.Length == 0)
            {
                Debug.Log("Host AppSettings override exists, but no Settings.bundle was found in built Xcode project.", typeof(PostBuildXcode));
                return;
            }

            int overwrittenCount = 0;
            foreach (string settingsBundlePath in settingsBundles)
            {
                string destinationPath = Path.Combine(settingsBundlePath, FRAMEWORK_APP_SETTINGS_FILE_NAME);
                File.Copy(HOST_APP_SETTINGS_SOURCE_PATH, destinationPath, true);
                overwrittenCount++;
            }

            Debug.Log("Applied host AppSettings override to " + overwrittenCount + " Settings.bundle location(s) from: " + HOST_APP_SETTINGS_SOURCE_PATH, typeof(PostBuildXcode));
        }

        /// <summary> 
        /// Writes the current state of the PBXProject to disk with a log message indicating the reason for the write. 
        /// </summary>
        /// <param name="reason">The reason for writing the project to disk.</param>
        private static void WriteProjectToDisk(string reason)
        {
            pbxProject.WriteToFile(projectPath);
            Debug.Log("Wrote Xcode project changes: " + reason, typeof(PostBuildXcode));
        }

        /// <summary>
        /// Ensures that the main target's entitlements file contains the specified App Group, and if not, adds it to the entitlements.
        /// </summary>
        static void EnsureMainAppUrlScheme(string scheme)
        {
            string infoPlistPath = FindMainInfoPlistPath(pathToBuiltProject);

            PlistDocument plist = new PlistDocument();
            plist.ReadFromFile(infoPlistPath);

            PlistElementArray urlTypes = plist.root.values.ContainsKey("CFBundleURLTypes")
                ? plist.root["CFBundleURLTypes"].AsArray()
                : plist.root.CreateArray("CFBundleURLTypes");

            bool plistChanged = false;
            bool schemeExists = false;
            foreach (PlistElement entry in urlTypes.values)
            {
                PlistElementDict dict = entry.AsDict();
                if (dict == null || !dict.values.ContainsKey("CFBundleURLSchemes")) continue;

                PlistElementArray schemes = dict["CFBundleURLSchemes"].AsArray();
                List<string> originalSchemes = schemes.values.Select(v => v.AsString()).ToList();
                List<string> rewrittenSchemes = originalSchemes
                    .Select(value => (!string.IsNullOrEmpty(value) && char.IsDigit(value[0])) ? "share-" + value : value)
                    .ToList();

                if (rewrittenSchemes.Any(value => string.Equals(value, scheme, System.StringComparison.OrdinalIgnoreCase)))
                    schemeExists = true;

                if (!originalSchemes.SequenceEqual(rewrittenSchemes, System.StringComparer.Ordinal))
                {
                    schemes.values.Clear();
                    
                    foreach (string rewrittenScheme in rewrittenSchemes)
                        schemes.AddString(rewrittenScheme);

                    plistChanged = true;
                }
            }

            if (!schemeExists)
            {
                PlistElementDict dict = urlTypes.AddDict();
                dict.SetString("CFBundleURLName", "com.nsynk.hyperslides.share");
                PlistElementArray schemes = dict.CreateArray("CFBundleURLSchemes");
                schemes.AddString(scheme);
                plistChanged = true;
                Debug.Log("Added URL scheme to Info.plist: " + scheme + " (" + infoPlistPath + ")", typeof(PostBuildXcode));
            }

            if (plistChanged)
            {
                plist.WriteToFile(infoPlistPath);
            }
        }

        /// <summary>
        /// Ensures that the specified entitlements file contains the given App Group, and if not, adds it to the entitlements.
        /// </summary>
        static void AddShareExtensionTarget()
        {
            string templateRelative = ResolveShareExtensionTemplatePath();

            string dest = Path.Combine(pathToBuiltProject, "ShareExtension");
            CopyAndReplaceDirectory(templateRelative, dest);

            string appBundleId = ResolveMainAppBundleId();

            // Compute extension bundle id (must be prefixed by the app bundle id)
            string extensionBundleId = appBundleId + ".share";

            // Set the extension Info.plist CFBundleIdentifier to <appBundleId>.share
            string infoPlistPath = Path.Combine(dest, "Info.plist");
            if (!File.Exists(infoPlistPath))
            {
                throw new BuildFailedException("ShareExtension Info.plist not found at: " + infoPlistPath);
            }

            PlistDocument tpl = new PlistDocument();
            tpl.ReadFromFile(infoPlistPath);
            tpl.root.SetString("CFBundleIdentifier", extensionBundleId);
            tpl.WriteToFile(infoPlistPath);
            Debug.Log("Wrote extension CFBundleIdentifier to Info.plist: " + extensionBundleId, typeof(PostBuildXcode));

            string projectEntitlements = Path.Combine(pathToBuiltProject, MAIN_ENTITLEMENTS_FILE_NAME);
            string templateEntitlements = Path.Combine(dest, "ShareExtension.entitlements");
            if (!File.Exists(projectEntitlements))
            {
                throw new BuildFailedException("Main entitlements file not found at: " + projectEntitlements);
            }

            if (!File.Exists(templateEntitlements))
            {
                throw new BuildFailedException("ShareExtension entitlements file not found at: " + templateEntitlements);
            }

            CopyAppGroupsFromMainEntitlementsToExtension(projectEntitlements, templateEntitlements);
            EnsureCanonicalAppGroupInExtensionEntitlements(templateEntitlements, appBundleId);

            string extTargetGuid = GetOrCreateShareExtensionTarget(extensionBundleId, infoPlistPath);

            AddShareExtensionFilesToProject(dest, extTargetGuid);
            SetExtensionEntitlements(dest, extTargetGuid);
            ConfigureShareExtensionSigning(extTargetGuid);

            Debug.Log("Extension target created or reused; entitlements and signing were configured by the postprocess.", typeof(PostBuildXcode));
            Debug.Log("Copied ShareExtensionTemplate to Xcode project at: " + dest + ". Updated extension bundle id to: " + extensionBundleId + ".", typeof(PostBuildXcode));
        }

        private static void CopyAppGroupsFromMainEntitlementsToExtension(string projectEntitlementsPath, string extensionEntitlementsPath)
        {
            PlistDocument projectEntitlements = new PlistDocument();
            projectEntitlements.ReadFromFile(projectEntitlementsPath);
            if (!projectEntitlements.root.values.ContainsKey("com.apple.security.application-groups"))
            {
                return;
            }

            PlistDocument extensionEntitlements = new PlistDocument();
            extensionEntitlements.ReadFromFile(extensionEntitlementsPath);

            PlistElementArray sourceGroups = projectEntitlements.root.values["com.apple.security.application-groups"].AsArray();
            PlistElementArray targetGroups = extensionEntitlements.root.CreateArray("com.apple.security.application-groups");
            foreach (PlistElement value in sourceGroups.values)
            {
                targetGroups.AddString(value.AsString());
            }

            extensionEntitlements.WriteToFile(extensionEntitlementsPath);
        }

        private static void EnsureCanonicalAppGroupInExtensionEntitlements(string extensionEntitlementsPath, string appBundleId)
        {
            PlistDocument extensionEntitlements = new PlistDocument();
            extensionEntitlements.ReadFromFile(extensionEntitlementsPath);

            PlistElementArray appGroups = extensionEntitlements.root.values.ContainsKey("com.apple.security.application-groups")
                ? extensionEntitlements.root.values["com.apple.security.application-groups"].AsArray()
                : extensionEntitlements.root.CreateArray("com.apple.security.application-groups");

            string baseBundleId = appBundleId.EndsWith(".share", System.StringComparison.OrdinalIgnoreCase)
                ? appBundleId.Substring(0, appBundleId.Length - ".share".Length)
                : appBundleId;
            string appGroup = "group." + baseBundleId;

            System.Collections.Generic.HashSet<string> existing = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (PlistElement value in appGroups.values)
            {
                existing.Add(value.AsString());
            }

            if (existing.Contains(appGroup))
            {
                return;
            }

            appGroups.AddString(appGroup);
            extensionEntitlements.WriteToFile(extensionEntitlementsPath);
            Debug.Log("Merged App Group into ShareExtension.entitlements: " + appGroup, typeof(PostBuildXcode));
        }

        private static string ResolveShareExtensionTemplatePath()
        {
            const string extensionFolderName = "ShareExtensionTemplate";
            List<string> searchedCandidates = new List<string>();

            foreach (string originPath in HYPERSLIDES_FRAMEWORK_ORIGIN_PATHS)
            {
                string candidate = Path.Combine(originPath, extensionFolderName);
                searchedCandidates.Add(candidate);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            UnityEditor.PackageManager.PackageInfo[] packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            foreach (UnityEditor.PackageManager.PackageInfo package in packages)
            {
                if (!string.Equals(package.name, PACKAGE_NAME, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string candidate = Path.Combine(package.resolvedPath, "Assets/Plugins/iOS", extensionFolderName);
                searchedCandidates.Add(candidate);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                string candidateNoAssets = Path.Combine(package.resolvedPath, "Plugins/iOS", extensionFolderName);
                searchedCandidates.Add(candidateNoAssets);
                if (Directory.Exists(candidateNoAssets))
                {
                    return candidateNoAssets;
                }
            }

            throw new BuildFailedException("ShareExtensionTemplate not found. Searched: " + string.Join(" | ", searchedCandidates));
        }

        private static string GetOrCreateShareExtensionTarget(string extensionBundleId, string infoPlistPath)
        {
            string extTargetGuid = GetExistingShareExtensionTargetGuid();
            if (!string.IsNullOrEmpty(extTargetGuid))
            {
                Debug.Log("ShareExtension target already exists in Xcode project; reusing existing target.", typeof(PostBuildXcode));
                return extTargetGuid;
            }

            extTargetGuid = pbxProject.AddAppExtension(mainTarget, "ShareExtension", extensionBundleId, infoPlistPath);

            pbxProject.AddTargetDependency(mainTarget, extTargetGuid);
            return extTargetGuid;
        }

        private static string GetExistingShareExtensionTargetGuid()
        {
            return pbxProject.TargetGuidByName("ShareExtension");
        }

        private static void AddShareExtensionFilesToProject(string extensionFolderPath, string extTargetGuid)
        {
            string[] files = Directory.GetFiles(extensionFolderPath, "*", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                string relPath = "ShareExtension/" + file.Substring(extensionFolderPath.Length + 1).Replace("\\", "/");
                string fileGuid = pbxProject.AddFile(relPath, relPath);
                string ext = Path.GetExtension(file).ToLowerInvariant();

                if (string.IsNullOrEmpty(extTargetGuid))
                {
                    continue;
                }

                if (ext != ".swift" && ext != ".m" && ext != ".mm" && ext != ".c" && ext != ".cpp")
                {
                    continue;
                }

                pbxProject.AddFileToBuild(extTargetGuid, fileGuid);
            }
        }

        private static void SetExtensionEntitlements(string extensionFolderPath, string extTargetGuid)
        {
            if (string.IsNullOrEmpty(extTargetGuid))
            {
                throw new BuildFailedException("Extension target guid is empty.");
            }

            string entRel = "ShareExtension/ShareExtension.entitlements";
            string entFull = Path.Combine(extensionFolderPath, "ShareExtension.entitlements");
            if (!File.Exists(entFull))
            {
                throw new BuildFailedException("ShareExtension entitlements file not found at: " + entFull);
            }

            pbxProject.AddFile(entRel, entRel);
            pbxProject.SetBuildProperty(extTargetGuid, "CODE_SIGN_ENTITLEMENTS", entRel);
            Debug.Log("Set CODE_SIGN_ENTITLEMENTS for ShareExtension target to: " + entRel, typeof(PostBuildXcode));
        }

        /// <summary>
        /// Configures the signing settings for the ShareExtension target to use the same development team
        /// as specified in the PlayerSettings, and sets the code signing style to Automatic.
        /// </summary>
        private static void ConfigureShareExtensionSigning(string extTargetGuid)
        {
            if (string.IsNullOrEmpty(extTargetGuid))
            {
                throw new BuildFailedException("Cannot configure ShareExtension signing: target guid is empty.");
            }

            string teamId = PlayerSettings.iOS.appleDeveloperTeamID;
            if (string.IsNullOrEmpty(teamId))
            {
                Debug.Log("ShareExtension signing not updated because PlayerSettings.iOS.appleDeveloperTeamID is empty.", typeof(PostBuildXcode));
                return;
            }

            pbxProject.SetBuildProperty(extTargetGuid, "DEVELOPMENT_TEAM", teamId);
            pbxProject.SetBuildProperty(extTargetGuid, "CODE_SIGN_STYLE", "Automatic");

            Debug.Log("Set ShareExtension signing team to: " + teamId, typeof(PostBuildXcode));
        }

        private static string ResolveMainAppBundleId()
        {
            string appBundleId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);

            if (IsUnresolvedBundleId(appBundleId))
            {
                throw new BuildFailedException("Could not resolve iOS app bundle identifier during postbuild; aborting to avoid invalid App Group entitlements.");
            }

            return appBundleId;
        }

        private static string FindMainInfoPlistPath(string pathToBuiltProject)
        {
            string[] candidates = new string[]
            {
            Path.Combine(pathToBuiltProject, "Info.plist"),
            Path.Combine(pathToBuiltProject, "Unity-iPhone/Info.plist"),
            Path.Combine(pathToBuiltProject, "Unity-VisionOS/Info.plist")
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new BuildFailedException("Could not find main app Info.plist in expected locations.");
        }

        static void EnsureMainTargetAppGroupCapability(string entitlementFilePath, string appGroup)
        {
            string entitlementsFullPath = Path.Combine(pathToBuiltProject, entitlementFilePath);
            PlistDocument ent = new PlistDocument();

            if (File.Exists(entitlementsFullPath)) ent.ReadFromFile(entitlementsFullPath);

            PlistElementArray groups = ent.root.values.ContainsKey("com.apple.security.application-groups")
                ? ent.root["com.apple.security.application-groups"].AsArray()
                : ent.root.CreateArray("com.apple.security.application-groups");

            bool hasGroup = false;
            foreach (PlistElement value in groups.values)
            {
                if (string.Equals(value.AsString(), appGroup, System.StringComparison.OrdinalIgnoreCase))
                {
                    hasGroup = true;
                    break;
                }
            }

            if (!hasGroup)
            {
                groups.AddString(appGroup);
                ent.WriteToFile(entitlementsFullPath);
                Debug.Log("Explicitly added app group to main entitlements: " + appGroup, typeof(PostBuildXcode));
            }

            pbxProject.SetBuildProperty(mainTarget, "CODE_SIGN_ENTITLEMENTS", entitlementFilePath);
            pbxProject.WriteToFile(projectPath);
            Debug.Log("Ensured CODE_SIGN_ENTITLEMENTS for main target: " + entitlementFilePath, typeof(PostBuildXcode));
        }

        private static void CopyAndReplaceDirectory(string srcPath, string dstPath)
        {
            if (Directory.Exists(dstPath))
                Directory.Delete(dstPath, true);
            if (File.Exists(dstPath))
                File.Delete(dstPath);

            Directory.CreateDirectory(dstPath);

            foreach (string file in Directory.GetFiles(srcPath))
                File.Copy(file, Path.Combine(dstPath, Path.GetFileName(file)));

            foreach (string dir in Directory.GetDirectories(srcPath))
                CopyAndReplaceDirectory(dir, Path.Combine(dstPath, Path.GetFileName(dir)));
        }

        private static bool IsUnresolvedBundleId(string value)
        {
            return string.IsNullOrEmpty(value)
                || value.Contains("${")
                || value.Contains("PRODUCT_BUNDLE_IDENTIFIER");
        }
    }
}
#endif