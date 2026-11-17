using UnityEngine;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 呼吸式微高亮（R12 path 要素，MoL）：正弦驱动自发光，无箭头无文字，
    /// 任何时刻单焦点（挂载点唯一）。用于星尘信标与房间门柱。
    /// 挂载对象的 Renderer 材质需含 _EmissionColor（URP/Lit 系）。
    /// </summary>
    public class PulseEmissive : MonoBehaviour
    {
        [SerializeField] private Color baseColor = new Color(0.91f, 0.64f, 0.29f, 1f);
        [SerializeField] private float baseIntensity = 1.2f;
        [SerializeField] private float pulseRange = 0.8f;
        [SerializeField] private float speed = 1.6f;

        private Renderer targetRenderer;
        private Material materialInstance;

        private void Awake()
        {
            targetRenderer = GetComponent<Renderer>();
            if (targetRenderer != null)
            {
                materialInstance = targetRenderer.material; // 实例化，避免污染共享材质
                materialInstance.EnableKeyword("_EMISSION");
            }
        }

        private void Update()
        {
            if (materialInstance == null) return;
            float pulse = baseIntensity + pulseRange * 0.5f * (1f + Mathf.Sin(Time.unscaledTime * speed));
            materialInstance.SetColor("_EmissionColor", baseColor * pulse);
        }
    }
}
