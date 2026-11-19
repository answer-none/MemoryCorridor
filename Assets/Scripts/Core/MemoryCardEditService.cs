using System;
using System.Collections.Generic;
using UnityEngine;

namespace MemoryCorridor.Core
{
    /// <summary>
    /// 记忆卡片编辑服务（R21 P2 落地）：标题/日期/正文的统一编辑入口——
    /// 校验（空标题兜底 / 标题≤32 字 / 日期 yyyy-MM-dd / 正文≤500 字）→ 写 AppData → 落盘 →
    /// OnFragmentEdited 事件驱动 MemoryPanel 刷新显示。真实输入链路：浏览器 ImportServer POST（已接线）。
    /// </summary>
    public static class MemoryCardEditService
    {
        public const int MaxTitleLen = 32;
        public const int MaxTextLen = 500;
        public const string FallbackTitle = "未命名记忆";

        /// <summary>编辑结果（探针断言/HTTP 响应读取）。</summary>
        public class EditResult
        {
            public bool Ok;
            public string Message;
            public string AppliedTitle;
        }

        /// <summary>碎片被编辑保存后触发（MemoryPanel 订阅刷新显示）。
        /// 坑-057：HTTP 监听线程不能直接 Invoke（Unity API 仅主线程）——Apply 入队，主线程 Pump() 出队触发。</summary>
        public static event Action<MemoryFragment> OnFragmentEdited;
        private static readonly Queue<MemoryFragment> refreshQueue = new Queue<MemoryFragment>();

        /// <summary>主线程每帧调用（MemoryPanel.Update），把待刷新碎片派发给订阅者。</summary>
        public static void Pump()
        {
            while (refreshQueue.Count > 0)
            {
                var handler = OnFragmentEdited;
                if (handler != null) handler(refreshQueue.Dequeue());
                else refreshQueue.Dequeue();
            }
        }

        /// <summary>
        /// 应用编辑：任何一档校验失败即拒（不写盘）；空标题走兜底值（FallbackTitle）。
        /// data 由调用方传入（LoadAppData 产物），保存由本服务负责。
        /// </summary>
        public static EditResult Apply(AppData data, string fragmentId, string newTitle, string newDate, string newText)
        {
            var result = new EditResult();
            if (data == null || string.IsNullOrEmpty(fragmentId))
            {
                result.Message = "missing data/fragmentId";
                return result;
            }

            // ① 空标题兜底（不拒）：trim 后为空 → 未命名记忆
            string title = (newTitle ?? "").Trim();
            if (title.Length == 0) title = FallbackTitle;
            // ② 超长拒
            if (title.Length > MaxTitleLen)
            {
                result.Message = "title too long (>" + MaxTitleLen + ")";
                return result;
            }
            // ③ 日期格式拒（空日期=保留原值）
            string date = (newDate ?? "").Trim();
            if (date.Length > 0)
            {
                DateTime parsed;
                if (!DateTime.TryParseExact(date, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out parsed))
                {
                    result.Message = "bad date format (expect yyyy-MM-dd): " + date;
                    return result;
                }
            }
            // ④ 正文超长拒
            string text = newText ?? "";
            if (text.Length > MaxTextLen)
            {
                result.Message = "text too long (>" + MaxTextLen + ")";
                return result;
            }

            MemoryFragment target = FindFragment(data, fragmentId);
            if (target == null)
            {
                result.Message = "fragment not found: " + fragmentId;
                return result;
            }

            target.title = title;
            if (target.diary == null) target.diary = new DiaryEntry();
            if (date.Length > 0) target.diary.date = date;
            target.diary.text = text;
            SaveDataService.SaveAppData(data);

            result.Ok = true;
            result.Message = "edited: " + fragmentId;
            result.AppliedTitle = title;
            refreshQueue.Enqueue(target); // 坑-057：跨线程安全，主线程 Pump 派发
            Debug.Log("[CardEdit] 已保存: " + fragmentId + " → \"" + title + "\"（" + target.diary.date + "）");
            return result;
        }

        /// <summary>便捷重载：内部 LoadAppData（探针/轻量调用）。</summary>
        public static EditResult Apply(string fragmentId, string newTitle, string newDate, string newText)
        {
            return Apply(SaveDataService.LoadAppData(), fragmentId, newTitle, newDate, newText);
        }

        private static MemoryFragment FindFragment(AppData data, string fragmentId)
        {
            for (int r = 0; r < data.rooms.Count; r++)
            {
                if (data.rooms[r].fragments == null) continue;
                for (int f = 0; f < data.rooms[r].fragments.Count; f++)
                {
                    if (data.rooms[r].fragments[f].id == fragmentId) return data.rooms[r].fragments[f];
                }
            }
            return null;
        }
    }
}
