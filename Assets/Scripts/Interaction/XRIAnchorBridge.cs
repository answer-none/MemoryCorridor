using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace MemoryCorridor.Interaction
{
    /// <summary>
    /// XRI 事件 → 光点契约桥：XRSimpleInteractable 的 hoverEntered/Exited 驱动
    /// SetHighlight，selectEntered 驱动 Select；悬停期间逐帧 TickHover（0.3s 预览不变）。
    /// 无论射线来自 XRI 手柄还是 XR Device Simulator，锚点契约保持一致（HANDOFF 决策）。
    /// </summary>
    public class XRIAnchorBridge : MonoBehaviour
    {
        private LightPointAnchor[] anchors;

        private void Start()
        {
            anchors = FindObjectsByType<LightPointAnchor>(FindObjectsSortMode.None);
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
                });
                wired++;
            }
            Debug.Log("[XRIAnchorBridge] 已桥接 " + wired + " 个光点");
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
