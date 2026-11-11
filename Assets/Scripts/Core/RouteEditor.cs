using System;
using System.Collections.Generic;

namespace MemoryCorridor.Core
{
    /// <summary>
    /// 路线编排：手动排序（P0）——前后移动碎片并落盘；走读路线按 order 生成。
    /// VR 内抓取编排（W3）与本服务的数据契约一致。
    /// </summary>
    public static class RouteEditor
    {
        /// <summary>移动碎片顺序：delta=+1 后移 / -1 前移（按 order 值找交换对象，非列表下标）。成功返回 true。</summary>
        public static bool MoveFragment(string fragmentId, int delta)
        {
            AppData data = SaveDataService.LoadAppData();
            if (data == null || data.rooms == null) return false;

            for (int r = 0; r < data.rooms.Count; r++)
            {
                RoomData room = data.rooms[r];
                if (room.fragments == null) continue;

                MemoryFragment self = FindById(room.fragments, fragmentId);
                if (self == null) continue;

                int targetOrder = self.order + delta;
                if (targetOrder < 0 || targetOrder >= room.fragments.Count) return false;

                MemoryFragment other = FindByOrder(room.fragments, targetOrder);
                if (other == null || other == self) return false;

                int oldOrder = self.order;
                self.order = targetOrder;
                other.order = oldOrder;

                SaveDataService.SaveAppData(data);
                return true;
            }
            return false;
        }

        /// <summary>某房间按 order 升序的碎片路线（读盘最新数据）。</summary>
        public static List<MemoryFragment> OrderedFragments(string roomId)
        {
            List<MemoryFragment> ordered = new List<MemoryFragment>();
            AppData data = SaveDataService.LoadAppData();
            if (data == null || data.rooms == null) return ordered;

            for (int r = 0; r < data.rooms.Count; r++)
            {
                RoomData room = data.rooms[r];
                if (room.id != roomId || room.fragments == null) continue;
                ordered.AddRange(room.fragments);
                break;
            }
            ordered.Sort(delegate (MemoryFragment a, MemoryFragment b) { return a.order.CompareTo(b.order); });
            return ordered;
        }

        private static MemoryFragment FindById(List<MemoryFragment> fragments, string id)
        {
            for (int i = 0; i < fragments.Count; i++)
            {
                if (fragments[i].id == id) return fragments[i];
            }
            return null;
        }

        private static MemoryFragment FindByOrder(List<MemoryFragment> fragments, int order)
        {
            for (int i = 0; i < fragments.Count; i++)
            {
                if (fragments[i].order == order) return fragments[i];
            }
            return null;
        }
    }
}
