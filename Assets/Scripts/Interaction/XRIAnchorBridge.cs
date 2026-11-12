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
        [SerializeField] private HoverPreviewCard previewCard;
        [SerializeField] private AudioClip selectChime;

        private readonly HashSet<LightPointAnchor> wired = new HashSet<LightPointAnchor>();
        private Dictionary<string, MemoryFragment> fragmentsById;
        private int wiredLoggedCount = -1;
        private string pendingFragmentId;
        private bool pendingPanelShown;

        /// <summary>编辑器场景搭建注入引用。</summary>
        public void Configure(MemoryPanel panelRef, RoutePlayer routePlayerRef,
            ReplaySequencer replayRef, HoverPreviewCard previewCardRef, AudioClip selectChimeRef)
        {
            panel = panelRef;
            routePlayer = routePlayerRef;
            replaySequencer = replayRef;
            previewCard = previewCardRef;
            selectChime = selectChimeRef;
            SubscribeReplayBeats(replayRef);
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
            SubscribeReplayBeats(replaySequencer); // 坑-026：Play 期重挂事件订阅
        }

        /// <summary>重演节拍回调：bridge 发起的重演在第③拍"重返当时"才出面板（TEAM_PLAN E7，
        /// 避免剧透日记）；跳过直达 Done 时兜底显示。星河墙（wall）路径不在此处理。</summary>
        private void SubscribeReplayBeats(ReplaySequencer replayRef)
        {
            if (replayRef == null) return;
            replayRef.OnBeat -= OnReplayBeat;
            replayRef.OnBeat += OnReplayBeat;
        }

        private void OnReplayBeat(ReplaySequencer.ReplayState state)
        {
            if (replaySequencer == null || replaySequencer.LastPlaySource != "bridge") return;
            if (string.IsNullOrEmpty(pendingFragmentId)) return;

            if (state == ReplaySequencer.ReplayState.Beat3_Return && !pendingPanelShown)
            {
                pendingPanelShown = true;
                ShowPanelFor(pendingFragmentId);
            }
            else if (state == ReplaySequencer.ReplayState.Done && !pendingPanelShown)
            {
                pendingPanelShown = true;
                ShowPanelFor(pendingFragmentId);
            }
            else if (state == ReplaySequencer.ReplayState.Done)
            {
                pendingFragmentId = null;
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
                    if (previewCard != null) previewCard.Hide(); // R1 E2：离开即收预览卡
                });
                interactable.selectEntered.AddListener(delegate (SelectEnterEventArgs args)
                {
                    OnFragmentSelected(captured);
                });
                // R1 E2：悬停 0.3s → 预览小卡（缩略图+标题+日期）
                anchor.OnHoverPreview += delegate (string id)
                {
                    if (previewCard == null) return;
                    MemoryFragment fragment;
                    if (fragmentsById != null && fragmentsById.TryGetValue(id, out fragment))
                    {
                        previewCard.Show(fragment, captured.transform.position);
                    }
                };
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

        /// <summary>选中：回访记录 → 音效反馈 → 重演（延迟出面板，OnReplayBeat 驱动）。</summary>
        private void OnFragmentSelected(LightPointAnchor anchor)
        {
            if (anchor == null) return;
            string fragmentId = anchor.FragmentId;

            if (routePlayer != null) routePlayer.MarkVisited(fragmentId);

            MemoryFragment fragment;
            fragmentsById.TryGetValue(fragmentId, out fragment);

            if (replaySequencer != null && fragment != null)
            {
                pendingFragmentId = fragmentId;
                pendingPanelShown = false;
                replaySequencer.Play(fragment, anchor.transform.position, "bridge");
            }
            else if (panel != null && fragment != null)
            {
                panel.Show(fragment); // 无重演路径时立即显示（兜底）
            }
        }

        private void ShowPanelFor(string fragmentId)
        {
            if (panel == null) return;
            MemoryFragment fragment;
            if (fragmentsById != null && fragmentsById.TryGetValue(fragmentId, out fragment))
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
