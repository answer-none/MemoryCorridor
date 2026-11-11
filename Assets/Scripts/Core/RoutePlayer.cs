using System;
using System.Collections.Generic;
using MemoryCorridor.Interaction;
using UnityEngine;

namespace MemoryCorridor.Core
{
    /// <summary>
    /// 走读模式（TECH_DESIGN_V2 §5.7）：按 order 生成路线，星尘信标指示下一站；
    /// 回访即 MarkVisited（更新 lastVisitedAt → 褪色复位）。
    /// 全程定点传送（XRI Teleportation，W3 接入），本层只管路线状态。
    /// </summary>
    public class RoutePlayer : MonoBehaviour
    {
        [SerializeField] private GameObject beacon;
        [SerializeField] private RoomManager roomManager;
        [SerializeField] private FadeController fadeController;

        private List<MemoryFragment> route = new List<MemoryFragment>();
        private int cursor = -1;
        private bool touring;

        public bool IsTouring => touring;
        public int Cursor => cursor;
        public int RouteCount => route.Count;

        /// <summary>编辑器搭建注入引用（坑-026：事件订阅不序列化，Play 期在 Start 重新挂）。</summary>
        public void Configure(GameObject beaconRef, RoomManager roomManagerRef, FadeController fadeRef)
        {
            beacon = beaconRef;
            roomManager = roomManagerRef;
            fadeController = fadeRef;
            SubscribeRoomSwitch(roomManagerRef);
        }

        private void Start()
        {
            if (beacon != null) beacon.SetActive(false);
            SubscribeRoomSwitch(roomManager); // 坑-026：编辑期订阅随域重载丢失
        }

        private void SubscribeRoomSwitch(RoomManager roomManagerRef)
        {
            if (roomManagerRef != null)
            {
                roomManagerRef.OnRoomSwitched -= OnRoomSwitched;
                roomManagerRef.OnRoomSwitched += OnRoomSwitched;
            }
        }

        private void OnRoomSwitched(int roomIndex)
        {
            EndTour();
        }

        /// <summary>开始走读：当前房间按 order 生成路线，信标落到第一站。</summary>
        public bool StartTour()
        {
            RoomData room = (roomManager != null) ? roomManager.CurrentRoom() : null;
            if (room == null) return false;

            route = RouteEditor.OrderedFragments(room.id);
            if (route.Count == 0) return false;

            cursor = 0;
            touring = true;
            PlaceBeacon();
            Debug.Log("[RoutePlayer] 走读开始：" + room.title + "，" + route.Count + " 站");
            return true;
        }

        /// <summary>推进到下一站；路线走完返回 false 并收队。</summary>
        public bool Advance()
        {
            if (!touring) return false;
            cursor++;
            if (cursor >= route.Count)
            {
                EndTour();
                return false;
            }
            PlaceBeacon();
            return true;
        }

        public void EndTour()
        {
            touring = false;
            cursor = -1;
            if (beacon != null) beacon.SetActive(false);
        }

        /// <summary>回访：更新 lastVisitedAt 并把该光点褪色复位（创新点"记忆褪色"的闭环）。</summary>
        public bool MarkVisited(string fragmentId)
        {
            AppData data = SaveDataService.LoadAppData();
            if (data == null || data.rooms == null) return false;

            for (int r = 0; r < data.rooms.Count; r++)
            {
                RoomData room = data.rooms[r];
                if (room.fragments == null) continue;
                for (int f = 0; f < room.fragments.Count; f++)
                {
                    if (room.fragments[f].id != fragmentId) continue;
                    room.fragments[f].lastVisitedAt =
                        DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                    SaveDataService.SaveAppData(data);

                    GameObject anchorGo = GameObject.Find("LP_" + fragmentId);
                    if (anchorGo != null && fadeController != null)
                    {
                        LightPointAnchor anchor = anchorGo.GetComponent<LightPointAnchor>();
                        if (anchor != null) fadeController.Apply(room.fragments[f], anchor);
                    }
                    return true;
                }
            }
            return false;
        }

        private void PlaceBeacon()
        {
            if (beacon == null || cursor < 0 || cursor >= route.Count) return;
            GameObject anchorGo = GameObject.Find("LP_" + route[cursor].id);
            if (anchorGo == null) return;
            beacon.transform.position = anchorGo.transform.position;
            beacon.SetActive(true);
        }
    }
}
