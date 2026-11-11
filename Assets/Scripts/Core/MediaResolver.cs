using System.IO;
using UnityEngine;

namespace MemoryCorridor.Core
{
    /// <summary>
    /// 素材路径解析：优先 PersistentDataPath（用户上传），回退 StreamingAssets（随包 Demo）。
    /// 编辑器/PC 两处都可直接 File 读；Android 上 StreamingAssets 不能 File 读，
    /// 需 UnityWebRequest 异步化（W3 前处理，见 MEMORY 待办）。
    /// </summary>
    public static class MediaResolver
    {
        /// <summary>返回存在的本机绝对路径；都不存在返回 null。</summary>
        public static string ResolveExisting(string relative)
        {
            if (string.IsNullOrEmpty(relative)) return null;

            string persistent = Path.Combine(SaveDataService.PersistentRoot, relative);
            if (File.Exists(persistent)) return persistent;

            string streaming = Path.Combine(SaveDataService.StreamingRoot, relative);
            if (File.Exists(streaming)) return streaming;

            return null;
        }

        /// <summary>解析不到时给出诊断串（日志用）。</summary>
        public static string DescribeMiss(string relative)
        {
            return relative + " (persistent=" + SaveDataService.PersistentRoot
                + ", streaming=" + SaveDataService.StreamingRoot + ")";
        }
    }
}
