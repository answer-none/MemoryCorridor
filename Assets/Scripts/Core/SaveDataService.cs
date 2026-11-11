using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace MemoryCorridor.Core
{
    /// <summary>
    /// appdata.json 读写（Newtonsoft 实现）。
    /// 坑-004：禁用 JsonUtility——嵌套 null 字段行为不可控。
    /// </summary>
    public static class SaveDataService
    {
        // 坑-024：Application.persistentDataPath / streamingAssetsPath 只能主线程调用；
        // 后台线程（ImportServer HTTP 处理）必须读这里缓存的主线程预热值。
        private static string cachedPersistentPath;
        private static string cachedStreamingPath;

        /// <summary>主线程调用一次（ImportServer.Start 等入口），缓存路径供后台线程使用。</summary>
        public static void WarmCache()
        {
            cachedPersistentPath = Application.persistentDataPath;
            cachedStreamingPath = Application.streamingAssetsPath;
        }

        /// <summary>持久化根目录：主线程直接取，后台线程读 WarmCache 缓存。</summary>
        public static string PersistentRoot
        {
            get { return cachedPersistentPath ?? Application.persistentDataPath; }
        }

        /// <summary>StreamingAssets 根目录：语义同 PersistentRoot。</summary>
        public static string StreamingRoot
        {
            get { return cachedStreamingPath ?? Application.streamingAssetsPath; }
        }

        public static string AppDataFilePath =>
            Path.Combine(PersistentRoot, "appdata.json");

        public static string MediaRoot =>
            Path.Combine(PersistentRoot, "Media");

        public static string PanoramaRoot =>
            Path.Combine(PersistentRoot, "Panoramas");

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include,
            Formatting = Formatting.Indented,
        };

        public static void SaveAppData(AppData data)
        {
            string json = JsonConvert.SerializeObject(data, JsonSettings);
            File.WriteAllText(AppDataFilePath, json);
        }

        public static AppData LoadAppData()
        {
            if (!File.Exists(AppDataFilePath))
            {
                return new AppData();
            }

            try
            {
                string json = File.ReadAllText(AppDataFilePath);
                AppData loaded = JsonConvert.DeserializeObject<AppData>(json);
                return loaded ?? new AppData();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveDataService] appdata.json 解析失败，返回新档：{e.Message}");
                return new AppData();
            }
        }
    }
}
