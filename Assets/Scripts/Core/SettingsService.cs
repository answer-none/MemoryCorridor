using MemoryCorridor.Interaction;
using MemoryCorridor.Media;
using UnityEngine;

namespace MemoryCorridor.Core
{
    /// <summary>
    /// 设置服务（R5）：光点标记/星点标签显隐的单一消费点，切换即持久化并应用到场景。
    /// settings.showLightPoints（光点标记）/ settings.showStarLabels（星点标签，schema v2 扩展字段）。
    /// 应用时机：RoomManager spawn（光点）、ConstellationWall.Build（标签）、本服务 ApplyAll（兜底）。
    /// </summary>
    public static class SettingsService
    {
        /// <summary>读 appdata 应用到场景全部光点与星河墙标签。</summary>
        public static void ApplyAll()
        {
            AppData data = SaveDataService.LoadAppData();
            bool showPoints = data == null || data.settings == null || data.settings.showLightPoints;
            bool showLabels = data == null || data.settings == null || data.settings.showStarLabels;

            LightPointAnchor[] anchors = UnityEngine.Object.FindObjectsByType<LightPointAnchor>(
                UnityEngine.FindObjectsSortMode.None);
            for (int i = 0; i < anchors.Length; i++)
            {
                anchors[i].SetMarkerVisible(showPoints);
            }

            ConstellationWall[] walls = UnityEngine.Object.FindObjectsByType<ConstellationWall>(
                UnityEngine.FindObjectsSortMode.None);
            for (int i = 0; i < walls.Length; i++)
            {
                walls[i].SetLabelsVisible(showLabels);
            }
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
    }
}
