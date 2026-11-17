using System.Collections.Generic;
using MemoryCorridor.Interaction;
using MemoryCorridor.Media;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace MemoryCorridor.Core
{
    /// <summary>
    /// 设置服务（R5 建，R11 扩）：显隐/面板距离/舒适暗角/移动方式的单一消费点。
    /// 全部 Set* 即持久化（appdata.settings）+ 立即应用到场景对象。
    /// </summary>
    public static class SettingsService
    {
        public const float PanelDistanceMin = 1.5f;
        public const float PanelDistanceMax = 3.0f;
        private static readonly float[] PanelDistanceSteps = { 1.5f, 2.0f, 2.4f, 3.0f };

        /// <summary>读 appdata 应用到场景：光点标记/星点标签显隐 + 舒适暗角强度。</summary>
        public static void ApplyAll()
        {
            AppData data = SaveDataService.LoadAppData();
            bool showPoints = data == null || data.settings == null || data.settings.showLightPoints;
            bool showLabels = data == null || data.settings == null || data.settings.showStarLabels;
            float vignette = (data != null && data.settings != null && data.settings.vignetteEnabled)
                ? Mathf.Clamp01(data.settings.vignetteIntensity) : 0f;

            LightPointAnchor[] anchors = Object.FindObjectsByType<LightPointAnchor>(
                UnityEngine.FindObjectsSortMode.None);
            for (int i = 0; i < anchors.Length; i++)
            {
                anchors[i].SetMarkerVisible(showPoints);
            }

            ConstellationWall[] walls = Object.FindObjectsByType<ConstellationWall>(
                UnityEngine.FindObjectsSortMode.None);
            for (int i = 0; i < walls.Length; i++)
            {
                walls[i].SetLabelsVisible(showLabels);
            }

            ApplyVignette(vignette);
        }

        /// <summary>切换光点标记显隐并持久化（返回切换后的值）。</summary>
        public static bool ToggleLightPoints()
        {
            AppData data = SaveDataService.LoadAppData();
            if (data == null || data.settings == null) return true;
            data.settings.showLightPoints = !data.settings.showLightPoints;
            SaveDataService.SaveAppData(data);
            ApplyAll();
            return data.settings.showLightPoints;
        }

        /// <summary>切换星点标签显隐并持久化（返回切换后的值）。</summary>
        public static bool ToggleStarLabels()
        {
            AppData data = SaveDataService.LoadAppData();
            if (data == null || data.settings == null) return true;
            data.settings.showStarLabels = !data.settings.showStarLabels;
            SaveDataService.SaveAppData(data);
            ApplyAll();
            return data.settings.showStarLabels;
        }

        /// <summary>设置面板距离（1.5-3.0m，档位吸附）并持久化。面板 Show 时读取。</summary>
        public static float SetPanelDistance(float meters)
        {
            float d = Mathf.Clamp(meters, PanelDistanceMin, PanelDistanceMax);
            float best = PanelDistanceSteps[0];
            for (int i = 1; i < PanelDistanceSteps.Length; i++)
            {
                if (Mathf.Abs(d - PanelDistanceSteps[i]) < Mathf.Abs(d - best)) best = PanelDistanceSteps[i];
            }
            d = best;

            AppData data = SaveDataService.LoadAppData();
            if (data != null && data.settings != null)
            {
                data.settings.panelDistance = d;
                SaveDataService.SaveAppData(data);
            }
            return d;
        }

        public static float GetPanelDistance()
        {
            AppData data = SaveDataService.LoadAppData();
            return (data != null && data.settings != null)
                ? Mathf.Clamp(data.settings.panelDistance, PanelDistanceMin, PanelDistanceMax)
                : 2.4f;
        }

        /// <summary>循环舒适暗角档位（关→0.2→0.4→关）并持久化+立即应用。</summary>
        public static float CycleVignette()
        {
            AppData data = SaveDataService.LoadAppData();
            if (data == null || data.settings == null) return 0f;
            // R11 状态机：关→0.2→0.4→关（三档循环）。
            // 旧条件 `!enabled || intensity<0.3` 会把 0.2 档永远吸回（0.4 不可达）——只判 enabled。
            if (!data.settings.vignetteEnabled)
            {
                data.settings.vignetteEnabled = true;
                data.settings.vignetteIntensity = 0.2f;
            }
            else if (data.settings.vignetteIntensity < 0.3f)
            {
                data.settings.vignetteIntensity = 0.4f;
            }
            else
            {
                data.settings.vignetteEnabled = false;
                data.settings.vignetteIntensity = 0.3f;
            }
            SaveDataService.SaveAppData(data);
            ApplyAll();
            return data.settings.vignetteEnabled ? data.settings.vignetteIntensity : 0f;
        }

        /// <summary>把暗角强度应用到场景 Global Volume 的 Vignette override。</summary>
        public static void ApplyVignette(float intensity)
        {
            GameObject volumeGo = GameObject.Find("GlobalVolume");
            if (volumeGo == null)
            {
                Debug.LogWarning("[SettingsService] GlobalVolume 不存在，跳过暗角应用");
                return;
            }
            Volume volume = volumeGo.GetComponent<Volume>();
            if (volume == null) return;
            VolumeProfile profile = volume.sharedProfile;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                volume.sharedProfile = profile;
            }
            if (!profile.TryGet<UnityEngine.Rendering.Universal.Vignette>(out var vignette))
            {
                vignette = profile.Add<UnityEngine.Rendering.Universal.Vignette>();
            }
            vignette.intensity.Override(Mathf.Clamp01(intensity));
        }

        /// <summary>设置移动方式（"teleport"=传送优先 / "smooth"=平滑）并持久化+应用到 XR Rig。</summary>
        public static string SetLocomotion(string mode)
        {
            string m = (mode == "smooth") ? "smooth" : "teleport";
            AppData data = SaveDataService.LoadAppData();
            if (data != null && data.settings != null)
            {
                data.settings.locomotionMode = m;
                SaveDataService.SaveAppData(data);
            }
            ApplyLocomotion(m);
            return m;
        }

        public static string GetLocomotion()
        {
            AppData data = SaveDataService.LoadAppData();
            return (data != null && data.settings != null) ? data.settings.locomotionMode : "teleport";
        }

        /// <summary>应用到 XR Rig：传送=Teleport+SnapTurn 开/DynamicMove 关；平滑=反向。</summary>
        public static void ApplyLocomotion(string mode)
        {
            var teleport = Object.FindObjectsByType<TeleportationProvider>(
                UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
            var snap = Object.FindObjectsByType<ActionBasedSnapTurnProvider>(
                UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
            var dynamicMove = Object.FindObjectsByType<DynamicMoveProvider>(
                UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
            var continuous = Object.FindObjectsByType<ActionBasedContinuousTurnProvider>(
                UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);

            bool teleportOn = mode == "teleport";
            for (int i = 0; i < teleport.Length; i++) teleport[i].enabled = teleportOn;
            for (int i = 0; i < snap.Length; i++) snap[i].enabled = teleportOn;
            for (int i = 0; i < dynamicMove.Length; i++) dynamicMove[i].enabled = !teleportOn;
            for (int i = 0; i < continuous.Length; i++) continuous[i].enabled = !teleportOn;
        }
    }
}
