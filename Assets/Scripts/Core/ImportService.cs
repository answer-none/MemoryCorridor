using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace MemoryCorridor.Core
{
    /// <summary>
    /// 导入服务（P0 第 6 项，R3 补齐）：解压导出包 → 合并进本地 appdata。
    /// 冲突规则（V1 §5.12）：id 冲突自动追加后缀 _i2/_i3…，媒体文件同名改名，绝不覆盖。
    /// </summary>
    public static class ImportService
    {
        public class ImportResult
        {
            public bool success;
            public string message;
            public int importedRooms;
            public int importedFragments;
        }

        public static ImportResult Import(string zipPath)
        {
            ImportResult result = new ImportResult();
            if (!File.Exists(zipPath))
            {
                result.message = "zip 不存在: " + zipPath;
                return result;
            }

            AppData local = SaveDataService.LoadAppData();
            HashSet<string> localIds = CollectFragmentIds(local);
            HashSet<string> localRooms = new HashSet<string>();
            if (local != null && local.rooms != null)
            {
                for (int r = 0; r < local.rooms.Count; r++) localRooms.Add(local.rooms[r].id);
            }

            using (FileStream fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read))
            using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                ZipArchiveEntry jsonEntry = zip.GetEntry("appdata.json");
                if (jsonEntry == null)
                {
                    result.message = "包内缺 appdata.json";
                    return result;
                }

                AppData incoming;
                using (StreamReader reader = new StreamReader(jsonEntry.Open()))
                {
                    incoming = Newtonsoft.Json.JsonConvert.DeserializeObject<AppData>(reader.ReadToEnd());
                }
                if (incoming == null || incoming.rooms == null)
                {
                    result.message = "包内 appdata.json 解析失败";
                    return result;
                }

                if (local == null) { local = new AppData(); }
                if (local.rooms == null) { local.rooms = new List<RoomData>(); }

                for (int r = 0; r < incoming.rooms.Count; r++)
                {
                    RoomData room = incoming.rooms[r];
                    while (localRooms.Contains(room.id)) room.id = room.id + "_i" + (result.importedRooms + 1);
                    localRooms.Add(room.id);

                    if (room.fragments != null)
                    {
                        for (int f = 0; f < room.fragments.Count; f++)
                        {
                            MemoryFragment fragment = room.fragments[f];
                            string originalId = fragment.id;
                            while (localIds.Contains(fragment.id))
                            {
                                fragment.id = originalId + "_i" + (result.importedFragments + 1);
                            }
                            localIds.Add(fragment.id);
                            fragment.roomId = room.id; // 保持与（可能已改名的）房间一致
                            RewriteMediaPaths(zip, originalId, fragment);
                            result.importedFragments++;
                        }
                    }
                    local.rooms.Add(room);
                    result.importedRooms++;
                }
            }

            SaveDataService.SaveAppData(local);
            result.success = true;
            result.message = "导入 " + result.importedRooms + " 房间 / " + result.importedFragments + " 碎片";
            return result;
        }

        /// <summary>把包内该碎片的媒体文件落盘；重名自动改名并同步 mediaItems 路径。</summary>
        private static void RewriteMediaPaths(ZipArchive zip, string originalId, MemoryFragment fragment)
        {
            if (fragment.mediaItems == null) return;
            string mediaRoot = SaveDataService.MediaRoot;

            for (int i = 0; i < fragment.mediaItems.Count; i++)
            {
                MediaItem item = fragment.mediaItems[i];
                string entryName = item.path != null ? item.path.Replace('\\', '/') : null;
                if (string.IsNullOrEmpty(entryName)) continue;
                ZipArchiveEntry entry = zip.GetEntry(entryName);
                if (entry == null) continue;

                string fileName = Path.GetFileName(entryName);
                string targetDir = Path.Combine(mediaRoot, fragment.id);
                Directory.CreateDirectory(targetDir);
                string targetPath = Path.Combine(targetDir, fileName);
                string finalName = fileName;
                int copyIndex = 1;
                while (File.Exists(targetPath))
                {
                    string stem = Path.GetFileNameWithoutExtension(fileName);
                    string ext = Path.GetExtension(fileName);
                    finalName = stem + "_i" + copyIndex + ext;
                    targetPath = Path.Combine(targetDir, finalName);
                    copyIndex++;
                }
                using (Stream source = entry.Open())
                using (FileStream target = new FileStream(targetPath, FileMode.Create))
                {
                    source.CopyTo(target);
                }
                item.path = "Media/" + fragment.id + "/" + finalName;
            }
        }

        private static HashSet<string> CollectFragmentIds(AppData data)
        {
            HashSet<string> ids = new HashSet<string>();
            if (data == null || data.rooms == null) return ids;
            for (int r = 0; r < data.rooms.Count; r++)
            {
                if (data.rooms[r].fragments == null) continue;
                for (int f = 0; f < data.rooms[r].fragments.Count; f++)
                {
                    ids.Add(data.rooms[r].fragments[f].id);
                }
            }
            return ids;
        }
    }
}
