using MemoryCorridor.Core;
using UnityEngine;

namespace MemoryCorridor.Core
{
    /// <summary>
    /// 音频设置服务（R2，TEAM_PLAN E9）：appdata.settings 的三路音量的单一消费点。
    /// masterVolume → AudioListener.volume（全局总控）；
    /// narrationVolume → 面板录音/旁白；ambientVolume → 房间环境音（AmbienceController 读）。
    /// 在主线程初始化一次（WarmSettings），随后只读缓存。
    /// </summary>
    public static class AudioSettingsService
    {
        private static float masterVolume = 1f;
        private static float narrationVolume = 1f;
        private static float ambientVolume = 0.6f;
        private static bool warmed;

        public static float MasterVolume => masterVolume;
        public static float NarrationVolume => narrationVolume;
        public static float AmbientVolume => ambientVolume;

        /// <summary>主线程入口调用（bridge/panel Start 等），读 appdata 并应用到 AudioListener。</summary>
        public static void WarmSettings()
        {
            AppData data = SaveDataService.LoadAppData();
            if (data != null && data.settings != null)
            {
                masterVolume = Mathf.Clamp01(data.settings.masterVolume);
                narrationVolume = Mathf.Clamp01(data.settings.narrationVolume);
                ambientVolume = Mathf.Clamp01(data.settings.ambientVolume);
            }
            AudioListener.volume = masterVolume;
            warmed = true;
        }

        public static bool IsWarmed => warmed;
    }
}
