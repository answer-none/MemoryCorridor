using System.Collections.Generic;
using MemoryCorridor.Core;
using MemoryCorridor.Media;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace MemoryCorridor.Interaction
{
    /// <summary>
    /// XRI 事件 → 光点契约桥：XRSimpleInteractable 的 hoverEntered/Exited 驱动
    /// SetHighlight + 预览小卡；selectEntered → 回访记录 + 音效 + 回忆重演 + 延迟出面板。
    /// 坑-025：RoomManager 在 Start 动态生成光点——改 Update 懒接线（E11：0.5s 退避轮询）。
    /// 坑-026：跨对象引用全部走 Configure 序列化字段；事件订阅在 Play 期 Start 重挂。
    /// </summary>
    public class XRIAnchorBridge : MonoBehaviour
    {
        [SerializeField] private MemoryPanel panel;
        [SerializeField] private RoutePlayer routePlayer;
        [SerializeField] private ReplaySequencer replaySequencer;
        [SerializeField] private HoverPreviewCard previewCard;
        [SerializeField] private AudioClip selectChime;
        [SerializeField] private MemoryCorridor.Media.ConstellationWall constellationWall;
        [SerializeField] private MemoryCorridor.Media.WelcomeBoard welcomeBoard;
        [SerializeField] private MemoryCorridor.Media.TutorialGuide tutorialGuide;

        private readonly HashSet<LightPointAnchor> wired = new HashSet<LightPointAnchor>();
        private Dictionary<string, MemoryFragment> fragmentsById;
        private int wiredLoggedCount = -1;
        private float wireTimer;
        private string pendingFragmentId;
        private bool pendingPanelShown;

        /// <summary>兼容访问器（真值在序列化字段——坑-026：编辑期属性赋值不持久化）。</summary>
        public MemoryCorridor.Media.ConstellationWall ConstellationWallRef
        {
            get { return constellationWall; }
            set { constellationWall = value; }
        }

        public MemoryCorridor.Media.WelcomeBoard WelcomeBoardRef
        {
            get { return welcomeBoard; }
            set { welcomeBoard = value; }
        }

        public MemoryCorridor.Media.TutorialGuide TutorialGuideRef
        {
            get { return tutorialGuide; }
            set { tutorialGuide = value; }
        }

        /// <summary>编辑器场景搭建注入引用。</summary>
        public void Configure(MemoryPanel panelRef, RoutePlayer routePlayerRef,
            ReplaySequencer replayRef, HoverPreviewCard previewCardRef, AudioClip selectChimeRef,
            MemoryCorridor.Media.ConstellationWall wallRef, MemoryCorridor.Media.WelcomeBoard welcomeRef,
            MemoryCorridor.Media.TutorialGuide tutorialRef)
        {
            panel = panelRef;
            routePlayer = routePlayerRef;
            replaySequencer = replayRef;
            previewCard = previewCardRef;
            selectChime = selectChimeRef;
            constellationWall = wallRef;
            welcomeBoard = welcomeRef;
            tutorialGuide = tutorialRef;
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
            if (constellationWall != null)
            {
                constellationWall.OnStarSelected -= OnWallStarSelected;
                constellationWall.OnStarSelected += OnWallStarSelected;
            }
        }

        private void OnWallStarSelected(string fragmentId)
        {
            pendingFragmentId = fragmentId;
            pendingPanelShown = false; // 面板由 OnReplayBeat（wall 源）延迟驱动
        }

        /// <summary>重演节拍回调：bridge/wall 两个来源统一——第③拍"重返当时"出面板（E7），
        /// 跳过直达 Done 时兜底显示；Done 后清 pending。</summary>
        private void SubscribeReplayBeats(ReplaySequencer replayRef)
        {
            if (replayRef == null) return;
            replayRef.OnBeat -= OnReplayBeat;
            replayRef.OnBeat += OnReplayBeat;
        }

        private void OnReplayBeat(ReplaySequencer.ReplayState state)
        {
            if (replaySequencer == null || string.IsNullOrEmpty(pendingFragmentId)) return;

            string source = replaySequencer.LastPlaySource;
            if (source != "bridge" && source != "wall") return;

            if (state == ReplaySequencer.ReplayState.Beat3_Return && !pendingPanelShown)
            {
                pendingPanelShown = true;
                ShowPanelFor(pendingFragmentId);
                if (tutorialGuide != null) tutorialGuide.Notify("panel"); // R4 引导推进
            }
            else if (state == ReplaySequencer.ReplayState.Done && !pendingPanelShown)
            {
                pendingPanelShown = true;
                ShowPanelFor(pendingFragmentId);
                if (tutorialGuide != null) tutorialGuide.Notify("panel");
            }
            else if (state == ReplaySequencer.ReplayState.Done)
            {
                pendingFragmentId = null;
            }
        }

        private void Update()
        {
            // R2 E11：接线轮询降频至 0.5s 一次（TickHovered 仍逐帧——预览计时依赖）
            wireTimer += Time.unscaledDeltaTime;
            if (wireTimer >= 0.5f)
            {
                wireTimer = 0f;
                EnsureWired();
            }
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
                    if (previewCard != null) previewCard.Hide(); // E2：离开即收预览卡
                });
                interactable.selectEntered.AddListener(delegate (SelectEnterEventArgs args)
                {
                    OnFragmentSelected(captured);
                });
                // E2：悬停 0.3s → 预览小卡（缩略图+标题+日期）+ 引导推进（R4）
                anchor.OnHoverPreview += delegate (string id)
                {
                    if (tutorialGuide != null) tutorialGuide.Notify("hover");
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
                    + (replaySequencer != null) + "，预览=" + (previewCard != null) + "）");
            }
        }

        /// <summary>选中：回访记录 → 引导收起 → 音效（anchor 内聚）→ 重演（延迟出面板）。</summary>
        private void OnFragmentSelected(LightPointAnchor anchor)
        {
            if (anchor == null) return;
            string fragmentId = anchor.FragmentId;

            if (routePlayer != null) routePlayer.MarkVisited(fragmentId);
            if (welcomeBoard != null && welcomeBoard.IsShowing)
            {
                welcomeBoard.NotifyUsed(); // E1：引导完成即收
            }
            if (tutorialGuide != null) tutorialGuide.Notify("select"); // R4 引导推进

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
