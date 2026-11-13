using UnityEngine;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 新手引导（R4）：视线下方的分步提示条，事件+计时双驱动自动推进。
    /// 步骤：①认识光点 → ②走近悬停预览 → ③扳机选中重演 → ④面板/星尘 → 收起。
    /// HUD 固定在相机下前方（跟 yaw 不跟 pitch，防眩晕 §5.3）。
    /// </summary>
    public class TutorialGuide : MonoBehaviour
    {
        [SerializeField] private float autoStepSeconds = 9f;

        private GameObject rootObject;
        private TextMesh hintText;
        private int stepIndex;
        private float stepTimer;

        private static readonly string[] Steps =
        {
            "环顾四周——发光的光点是一段记忆（WASD 移动）",
            "走近它，悬停半秒即可预览",
            "扣下扳机选中，回忆会重演",
            "面板右上 × 可关闭 · 跟随星尘走完一条路线",
            "右前方有设置面板：开关光点标记与星点标签", // R6：设置入口可达性
        };

        /// <summary>只读访问第 index 步文案（断言用）。</summary>
        public string StepText(int index)
        {
            return (index >= 0 && index < Steps.Length) ? Steps[index] : null;
        }

        public int StepCount => Steps.Length;


        public bool IsShowing => rootObject != null && rootObject.activeSelf;
        public string HintText => hintText != null ? hintText.text : null;
        public int StepIndex => stepIndex;

        private void Awake()
        {
            EnsureBuilt();
        }

        private void Start()
        {
            EnsureBuilt();
            ShowStep(0);
        }

        /// <summary>事件推进（桥接器调用）：hover → 步骤2；select → 步骤3；panel → 步骤4。</summary>
        public void Notify(string eventName)
        {
            switch (eventName)
            {
                case "hover": if (stepIndex < 1) ShowStep(1); break;
                case "select": if (stepIndex < 2) ShowStep(2); break;
                case "panel": if (stepIndex < 3) ShowStep(3); break;
            }
        }

        private void ShowStep(int index)
        {
            EnsureBuilt();
            if (index >= Steps.Length)
            {
                rootObject.SetActive(false);
                return;
            }
            stepIndex = index;
            stepTimer = 0f;
            if (hintText != null)
            {
                hintText.text = Steps[index];
                hintText.color = index == 0 ? UiTokens.Amber : UiTokens.Cream;
            }
            rootObject.SetActive(true);
        }

        private void EnsureBuilt()
        {
            if (rootObject != null) return;

            rootObject = gameObject;
            MeshRenderer boardRenderer = GetComponent<MeshRenderer>();
            if (boardRenderer == null) boardRenderer = gameObject.AddComponent<MeshRenderer>();
            Shader spriteShader = Shader.Find("MemoryCorridor/Sprite3D");
            Material boardMat = new Material(spriteShader);
            boardMat.mainTexture = RoundedRectTexture.RoundedRect(
                760, 130, 16, new Color(0.10f, 0.08f, 0.13f, 0.86f),
                new Color(0.91f, 0.64f, 0.29f), 2);
            boardMat.SetColor("_Color", Color.white);
            boardRenderer.material = boardMat;
            transform.localScale = new Vector3(0.62f, 0.11f, 1f);

            GameObject textGo = new GameObject("Hint");
            textGo.transform.SetParent(transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            MeshRenderer textRenderer = textGo.AddComponent<MeshRenderer>();
            hintText = textGo.AddComponent<TextMesh>();
            Font font = UiTextFactory.Legacy;
            hintText.font = font;
            textRenderer.sharedMaterial = font.material;
            hintText.fontSize = 46;
            hintText.characterSize = 0.011f;
            hintText.anchor = TextAnchor.MiddleCenter;
            hintText.alignment = TextAlignment.Center;
            hintText.color = UiTokens.Cream;
        }

        private void Update()
        {
            if (rootObject == null || !rootObject.activeSelf) return;

            // HUD 跟随：相机下前方 0.8m，随 yaw 旋转（不跟 pitch）
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                transform.position = cam.transform.position + forward * 0.8f + Vector3.down * 0.32f;
                transform.rotation = Quaternion.LookRotation(forward);
            }

            // 计时兜底：用户不操作也按节奏推进（每步 autoStepSeconds）
            stepTimer += Time.unscaledDeltaTime;
            if (stepTimer >= autoStepSeconds)
            {
                ShowStep(stepIndex + 1);
            }
        }
    }
}
