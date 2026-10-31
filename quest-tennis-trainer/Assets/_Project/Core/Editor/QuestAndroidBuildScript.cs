using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
#endif

namespace QuestTennisTrainer.Core.Editor
{
    /// <summary>
    /// Canonical Meta Quest 3 (Snapdragon XR2 Gen 2) build specification for Unity 6 LTS.
    /// </summary>
    [Serializable]
    public struct Quest3BuildManifest
    {
        public string ApplicationIdentifier;
        public string ProductName;
        public string TargetArchitecture; // Must be ARM64 for Quest 3
        public string ScriptingBackend;   // Must be IL2CPP for 120 Hz native performance
        public string GraphicsApi;        // Must be Vulkan for URP + Dynamic FFR on Quest 3
        public int MinAndroidSdkLevel;    // Android 12L (API 32) minimum for Quest 3 OpenXR
        public int TargetRefreshRateHz;   // 120 Hz
        public bool EnableDynamicFixedFoveatedRendering;
        public string OutputApkRelativePath;
    }

    /// <summary>
    /// One-click Unity 6 Editor menu item and headless CI batch-mode builder for Meta Quest 3.
    /// Outputs directly to `build/Android/QuestTennisTrainer.apk` so `./quest-tennis-trainer/scripts/sandbox.sh sideload`
    /// can deploy it to the headset immediately.
    /// </summary>
    public static class QuestAndroidBuildScript
    {
        public const string DefaultAppId = "com.krishnakasinadhuni.questtennistrainer";
        public const string DefaultOutputApkPath = "build/Android/QuestTennisTrainer.apk";
        public const int Quest3MinAndroidSdkLevel = 32;

        /// <summary>
        /// Returns the canonical Meta Quest 3 build configuration.
        /// </summary>
        public static Quest3BuildManifest GetQuest3BuildManifest()
        {
            return new Quest3BuildManifest
            {
                ApplicationIdentifier = DefaultAppId,
                ProductName = "Quest 3 Tennis Trainer",
                TargetArchitecture = "ARM64",
                ScriptingBackend = "IL2CPP",
                GraphicsApi = "Vulkan",
                MinAndroidSdkLevel = Quest3MinAndroidSdkLevel,
                TargetRefreshRateHz = (int)TennisPhysicsConfig.TargetFrameRateHz,
                EnableDynamicFixedFoveatedRendering = true,
                OutputApkRelativePath = DefaultOutputApkPath
            };
        }

        /// <summary>
        /// Validates that a Quest 3 build manifest satisfies all hardware and 120 Hz physics requirements.
        /// </summary>
        public static bool ValidateBuildManifest(Quest3BuildManifest manifest, out string validationError)
        {
            if (manifest.TargetArchitecture != "ARM64")
            {
                validationError = "Meta Quest 3 requires 64-bit ARM64 architecture.";
                return false;
            }

            if (manifest.ScriptingBackend != "IL2CPP")
            {
                validationError = "120 Hz sub-stepped physics requires IL2CPP native compilation.";
                return false;
            }

            if (manifest.GraphicsApi != "Vulkan")
            {
                validationError = "Meta Quest 3 URP + Dynamic FFR requires Vulkan graphics API.";
                return false;
            }

            if (manifest.MinAndroidSdkLevel < Quest3MinAndroidSdkLevel)
            {
                validationError = $"Meta Quest 3 requires at least Android API Level {Quest3MinAndroidSdkLevel}.";
                return false;
            }

            if (manifest.TargetRefreshRateHz != 120)
            {
                validationError = "Target refresh rate must be locked to 120 Hz.";
                return false;
            }

            if (string.IsNullOrEmpty(manifest.OutputApkRelativePath) ||
                !manifest.OutputApkRelativePath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
            {
                validationError = "Output path must point to a valid .apk file.";
                return false;
            }

            validationError = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        [MenuItem("QuestTennis/1. Generate Playable Training Scene Rig")]
        public static void GeneratePlayableRigMenuItem()
        {
            TennisCourtSceneBuilder.BuildWiredTrainingRig();
            Debug.Log("[QuestTennisTrainer] Spawned complete 120 Hz court, wall, ball machine, and serve rig in active scene.");
        }

        [MenuItem("QuestTennis/2. Build Quest 3 APK (ARM64 + Vulkan 120Hz)")]
        public static void BuildQuest3ApkFromMenu()
        {
            BuildQuest3ApkFromCli();
        }

        /// <summary>
        /// Batch-mode CLI and Menu entrypoint that configures Unity 6 PlayerSettings for Quest 3
        /// and builds `build/Android/QuestTennisTrainer.apk`.
        /// </summary>
        public static void BuildQuest3ApkFromCli()
        {
            Quest3BuildManifest manifest = GetQuest3BuildManifest();
            if (!ValidateBuildManifest(manifest, out string err))
            {
                throw new InvalidOperationException($"[QuestTennisTrainer] Invalid build manifest: {err}");
            }

            PlayerSettings.companyName = "KrishnaKasinadhuni";
            PlayerSettings.productName = manifest.ProductName;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, manifest.ApplicationIdentifier);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)manifest.MinAndroidSdkLevel;

            Time.fixedDeltaTime = TennisPhysicsConfig.FixedDeltaTime;

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = GetEnabledScenePaths(),
                locationPathName = manifest.OutputApkRelativePath,
                target = BuildTarget.Android,
                options = BuildOptions.CompressWithLz4HC
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"[QuestTennisTrainer] Android APK build failed: {report.summary.result}");
            }

            Debug.Log($"[QuestTennisTrainer] Built Quest 3 APK at: {manifest.OutputApkRelativePath}");
        }

        private static string[] GetEnabledScenePaths()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            int count = 0;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].enabled) count++;
            }

            string[] paths = new string[count];
            int idx = 0;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].enabled) paths[idx++] = scenes[i].path;
            }
            return paths;
        }
#endif
    }
}
