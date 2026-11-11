using System;
using System.Collections.Generic;

namespace MemoryCorridor.Core
{
    /// <summary>房间 = 一张全景背景 + 一组碎片 + 环境音（V2 schema）。</summary>
    [Serializable]
    public class RoomData
    {
        public string id;
        public string title;
        public string panoramaPath;
        public string theme = "amber";   // amber / dusk …，驱动色调 Volume
        public string ambientPath;       // 房间环境音（相对 PersistentDataPath）
        public string visibility = "public"; // public / private（P1 房间级）
        public FragmentAnchor entryAnchor = new FragmentAnchor();
        public List<MemoryFragment> fragments = new List<MemoryFragment>();
    }
}
