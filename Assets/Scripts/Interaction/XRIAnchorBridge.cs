using System.Collections.Generic;
using MemoryCorridor.Core;
using MemoryCorridor.Media;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace MemoryCorridor.Interaction
{
    /// <summary>
    /// XRI 事件 → 光点契约桥：XRSimpleInteractable 的 hoverEntered/Exited 驱动
    /// SetHighlight，selectEntered 驱动 Select + 回访记录 + 回忆重演 + 记忆面板。
    /// 坑-025：RoomManager 在 Start 动态生成光点，与本项目 Start 顺序存在竞态——
    /// 改为 Update 懒接线（已接线的锚点进 HashSet 去重，房间切换后自动接新光点）。
    /// </summary>
    public class XRIAnchorBridge : MonoBehaviour
    {
        [SerializeField] private MemoryPanel panel;
        [SerializeField] private RoutePlayer routePlayer;
        [SerializeField] private ReplaySequencer replaySequencer;

        private readonly HashSet<LightPointAnchor> wired = new HashSet<LightPointAnchor>();
        private Dictionary<string, MemoryFragment> fragmentsById;
        private int wiredLoggedCount = -1;

        /// <summary>编辑器搭建注入引用。</summary>
        public void Configure(MemoryPanel panelRef, RoutePlayer routePlayerRef, ReplaySequencer replayRef)
        {
            panel = panelRef;
            routePlayer = routePlayerRef;
            replaySequencer = replayRef;
        }

        private void Start()
        {
            fragmentsById = new Dictionary<string, MemoryFragment>();
            AppData data = SaveDataService.LoadAppData();
            if (data != null && data.rooms != null)
            {
                for (int r = 0; r < data.rooms.Count; r++)
                {
                    RoomData room = data.rooms[r];
                    if (room.fragments == null) continue;
                    for (int f = 0; f < room.fragments.Count; f++)
                    {
                        fragmentsById[room.fragments[f].id] = room.fragments[f];
                    }
                }
            }
        }

        private void Update()
        {
            EnsureWired();
            TickHoveredAnchors();
        }

        /// <summary>为尚未接线的新光点补接线（房间切换后自动生效）。</summary>
        private void EnsureWired()
        {
            LightPointAnchor[] anchors = FindObjectsByType<LightPointAnchor>(FindObjectsSortMode.None);
            int newWired = 0;
            for (int i = 0; i < anchors.Length; i++)
            {
                LightPointAnchor anchor = anchors[i];
                if (wired.Contains(anchor)) continue;

                XRSimpleInteractable interactable = anchor.GetComponent<XRSimpleInteractable>();
                if (interactable == null)
                {
                    interactable = anchor.gameObject.AddComponent<XRSimpleInteractable>();
                }

                LightPointAnchor captured = anchor;
                interactable.hoverEntered.AddListener(delegate (HoverEnterEventArgs args)
                {
                    captured.SetHighlight(true);
                });
                interactable.hoverExited.AddListener(delegate (HoverExitEventArgs args)
                {
                    captured.SetHighlight(false);
                });
                interactable.selectEntered.AddListener(delegate (SelectEnterEventArgs args)
                {
                    OnFragmentSelected(captured.FragmentId);
                });
                wired.Add(anchor);
                newWired++;
            }

            if (newWired > 0 && wired.Count != wiredLoggedCount)
            {
                wiredLoggedCount = wired.Count;
                Debug.Log("[XRIAnchorBridge] 已桥接 " + wired.Count + " 个光点（面板="
                    + (panel != null) + "，走读=" + (routePlayer != null) + "，重演="
                    + (replaySequencer != null) + "）");
            }
        }

        private void OnFragmentSelected(string fragmentId)
        {
            MemoryFragment fragment;
            fragmentsById.TryGetValue(fragmentId, out fragment);

            if (routePlayer != null) routePlayer.MarkVisited(fragmentId);
            if (replaySequencer != null && fragment != null)
            {
                GameObject anchorGo = GameObject.Find("LP_" + fragmentId);
                Vector3 anchorPosition = (anchorGo != null)
                    ? anchorGo.transform.position
                    : Vector3.zero;
                replaySequencer.Play(fragment, anchorPosition);
            }
            if (panel != null && fragment != null)
            {
                panel.Show(fragment);
            }
        }

        private void TickHoveredAnchors()
        {
            foreach (LightPointAnchor anchor in wired)
            {
                if (anchor != null && anchor.IsHighlighted)
                {
                    anchor.TickHover();
                }
            }
        }
    }
}
