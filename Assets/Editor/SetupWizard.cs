using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MemoryCorridor.EditorTools
{
    /// <summary>
    /// W1 一键环境向导（全同步版）：URP 配置 → 场景创建 → Android 平台切换 → OpenXR Loader。
    /// 前置：Packages/manifest.json 已写好依赖（不在此处装包，避免域重载打断批处理）。
    /// 用法：Unity.exe -batchmode -projectPath <repo> -executeMethod MemoryCorridor.EditorTools.SetupWizard.Run -quit -logFile <log>
    /// API 均按 2022.3.62f3c1 实测（坑-013）：
    ///  - PlayerSettings.Android.forceInternetPermission（internetAccess 不存在）
    ///  - activeInputHandler 属性不存在 → 直改 ProjectSettings.asset YAML
    ///  - EditorUserBuildSettings.SwitchActiveBuildTarget 返回 bool
    ///  - XRPackageMetadataStore.AssignLoader(XRManagerSettings, string, BuildTargetGroup)（3 参，4.5.3 实测）
    /// </summary>
    public static class SetupWizard
    {
        [MenuItem("MemoryCorridor/W1 环境向导")]
        public static void Run()
        {
            int ok = 0, fail = 0;
            if (Stage("URP", StageUrp)) ok++; else fail++;
            if (Stage("Scenes", StageScenes)) ok++; else fail++;
            if (Stage("Android", StageAndroid)) ok++; else fail++;
            if (Stage("OpenXR", StageOpenXrLoader)) ok++; else fail++;
            if (Stage("PICO", StagePicoLoader)) ok++; else fail++;
            AssetDatabase.SaveAssets();
            Debug.Log($"[SetupWizard] 完成：{ok} 成功 / {fail} 失败（失败项见上方 LogError，可菜单 MemoryCorridor/W1 环境向导 重跑）");
        }

        private static bool Stage(string name, Action action)
        {
            try
            {
                action();
                Debug.Log($"[SetupWizard] {name} OK");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SetupWizard] {name} FAILED: {e}");
                return false;
            }
        }

        private static void StageUrp()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                AssetDatabase.CreateFolder("Assets", "Settings");

            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, "Assets/Settings/URP-Renderer.asset");

            var asset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            // 用 SerializedObject 直接填 m_RendererDataList（公开 SetRenderer API 各版本签名不一）
            var so = new SerializedObject(asset);
            var list = so.FindProperty("m_RendererDataList");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, "Assets/Settings/URP-Asset.asset");

            GraphicsSettings.defaultRenderPipeline = asset;
            QualitySettings.renderPipeline = asset;
        }

        private static void StageScenes()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            var s1 = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(s1, "Assets/Scenes/EditorScene.unity");
            var s2 = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(s2, "Assets/Scenes/TourScene.unity");
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/EditorScene.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/TourScene.unity", true),
            };
        }

        private static void StageAndroid()
        {
            PlayerSettings.companyName = "MemoryCorridor";
            PlayerSettings.productName = "MemoryCorridor";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.memorycorridor.app");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.forceInternetPermission = true; // 坑-013：internetAccess 不存在

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new Exception("平台切换失败（SwitchActiveBuildTarget 返回 false）");

            // activeInputHandler 属性在本版本不存在（坑-013）→ 直改 ProjectSettings.asset：
            // 0=旧输入 1=InputSystem 2=Both；XRI 需要 1 或 2
            const string settingsPath = "ProjectSettings/ProjectSettings.asset";
            string yaml = File.ReadAllText(settingsPath);
            if (yaml.Contains("activeInputHandler: 0"))
            {
                yaml = yaml.Replace("activeInputHandler: 0", "activeInputHandler: 2");
                File.WriteAllText(settingsPath, yaml);
                Debug.Log("[SetupWizard] ProjectSettings.activeInputHandler → 2 (Both)");
            }
            else
            {
                Debug.LogWarning("[SetupWizard] 未找到 activeInputHandler: 0，当前值保持不变（请人工确认）");
            }
        }

        private static void StageOpenXrLoader()
        {
            // 坑-013 续：XRGeneralSettingsForBuildTarget 在资产不存在时返回 null；
            // 官方创建入口是 internal GetOrCreate()（反射调用），再用公开方法建 per-BT 设置
            var perBtType = typeof(UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget);
            var getOrCreate = perBtType.GetMethod("GetOrCreate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var perBt = getOrCreate?.Invoke(null, null) as UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget;
            if (perBt == null)
                throw new Exception("XRGeneralSettingsPerBuildTarget 创建失败（GetOrCreate 反射失败）");

            if (!perBt.HasSettingsForBuildTarget(BuildTargetGroup.Android))
                perBt.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!perBt.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                perBt.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            var settings = perBt.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (settings == null || settings.Manager == null)
                throw new Exception("XRGeneralSettings(Android) 或其 Manager 仍为空");

            // 4.5.3 实际签名 3 参（PackageCache 源码 XRPackageMetadataStore.cs:336）
            bool result = UnityEditor.XR.Management.Metadata.XRPackageMetadataStore.AssignLoader(
                settings.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android);
            if (!result)
                throw new Exception("AssignLoader(OpenXR) 返回 false");
            AssetDatabase.SaveAssets();
        }

        /// <summary>PICO Loader：前提 Packages/com.unity.xr.picoxr 已嵌入（本地 UPM 包）。
        /// Loader 类 Unity.XR.PXR.PXR_Loader（源码实测，坑-013 纪律）。
        /// OpenXR 与 PICO 双 Loader 并存：PICO 设备走 PXR_Loader，模拟器/OpenXR 走 OpenXRLoader。</summary>
        private static void StagePicoLoader()
        {
            var perBtType = typeof(UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget);
            var getOrCreate = perBtType.GetMethod("GetOrCreate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var perBt = getOrCreate?.Invoke(null, null) as UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget;
            if (perBt == null)
                throw new Exception("XRGeneralSettingsPerBuildTarget 创建失败");

            var settings = perBt.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (settings == null || settings.Manager == null)
                throw new Exception("XRGeneralSettings(Android) 未初始化（依赖 OpenXR 阶段）");

            bool assigned = UnityEditor.XR.Management.Metadata.XRPackageMetadataStore.AssignLoader(
                settings.Manager, "Unity.XR.PXR.PXR_Loader", BuildTargetGroup.Android);
            if (!assigned)
                throw new Exception("AssignLoader(PICO) 返回 false（检查 Packages/com.unity.xr.picoxr 是否完整）");
            AssetDatabase.SaveAssets();
        }
    }
}
