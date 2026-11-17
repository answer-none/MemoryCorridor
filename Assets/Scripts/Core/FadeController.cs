using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MemoryCorridor.Core
{
    /// <summary>
    /// 记忆褪色机制（V2 §3.1，创新点）：
    /// lastVisitedAt 距今天数 → 褪色系数 fadeFactor ∈ [fadeFloor, 1]，
    /// 驱动光点亮度/缩放（LightPointAnchor）与音频低通（AudioLowPassFilter）。
    /// 挂在场景唯一对象上，按房间批量刷新。
    /// </summary>
    public class FadeController : MonoBehaviour
    {
        [SerializeField] private int fadeStartDays = 30;
        [SerializeField] private float fadeFloor = 0.25f;
        [SerializeField] private float fullFadeDays = 365f; // 衰减到地板值的天数

        // 褪色应用时机归 RoomManager（spawn 时逐光点 Apply）；
        // 坑-025：Start 自动应用与 spawn 存在时序竞态，已移除。ApplyAllFromDisk 保留为工具方法。

        /// <summary>从盘上 appdata.json 读取碎片，按 FragmentId 匹配场景内锚点并应用褪色。</summary>
        public int ApplyAllFromDisk()
        {
            AppData data = SaveDataService.LoadAppData();
            if (data == null || data.rooms == null) return 0;

            Dictionary<string, MemoryFragment> byId = new Dictionary<string, MemoryFragment>();
            for (int r = 0; r < data.rooms.Count; r++)
            {
                RoomData room = data.rooms[r];
                if (room.fragments == null) continue;
                for (int f = 0; f < room.fragments.Count; f++)
                {
                    byId[room.fragments[f].id] = room.fragments[f];
                }
            }

            Interaction.LightPointAnchor[] anchors =
                UnityEngine.Object.FindObjectsByType<Interaction.LightPointAnchor>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
            int applied = 0;
            for (int i = 0; i < anchors.Length; i++)
            {
                MemoryFragment fragment;
                if (byId.TryGetValue(anchors[i].FragmentId, out fragment))
                {
                    Apply(fragment, anchors[i]);
                    applied++;
                }
            }
            Debug.Log("[FadeController] 已应用褪色到 " + applied + "/" + anchors.Length + " 个光点");
            return applied;
        }

        /// <summary>计算碎片当前褪色系数（1=满亮）。</summary>
        public float ComputeFadeFactor(MemoryFragment fragment)
        {
            if (fragment == null || string.IsNullOrEmpty(fragment.lastVisitedAt))
            {
                return 1f; // 从未记录回访时间的新碎片视为常亮
            }

            DateTime lastVisit;
            if (!DateTime.TryParse(fragment.lastVisitedAt, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out lastVisit))
            {
                return 1f;
            }

            double days = (DateTime.UtcNow - lastVisit).TotalDays;
            if (days <= fadeStartDays)
            {
                return 1f;
            }

            float t = Mathf.Clamp01((float)((days - fadeStartDays) / Mathf.Max(1f, fullFadeDays - fadeStartDays)));
            return Mathf.Lerp(1f, fadeFloor, t);
        }

        /// <summary>把褪色应用到光点与音频（回访后由 RoutePlayer 调 MarkVisited 重置）。</summary>
        public void Apply(MemoryFragment fragment, Interaction.LightPointAnchor anchor)
        {
            if (anchor == null) return;
            float factor = ComputeFadeFactor(fragment);
            anchor.SetFade(factor);

            var audio = anchor.GetComponentInChildren<AudioSource>();
            if (audio != null)
            {
                var lowPass = audio.GetComponent<AudioLowPassFilter>();
                if (lowPass != null)
                {
                    // 满亮=22000Hz 全通；地板≈16k→600Hz"隔着时间"的闷响
                    lowPass.cutoffFrequency = Mathf.Lerp(600f, 22000f, factor);
                }
                audio.volume = Mathf.Lerp(0.2f, 1f, factor);
            }
        }

        public void SyncSettings(AppSettings settings)
        {
            if (settings == null) return;
            fadeStartDays = settings.fadeStartDays;
            fadeFloor = settings.fadeFloor;
        }
    }
}
