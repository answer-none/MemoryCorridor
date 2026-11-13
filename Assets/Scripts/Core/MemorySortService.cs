using System;
using System.Collections.Generic;
using System.Linq;

namespace MemoryCorridor.Core
{
    /// <summary>排序维度（R5）：手动排序（order）永远优先，多维排序仅用于陈列视图，互不覆写。</summary>
    public enum SortCriterion
    {
        Manual,     // order 字段（走读路线用）
        Time,       // 日记日期优先，缺失回退 createdAt，升序（旧→新，人生故事线）
        Tag,        // 类型分组：image → audio → diary → mixed（组内稳定）
        Relevance,  // 回访紧迫度：距 lastVisitedAt 越久越靠前（与褪色机制联动）
    }

    /// <summary>
    /// 多维排序服务（R5，数据层与 UI 解耦）：纯函数返回新列表，
    /// 不修改源列表、不写 order 字段——"手动排序优先"原则（TECH_DESIGN_V2 §5.4）。
    /// Time 视角：diary.date 优先、缺失回退 createdAt。
    /// Relevance 视角：距 lastVisitedAt 天数降序（最久未回访最靠前=最需回访）；未记录视为 0 天。
    /// </summary>
    public static class MemorySortService
    {
        public static List<MemoryFragment> Sorted(IList<MemoryFragment> source, SortCriterion criterion)
        {
            List<MemoryFragment> result = source != null ? new List<MemoryFragment>(source) : new List<MemoryFragment>();
            switch (criterion)
            {
                case SortCriterion.Time:
                    result = result
                        .OrderBy(f => SortDate(f), StringComparer.Ordinal) // yyyy-MM-dd 字典序即时间序
                        .ToList();
                    break;
                case SortCriterion.Tag:
                    result = result
                        .OrderBy(f => TagRank(f.type))
                        .ToList();
                    break;
                case SortCriterion.Relevance:
                    result = result
                        .OrderByDescending(f => DaysSinceVisit(f.lastVisitedAt ?? f.createdAt))
                        .ToList();
                    break;
                case SortCriterion.Manual:
                default:
                    result = result.OrderBy(f => f.order).ToList();
                    break;
            }
            return result;
        }

        /// <summary>排序时间键：diary.date 优先，缺失回退 createdAt，均缺失回退哨兵值（排最后）。</summary>
        private static string SortDate(MemoryFragment fragment)
        {
            if (fragment.diary != null && !string.IsNullOrEmpty(fragment.diary.date))
            {
                return fragment.diary.date;
            }
            if (!string.IsNullOrEmpty(fragment.createdAt) && fragment.createdAt.Length >= 10)
            {
                return fragment.createdAt.Substring(0, 10);
            }
            return "9999-12-31";
        }

        private static int TagRank(string type)
        {
            switch (type)
            {
                case "image": return 0;
                case "audio": return 1;
                case "diary": return 2;
                case "mixed": return 3;
                default: return 4;
            }
        }

        /// <summary>距 lastVisitedAt 的天数（ISO8601 解析失败或为空视为 0——最新鲜）。</summary>
        public static double DaysSinceVisit(string iso8601)
        {
            if (string.IsNullOrEmpty(iso8601)) return 0;
            DateTime visit;
            if (!DateTime.TryParse(iso8601, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out visit))
            {
                return 0;
            }
            return (DateTime.UtcNow - visit).TotalDays;
        }
    }
}
