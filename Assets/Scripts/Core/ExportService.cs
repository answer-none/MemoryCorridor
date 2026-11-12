using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace MemoryCorridor.Core
{
    /// <summary>
    /// 导出服务（P0 第 6 项，R3 补齐）：appdata + 媒体 + 全景 打包 zip。
    /// 隐私过滤硬规则：visibility=private 的碎片与房间**不写入导出包**（JSON 层与文件层双重过滤）。
    /// 产物：persistentDataPath/exports/memorycorridor_export_&lt;ts&gt;.zip
    /// </summary>
    public static class ExportService
    {
        public static string ExportAll()
        {
            AppData data = SaveDataService.LoadAppData();
            if (data == null || data.rooms == null || data.rooms.Count == 0)
            {
                throw new InvalidOperationException("appdata 为空，无可导出内容");
            }

            // JSON 层过滤：剔除私密房间与私密碎片
            AppData exportData = BuildFilteredCopy(data);

            string exportDir = Path.Combine(SaveDataService.PersistentRoot, "exports");
            Directory.CreateDirectory(exportDir);
            string zipPath = Path.Combine(exportDir,
                "memorycorridor_export_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".zip");

            using (FileStream fs = new FileStream(zipPath, FileMode.Create))
            using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(exportData,
                    Newtonsoft.Json.Formatting.Indented);
                ZipArchiveEntry jsonEntry = zip.CreateEntry("appdata.json", CompressionLevel.Optimal);
                using (StreamWriter writer = new StreamWriter(jsonEntry.Open()))
                {
                    writer.Write(json);
                }

                // 文件层过滤：仅公开碎片的媒体随包
                foreach (RoomData room in exportData.rooms)
                {
                    CopyPanorama(zip, room.panoramaPath);
                    if (room.fragments == null) continue;
                    foreach (MemoryFragment fragment in room.fragments)
                    {
                        CopyFragmentMedia(zip, fragment);
                    }
                }
            }
            return zipPath;
        }

        /// <summary>深拷贝并过滤：private 房间整间剔除；private 碎片剔除。</summary>
        public static AppData BuildFilteredCopy(AppData source)
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(source);
            AppData copy = Newtonsoft.Json.JsonConvert.DeserializeObject<AppData>(json);
            List<RoomData> keptRooms = new List<RoomData>();
            for (int r = 0; r < copy.rooms.Count; r++)
            {
                RoomData room = copy.rooms[r];
                if (room.visibility == "private") continue; // 房间级私密整间剔除
                List<MemoryFragment> kept = new List<MemoryFragment>();
                if (room.fragments != null)
                {
                    for (int f = 0; f < room.fragments.Count; f++)
                    {
                        if (room.fragments[f].visibility == "private") continue;
                        kept.Add(room.fragments[f]);
                    }
                }
                room.fragments = kept;
                keptRooms.Add(room);
            }
            copy.rooms = keptRooms;
            return copy;
        }

        private static void CopyPanorama(ZipArchive zip, string relative)
        {
            if (string.IsNullOrEmpty(relative)) return;
            string resolved = MediaResolver.ResolveExisting(relative);
            if (resolved == null) return;
            ZipArchiveEntry entry = zip.CreateEntry(relative.Replace('\\', '/'), CompressionLevel.Optimal);
            using (Stream source = File.OpenRead(resolved))
            using (Stream target = entry.Open())
            {
                source.CopyTo(target);
            }
        }

        private static void CopyFragmentMedia(ZipArchive zip, MemoryFragment fragment)
        {
            if (fragment.mediaItems == null) return;
            for (int i = 0; i < fragment.mediaItems.Count; i++)
            {
                MediaItem item = fragment.mediaItems[i];
                if (string.IsNullOrEmpty(item.path)) continue;
                string resolved = MediaResolver.ResolveExisting(item.path);
                if (resolved == null) continue;
                ZipArchiveEntry entry = zip.CreateEntry(item.path.Replace('\\', '/'), CompressionLevel.Optimal);
                using (Stream source = File.OpenRead(resolved))
                using (Stream target = entry.Open())
                {
                    source.CopyTo(target);
                }
            }
        }
    }
}
