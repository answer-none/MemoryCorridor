using MemoryCorridor.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 设置面板（R5）：世界空间两行开关（光点标记显隐 / 星点标签显隐），
    /// 点击即翻转 + 持久化（appdata.settings）+ 场景应用（SettingsService）。
    /// 位置：出生点右前方，与告示牌并列；圆角底板统一 tokens。
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        private TextMesh pointsToggleText;
        private TextMesh labelsToggleText;
        private bool showPoints = true;
        private bool showLabels = true;
        private Vector3 basePosition;
        private float bobPhase; // R6：轻微浮动显眼化（入口可达性）

        public string PointsToggleText => pointsToggleText != null ? pointsToggleText.text : null;
        public string LabelsToggleText => labelsToggleText != null ? labelsToggleText.text : null;


        private void Awake()
        {
            EnsureBuilt();
        }

        private void Start()
        {
            AudioSettingsService.WarmSettings();
            basePosition = transform.position;
            bobPhase = 0f;
            SyncFromDisk();
        }

        private void Update()
        {
            // R6 入口可达性：0.02m 正弦浮动，远看也能注意到
            bobPhase += Time.unscaledDeltaTime;
            transform.position = basePosition + Vector3.up * (0.02f * Mathf.Sin(bobPhase * 2f));
        }

        private void EnsureBuilt()
        {
            if (pointsToggleText != null) return;

            MeshRenderer boardRenderer = GetComponent<MeshRenderer>();
            if (boardRenderer == null) boardRenderer = gameObject.AddComponent<MeshRenderer>();
            Shader spriteShader = Shader.Find("MemoryCorridor/Sprite3D");
            Material boardMat = new Material(spriteShader);
            boardMat.mainTexture = RoundedRectTexture.RoundedRect(
                460, 300, 18, new Color(0.10f, 0.08f, 0.13f, 0.92f),
                new Color(0.91f, 0.64f, 0.29f), 2);
            boardMat.SetColor("_Color", Color.white);
            boardRenderer.material = boardMat;
            transform.localScale = new Vector3(0.46f, 0.30f, 1f);

            Font font = UiTextFactory.Legacy;

            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(transform, false);
            titleGo.transform.localPosition = new Vector3(0f, 0.105f, -0.01f);
            MeshRenderer titleRenderer = titleGo.AddComponent<MeshRenderer>();
            TextMesh titleTm = titleGo.AddComponent<TextMesh>();
            titleTm.font = font;
            titleRenderer.sharedMaterial = font.material;
            titleTm.text = "设置";
            titleTm.fontSize = 46;
            titleTm.characterSize = 0.013f;
            titleTm.anchor = TextAnchor.MiddleCenter;
            titleTm.alignment = TextAlignment.Center;
            titleTm.color = UiTokens.Amber;

            pointsToggleText = BuildToggleRow("TogglePoints", new Vector3(0f, 0.025f, -0.01f), font);
            labelsToggleText = BuildToggleRow("ToggleLabels", new Vector3(0f, -0.075f, -0.01f), font);

            RefreshTexts();
        }

        private TextMesh BuildToggleRow(string name, Vector3 localPos, Font font)
        {
            GameObject rowGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            rowGo.name = name;
            rowGo.transform.SetParent(transform, false);
            rowGo.transform.localPosition = new Vector3(localPos.x, localPos.y, -0.01f);
            rowGo.transform.localScale = new Vector3(0.87f, 0.267f, 1f);
            MeshRenderer rowRenderer = rowGo.GetComponent<MeshRenderer>();
            Material rowMat = new Material(Shader.Find("MemoryCorridor/Sprite3D"));
            rowMat.mainTexture = RoundedRectTexture.RoundedRect(
                300, 64, 12, new Color(0.18f, 0.15f, 0.22f, 0.9f),
                new Color(0.91f, 0.64f, 0.29f, 0.5f), 2);
            rowMat.SetColor("_Color", Color.white);
            rowRenderer.material = rowMat;
            rowGo.GetComponent<Collider>().isTrigger = true; // 视觉牌可穿越，不挡射线（射线走 XRI）

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(rowGo.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            textGo.transform.localScale = new Vector3(0.36f, 0.95f, 1f);
            MeshRenderer textRenderer = textGo.AddComponent<MeshRenderer>();
            TextMesh tm = textGo.AddComponent<TextMesh>();
            tm.font = font;
            textRenderer.sharedMaterial = font.material;
            tm.fontSize = 40;
            tm.characterSize = 0.0105f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = UiTokens.Cream;

            XRSimpleInteractable interactable = rowGo.AddComponent<XRSimpleInteractable>();
            rowGo.GetComponent<Collider>().isTrigger = true; // Sprites3D shader 下 quad 可见无碰撞干扰
            TextMesh captured = tm;
            interactable.selectEntered.AddListener(delegate (SelectEnterEventArgs args)
            {
                if (name == "TogglePoints") TogglePoints();
                else ToggleLabels();
            });
            return tm;
        }

        private void TogglePoints()
        {
            showPoints = SettingsService.ToggleLightPoints();
            RefreshTexts();
        }

        private void ToggleLabels()
        {
            showLabels = SettingsService.ToggleStarLabels();
            RefreshTexts();
        }

        private void RefreshTexts()
        {
            if (pointsToggleText != null) pointsToggleText.text = "光点标记：" + (showPoints ? "开" : "关");
            if (labelsToggleText != null) labelsToggleText.text = "星点标签：" + (showLabels ? "开" : "关");
            if (pointsToggleText != null)
                pointsToggleText.color = showPoints ? UiTokens.OnColor : UiTokens.OffColor;
            if (labelsToggleText != null)
                labelsToggleText.color = showLabels ? UiTokens.OnColor : UiTokens.OffColor;
        }

        /// <summary>从持久化设置同步初始显示状态（Play 起始调用）。</summary>
        public void SyncFromDisk()
        {
            AppData data = SaveDataService.LoadAppData();
            if (data != null && data.settings != null)
            {
                showPoints = data.settings.showLightPoints;
                showLabels = data.settings.showStarLabels;
            }
            RefreshTexts();
        }
    }
}
