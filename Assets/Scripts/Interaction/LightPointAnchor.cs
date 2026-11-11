using System;
using UnityEngine;

namespace MemoryCorridor.Interaction
{
    /// <summary>
    /// 物品光点锚点：关联一条记忆碎片；射线悬停高亮 + 预览事件；扳机选中事件。
    /// 褪色：FadeController 写入 fadeFactor（0=全暗 1=满亮），本组件只负责呈现。
    /// </summary>
    public class LightPointAnchor : MonoBehaviour
    {
        [Header("绑定")]
        [SerializeField] private string fragmentId;
        [SerializeField] private Renderer pointRenderer;
        [SerializeField] private Light glowLight;

        [Header("配色（按记忆类型，见视觉规范 §4.1）")]
        [SerializeField] private Color defaultColor = new Color(0.91f, 0.64f, 0.29f); // 琥珀
        [SerializeField] private Color highlightColor = new Color(0.35f, 0.78f, 0.88f); // 青蓝
        [SerializeField] private Color privateColor = new Color(0.78f, 0.42f, 0.42f);  // 暖红（私密）

        [Header("呼吸动画")]
        [SerializeField] private float breathePeriod = 3f;
        [SerializeField] private float breatheAmplitude = 0.25f;

        private Material materialInstance;
        private float fadeFactor = 1f;
        private bool isPrivate;
        private bool isHighlighted;
        private Color activeColor;

        public string FragmentId => fragmentId;
        /// <summary>当前褪色系数（0=全暗 1=满亮），冒烟/测试读取。</summary>
        public float CurrentFade => fadeFactor;
        /// <summary>悬停 0.3s 后触发，UI 层弹出预览小卡片。</summary>
        public event Action<string> OnHoverPreview;
        public event Action<string> OnSelected;

        private float hoverTimer;
        private bool previewFired;

        private void Awake()
        {
            if (pointRenderer != null)
            {
                materialInstance = pointRenderer.material; // 实例化，避免共享材质被改
                activeColor = defaultColor;
            }
        }

        private void Update()
        {
            if (materialInstance == null) return;

            float breathe = 1f - breatheAmplitude * 0.5f *
                (1f + Mathf.Sin(2f * Mathf.PI * Time.time / breathePeriod));
            float emissive = breathe * fadeFactor;
            Color target = isHighlighted ? highlightColor : activeColor;
            materialInstance.SetColor("_EmissionColor", target * emissive * 2f);
            if (glowLight != null)
            {
                glowLight.color = target;
                glowLight.intensity = 2f * emissive;
            }
            transform.localScale = Vector3.one * (0.6f + 0.4f * fadeFactor); // 褪色=萤火飞远变小
        }

        public void BindFragment(string id, bool isPrivateFragment = false)
        {
            fragmentId = id;
            isPrivate = isPrivateFragment;
            activeColor = isPrivate ? privateColor : defaultColor;
        }

        /// <summary>编辑器场景搭建注入引用（运行时 Awake 仍会缓存材质实例）。</summary>
        public void Configure(Renderer rendererRef, Light lightRef)
        {
            pointRenderer = rendererRef;
            glowLight = lightRef;
        }

        /// <summary>由 FadeController 驱动：daysSinceVisit → [fadeFloor,1]。</summary>
        public void SetFade(float factor)
        {
            fadeFactor = Mathf.Clamp01(factor);
        }

        public void SetHighlight(bool active)
        {
            isHighlighted = active;
            if (active)
            {
                hoverTimer = 0f;
                previewFired = false;
            }
        }

        /// <summary>每帧由射线系统调用：保持悬停状态并计时预览。</summary>
        public void TickHover()
        {
            if (previewFired) return;
            hoverTimer += Time.deltaTime;
            if (hoverTimer >= 0.3f)
            {
                previewFired = true;
                OnHoverPreview?.Invoke(fragmentId);
            }
        }

        public void Select()
        {
            OnSelected?.Invoke(fragmentId);
        }

        private void OnDestroy()
        {
            if (materialInstance != null) Destroy(materialInstance);
        }
    }
}
