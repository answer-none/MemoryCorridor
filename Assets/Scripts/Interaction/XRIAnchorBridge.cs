using System.Collections.Generic;
using MemoryCorridor.Core;
using MemoryCorridor.Media;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace MemoryCorridor.Interaction
{
    /// <summary>
    /// XRI 事件 → 光点契约桥：XRSimpleInteractable 的 hoverEntered/Exited 驱动
    /// SetHighlight，selectEntered 驱动 Select 并弹出记忆面板；悬停期间逐帧 TickHover。
    /// 无论射线来自 XRI 手柄还是 XR Device Simulator，锚点契约保持一致（HANDOFF 决策）。
    /// </summary>
    public class XRIAnchorBridge : MonoBehaviour
    {
        [SerializeField] private MemoryPanel panel;

        private LightPointAnchor[] anchors;
        private Dictionary<string, MemoryFragment> fragmentsById;

        /// <summary>编辑器场景搭建注入面板引用。</summary>
        public void Configure(MemoryPanel panelRef)
        {
            panel = panelRef;
        }

        private void Start()
        {
            anchors = FindObjectsByType<LightPointAnchor>(FindObjectsSortMode.None);

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

            int wired = 0;
            for (int i = 0; i < anchors.Length; i++)
            {
                LightPointAnchor anchor = anchors[i];
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
                    captured.Select();
                    ShowPanelFor(captured.FragmentId);
                });
                wired++;
            }
            Debug.Log("[XRIAnchorBridge] 已桥接 " + wired + " 个光点（面板=" + (panel != null) + "，碎片表=" + fragmentsById.Count + "）");
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

        private void Update()
        {
            if (anchors == null) return;
            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i] != null && anchors[i].IsHighlighted)
                {
                    anchors[i].TickHover();
                }
            }
        }
    }
}
