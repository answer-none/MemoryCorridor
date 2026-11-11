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
        public static string AppDataFilePath =>
            Path.Combine(Application.persistentDataPath, "appdata.json");

        public static string MediaRoot =>
            Path.Combine(Application.persistentDataPath, "Media");

        public static string PanoramaRoot =>
            Path.Combine(Application.persistentDataPath, "Panoramas");

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
