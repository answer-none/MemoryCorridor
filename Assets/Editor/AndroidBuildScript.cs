using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MemoryCorridor.EditorTools
{
    /// <summary>
    /// Android 打包脚本（M2 内测包）：IL2CPP + ARM64 + ASTC + Linear 色彩空间。
    /// 产物输出到仓库外 D:/unityxiangmu/_builds/（.gitignore 已排除 *.apk）。
    /// 用法：Unity.exe -batchmode -projectPath … -executeMethod MemoryCorridor.EditorTools.AndroidBuildScript.Build -logFile …
    /// 成功 Exit(0)，失败 Exit(1)。
    /// PICO 要求：IL2CPP/ARM64/Linear；包名 com.memorycorridor.app 与 INTERNET 权限 W1 已设。
    /// </summary>
    public static class AndroidBuildScript
    {
        private const string OutputDir = "D:/unityxiangmu/_builds";
        private const string OutputPath = OutputDir + "/MemoryCorridor_M2.apk";

        public static void Build()
        {
            try
            {
                ConfigurePlayerSettings();
                DisableOpenXrLoaderOnAndroid();
                ReportLoaders();
                BuildReport report = BuildPipeline.BuildPlayer(
                    new BuildPlayerOptions
                    {
                        scenes = new[] { "Assets/Scenes/EditorScene.unity" },
                        target = BuildTarget.Android,
                        locationPathName = OutputPath,
                        options = BuildOptions.None,
                    });

                if (report.summary.result == BuildResult.Succeeded)
                {
                    FileInfo apk = new FileInfo(OutputPath);
                    Debug.Log("[AndroidBuild] 成功: " + OutputPath
                        + " | " + apk.Length / (1024 * 1024) + " MB"
                        + " | 耗时 " + report.summary.totalTime.TotalMinutes.ToString("F1") + " 分钟"
                        + " | 警告数 " + report.summary.totalWarnings);
                    EditorApplication.Exit(0);
                }
                else
                {
                    Debug.LogError("[AndroidBuild] 失败: " + report.summary.result
                        + " | 错误数 " + report.summary.totalErrors);
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[AndroidBuild] 异常: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64; // PICO 仅 64 位
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            PlayerSettings.colorSpace = ColorSpace.Linear; // PICO SDK 要求；URP 标配
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.forceInternetPermission = true;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            Debug.Log("[AndroidBuild] 配置: IL2CPP/ARM64/ASTC/Linear"
                + " | 包名 " + Application.identifier
                + " | minSdk " + PlayerSettings.Android.minSdkVersion);
        }

        /// <summary>
        /// Android 构建仅保留 PICO Loader（坑-033）：OpenXR 包与 PICO SDK 的
        /// libopenxr_loader.so 在 Gradle mergeNativeLibs 撞库。Editor Play 不依赖
        /// Android loader（XR Device Simulator 走 Input System），模拟器跑设备 APK 也用 PICO。
        /// </summary>
        private static void DisableOpenXrLoaderOnAndroid()
        {
            var general = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget
                .XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (general == null || general.Manager == null)
            {
                Debug.LogWarning("[AndroidBuild] 无 XR Manager，跳过移除 OpenXRLoader");
                return;
            }
            bool removed = UnityEditor.XR.Management.Metadata.XRPackageMetadataStore.RemoveLoader(
                general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android);
            Debug.Log("[AndroidBuild] 移除 OpenXRLoader(Android): " + removed);
        }

        /// <summary>打印 Android 端已启用的 XR Loader 顺序（应只剩 PXR_Loader）。</summary>
        private static void ReportLoaders()
        {
            var general = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget
                .XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (general == null || general.Manager == null)
            {
                Debug.LogWarning("[AndroidBuild] XR Loader 未配置（异常：wizard 应已设置）");
                return;
            }
            var active = general.Manager.activeLoaders;
            string names = "";
            for (int i = 0; i < active.Count; i++)
            {
                names += (i > 0 ? " -> " : "") + active[i].name;
            }
            Debug.Log("[AndroidBuild] Android XR Loaders: " + (string.IsNullOrEmpty(names) ? "(无)" : names));
        }
    }
}
