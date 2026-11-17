using System.Collections.Generic;
using MemoryCorridor.Media;
using UnityEngine;

namespace MemoryCorridor.Interaction
{
    /// <summary>
    /// 单房间光点密度管理（R16-2，BENCHMARK ⑤ 认知负荷控制）：
    /// 单房间光点 > WarnThreshold 时构建期/运行时告警；过密簇（间距 < ClusterRadius 的相邻点 ≥
    /// DimClusterMin 个）自动分组做"视觉降密度"——不打散用户布点，只把簇内次序靠后的成员
    /// 压暗（SetDensityDim）并缩小符号，簇首保持全亮作为簇锚点。走近后各点交互不变。
    /// </summary>
    public static class LightPointDensityManager
    {
        public const float ClusterRadius = 0.6f;   // 归簇阈值（米）：相邻点间距小于此值视为同簇
        public const int WarnThreshold = 12;       // 单房间光点上限告警阈值
        public const int DimClusterMin = 3;        // 簇内成员 ≥ 此数才视觉降密度
        public const float DimFactor = 0.35f;      // 降密度压暗系数（对比图可辨度优先）
        public const float DimSymbolScale = 0.7f;  // 降密度成员符号缩放

        public static int DensityCount { get; private set; }
        public static int ClusterCount { get; private set; }
        public static bool Warned { get; private set; }
        public static int DimmedCount { get; private set; }

        /// <summary>
        /// 贪心归簇：未分配点作种子，收距种子 ClusterRadius 内的未分配点成簇（单遍）。
        /// </summary>
        public static List<List<LightPointAnchor>> ComputeClusters(List<LightPointAnchor> anchors)
        {
            var clusters = new List<List<LightPointAnchor>>();
            var assigned = new HashSet<LightPointAnchor>();
            foreach (LightPointAnchor seed in anchors)
            {
                if (seed == null || assigned.Contains(seed)) continue;
                var cluster = new List<LightPointAnchor> { seed };
                assigned.Add(seed);
                foreach (LightPointAnchor other in anchors)
                {
                    if (other == null || assigned.Contains(other)) continue;
                    if (Vector3.Distance(seed.transform.position, other.transform.position) < ClusterRadius)
                    {
                        cluster.Add(other);
                        assigned.Add(other);
                    }
                }
                clusters.Add(cluster);
            }
            return clusters;
        }

        /// <summary>
        /// 检查并应用：返回 true 表示触发过告警或存在降密度簇。幂等可重跑（先 ResetVisual）。
        /// </summary>
        public static bool Check(List<LightPointAnchor> anchors)
        {
            ResetVisual();
            DensityCount = anchors != null ? anchors.Count : 0;
            Warned = DensityCount > WarnThreshold;
            if (Warned)
            {
                Debug.LogWarning("[DensityManager] 光点密度告警：单房间 " + DensityCount
                    + " 个 > 上限 " + WarnThreshold + "——过密簇将自动视觉降密度（不打散布点）");
            }

            List<List<LightPointAnchor>> clusters = ComputeClusters(anchors);
            ClusterCount = clusters.Count;
            DimmedCount = 0;
            foreach (List<LightPointAnchor> cluster in clusters)
            {
                if (cluster.Count < DimClusterMin) continue;
                // 簇内按距簇质心排序：首成员全亮作簇锚点，其余压暗+缩符号
                Vector3 center = Vector3.zero;
                foreach (LightPointAnchor a in cluster) center += a.transform.position;
                center /= cluster.Count;
                cluster.Sort((x, y) =>
                    (x.transform.position - center).sqrMagnitude
                    .CompareTo((y.transform.position - center).sqrMagnitude));
                for (int i = 1; i < cluster.Count; i++)
                {
                    LightPointAnchor member = cluster[i];
                    member.SetDensityDim(DimFactor);
                    if (member.Symbol != null)
                    {
                        member.Symbol.transform.localScale = Vector3.one * UiTokens.SymbolSize * DimSymbolScale;
                    }
                    DimmedCount++;
                }
            }
            return Warned || DimmedCount > 0;
        }

        /// <summary>恢复全亮/全尺寸（切房/探针清理时调用）。</summary>
        public static void ResetVisual()
        {
            DensityCount = 0;
            ClusterCount = 0;
            Warned = false;
            DimmedCount = 0;
        }
    }
}
