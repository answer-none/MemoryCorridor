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
        [SerializeField] private Color defaultColor = new Color(0.91f, 0.64f, 0.29f); // 琥珀=图片
        [SerializeField] private Color highlightColor = new Color(0.35f, 0.78f, 0.88f); // 青蓝（悬停态）
        [SerializeField] private Color privateColor = new Color(0.78f, 0.42f, 0.42f);  // 暖红（私密）
        [SerializeField] private Color audioColor = new Color(0.35f, 0.78f, 0.88f);    // 青蓝=录音
        [SerializeField] private Color diaryColor = new Color(0.92f, 0.88f, 0.82f);    // 米白=日记
        [SerializeField] private Color mixedColor = new Color(0.95f, 0.55f, 0.35f);    // 暖橙=混合

        [Header("音效（R1 E5：选中反馈，内聚于光点本体）")]
        [SerializeField] private AudioClip selectChime;
        [SerializeField] private Renderer glowShellRenderer; // R3 美学：光晕外球
        [SerializeField] private float baseScale = 0.45f; // 坑-039：光点基础尺寸（避免大球挡视线）

        [Header("符号浮标（R15 ①：MoL image 要素）")]
        [SerializeField] private LightPointSymbol symbol; // billboard 类型小牌（builder 注入）

        [Header("呼吸动画")]
        [SerializeField] private float breathePeriod = 3f;
        [SerializeField] private float breatheAmplitude = 0.25f;

        private Material materialInstance;
        private float fadeFactor = 1f;
        private float densityDim = 1f; // R16-2：过密簇视觉降密度系数（1=正常）
        private bool isPrivate;
        private bool isHighlighted;
        private Color activeColor;

        public string FragmentId => fragmentId;
        /// <summary>当前褪色系数（0=全暗 1=满亮），冒烟/测试读取。</summary>
        public float CurrentFade => fadeFactor;
        /// <summary>是否处于射线悬停高亮态（XRI 桥接驱动）。</summary>
        public bool IsHighlighted => isHighlighted;
        /// <summary>悬停 0.3s 后触发，UI 层弹出预览小卡片。</summary>
        public event Action<string> OnHoverPreview;
        public event Action<string> OnSelected;

        private float hoverTimer;
        private bool previewFired;
        private bool markerVisible = true;

        /// <summary>R5：光点标记显隐（只隐渲染，保留 collider/交互——规范 §5.9）。</summary>
        public void SetMarkerVisible(bool visible)
        {
            markerVisible = visible;
            if (pointRenderer != null) pointRenderer.enabled = visible;
            if (glowShellRenderer != null) glowShellRenderer.enabled = visible;
            if (glowLight != null) glowLight.enabled = visible;
            if (symbol != null) symbol.SetVisible(visible); // R15 ①：符号随显隐设置联动
        }

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
            float emissive = breathe * fadeFactor * densityDim; // R16-2：过密簇成员压暗
            Color target = isHighlighted ? highlightColor : activeColor;
            materialInstance.SetColor("_EmissionColor", target * emissive * 2f);
            if (glowLight != null)
            {
                glowLight.color = target;
                glowLight.intensity = 2f * emissive;
            }
            // R3 美学：光晕外球同色半透明，呼吸+褪色同步（萤火意象）
            if (glowShellRenderer != null)
            {
                Color shell = target;
                shell.a = Mathf.Clamp01(0.18f * emissive + (isHighlighted ? 0.15f : 0f));
                glowShellRenderer.material.SetColor("_Color", shell);
            }
            transform.localScale = Vector3.one * baseScale * (0.6f + 0.4f * fadeFactor); // 褪色=萤火飞远变小
        }

        public void BindFragment(string id, bool isPrivateFragment = false, string fragmentType = null)
        {
            fragmentId = id;
            isPrivate = isPrivateFragment;
            // R1 美学决策（TEAM_PLAN E4）：私密暖红优先，其余按记忆类型四色区分
            if (isPrivateFragment)
            {
                activeColor = privateColor;
            }
            else
            {
                switch (fragmentType)
                {
                    case "audio": activeColor = audioColor; break;
                    case "diary": activeColor = diaryColor; break;
                    case "mixed": activeColor = mixedColor; break;
                    case "image": activeColor = defaultColor; break;
                    default: activeColor = defaultColor; break;
                }
            }
            // R15 ①：按类型应用符号浮标（色形双编码——形状互不相似 + 同色系）
            if (symbol != null)
            {
                symbol.Apply(LightPointSymbol.KindFor(isPrivateFragment, fragmentType), activeColor);
            }
        }

        /// <summary>注入符号浮标（builder 在预制体上挂载后调用）。</summary>
        public void SetSymbol(LightPointSymbol symbolRef)
        {
            symbol = symbolRef;
        }

        /// <summary>R16-2：过密簇视觉降密度（0.55=压暗；不打散用户布点，只调观感）。</summary>
        public void SetDensityDim(float dim)
        {
            densityDim = Mathf.Clamp01(dim);
        }

        /// <summary>当前降密度系数（探针断言读取）。</summary>
        public float DensityDim => densityDim;

        /// <summary>符号浮标（冒烟/断言读取 kind 与可见性）。</summary>
        public LightPointSymbol Symbol => symbol;

        /// <summary>当前主色（冒烟/配色断言读取）。</summary>
        public Color CurrentColor => activeColor;

        /// <summary>编辑器场景搭建注入引用（运行时 Awake 仍会缓存材质实例）。</summary>
        public void Configure(Renderer rendererRef, Light lightRef)
        {
            pointRenderer = rendererRef;
            glowLight = lightRef;
        }

        /// <summary>注入选中音效（builder 在预制体上赋值，实例继承）。</summary>
        public void SetSelectChime(AudioClip clip)
        {
            selectChime = clip;
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

        /// <summary>注入光晕外球渲染器（builder 创建半透明壳后调用）。</summary>
        public void SetGlowShell(Renderer shell)
        {
            glowShellRenderer = shell;
        }

        public void Select()
        {
            if (selectChime != null)
            {
                AudioSource source = GetComponent<AudioSource>();
                if (source != null) source.PlayOneShot(selectChime);
            }
            OnSelected?.Invoke(fragmentId);
        }

        private void OnDestroy()
        {
            if (materialInstance != null) Destroy(materialInstance);
        }
    }
}
