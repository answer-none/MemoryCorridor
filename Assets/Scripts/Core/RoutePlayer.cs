using System;
using System.Collections.Generic;
using MemoryCorridor.Interaction;
using MemoryCorridor.Media;
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
        [SerializeField] private ConstellationWall constellationWall;

        private List<MemoryFragment> route = new List<MemoryFragment>();
        private int cursor = -1;
        private bool touring;

        public bool IsTouring => touring;
        public int Cursor => cursor;
        public int RouteCount => route.Count;

        /// <summary>编辑器搭建注入引用（坑-026：事件订阅不序列化，Play 期在 Start 重新挂）。</summary>
        public void Configure(GameObject beaconRef, RoomManager roomManagerRef, FadeController fadeRef,
            ConstellationWall wallRef)
        {
            beacon = beaconRef;
            roomManager = roomManagerRef;
            fadeController = fadeRef;
            constellationWall = wallRef;
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
            EndTourInternal(false); // 切房打断不建墙
        }

        /// <summary>开始走读：当前房间按 order 生成路线，信标落到第一站，收起星河墙。</summary>
        public bool StartTour()
        {
            RoomData room = (roomManager != null) ? roomManager.CurrentRoom() : null;
            if (room == null) return false;

            route = RouteEditor.OrderedFragments(room.id);
            if (route.Count == 0) return false;

            cursor = 0;
            touring = true;
            if (constellationWall != null) constellationWall.Clear();
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
            EndTourInternal(true);
        }

        /// <summary>buildWall=false 用于房间切换打断（避免新房间浮现旧房间星座）。</summary>
        private void EndTourInternal(bool buildWall)
        {
            bool wasTouring = touring;
            touring = false;
            cursor = -1;
            if (beacon != null) beacon.SetActive(false);
            if (buildWall && wasTouring && constellationWall != null && route.Count > 0)
            {
                constellationWall.Build(route);
                CaptureMemoryShot(); // R12 C：漫游完成自动截图（回忆卡片）
            }
        }

        /// <summary>R12 C：ScreenCapture 截图保存到 persistentDataPath/screenshots/。</summary>
        public bool LastCaptureRequested { get; private set; }

        private void CaptureMemoryShot()
        {
            LastCaptureRequested = true; // 坑-041：batchmode 下 ScreenCapture 不落盘——标志作为运行时证据
            string dir = System.IO.Path.Combine(Application.persistentDataPath, "screenshots");
            System.IO.Directory.CreateDirectory(dir);
            string fileName = "memory_shot_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
            string path = System.IO.Path.Combine(dir, fileName);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[RoutePlayer] 回忆卡片截图: " + path);
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
