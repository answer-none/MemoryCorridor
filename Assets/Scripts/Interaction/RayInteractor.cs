using UnityEngine;

namespace MemoryCorridor.Interaction
{
    /// <summary>
    /// 射线交互（W1 冒烟版）：Editor 内鼠标驱动（左键=选中）。
    /// 真机/XR 阶段由 XRI 射线替换，但对锚点的调用契约不变：
    /// SetHighlight(bool) → TickHover() → Select()。
    /// </summary>
    public class RayInteractor : MonoBehaviour
    {
        [SerializeField] private float maxDistance = 12f;

        private LightPointAnchor hovered;
        private Camera sourceCamera;

        private void Update()
        {
            if (sourceCamera == null)
            {
                sourceCamera = GetComponent<Camera>();
                if (sourceCamera == null) sourceCamera = Camera.main;
                if (sourceCamera == null) return;
            }

            Ray ray = sourceCamera.ScreenPointToRay(Input.mousePosition);
            LightPointAnchor hit = CastRay(ray);

            if (hit != hovered)
            {
                if (hovered != null) hovered.SetHighlight(false);
                hovered = hit;
                if (hovered != null) hovered.SetHighlight(true);
            }

            if (hovered != null)
            {
                hovered.TickHover();
                if (Input.GetMouseButtonDown(0)) hovered.Select();
            }
        }

        private LightPointAnchor CastRay(Ray ray)
        {
            RaycastHit hitInfo;
            if (Physics.Raycast(ray, out hitInfo, maxDistance))
            {
                return hitInfo.collider.GetComponentInParent<LightPointAnchor>();
            }
            return null;
        }
    }
}
