using System.Collections.Generic;
using MemoryCorridor.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 设置面板（R5 建，R11 扩）：5 行开关——面板距离档位 / 舒适暗角档位 / 移动方式 /
    /// 光点标记显隐 / 星点标签显隐。点击翻转或循环档位，全部持久化 + 场景应用。
    /// 单焦点联动：记忆面板显示时本面板淡化（SetDimmed），点本面板任意行时记忆面板收起。
    /// 位置：出生点右前方，与告示牌并列；圆角底板统一 tokens。
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        private TextMesh pointsToggleText;
        private TextMesh labelsToggleText;
        private TextMesh distanceToggleText;
        private TextMesh vignetteToggleText;
        private TextMesh locomotionToggleText;
        private TextMesh themeToggleText; // R17-1：场景主题选择行
        private TextMesh timelineReplayText; // R19 P2：时间轴回放启停行
        private TextMesh narrationVolumeText; // R20：讲解音量档位行
        private TextMesh volumeToggleText;
        private bool showPoints = true;
        private bool showLabels = true;
        private bool dimmed;
        private CanvasGroup canvasGroup;
        private readonly List<UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable> interactables =
            new List<UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable>();
        private Vector3 basePosition;
        private float bobPhase; // R6：轻微浮动显眼化（入口可达性）
        private MemoryPanel memoryPanel; // 单焦点联动（R11 c）

        public string PointsToggleText => pointsToggleText != null ? pointsToggleText.text : null;
        public string LabelsToggleText => labelsToggleText != null ? labelsToggleText.text : null;
        public string DistanceToggleText => distanceToggleText != null ? distanceToggleText.text : null;
        public string VolumeToggleText => volumeToggleText != null ? volumeToggleText.text : null;
        public bool IsDimmed => dimmed;
        public string ThemeRowText => themeToggleText != null ? themeToggleText.text : null; // R17 探针断言

        private static readonly Color OnColor = UiTokens.OnColor;
        private static readonly Color OffColor = UiTokens.OffColor;

        private void Awake()
        {
            EnsureBuilt();
        }

        /// <summary>builder 注入记忆面板引用（单焦点联动）。</summary>
        public void Configure(MemoryPanel panelRef)
        {
            memoryPanel = panelRef;
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

        private void TogglePoints()
        {
            showPoints = SettingsService.ToggleLightPoints();
            if (memoryPanel != null) memoryPanel.Hide(); // R11 c 单焦点
            RefreshTexts();
        }

        private void ToggleLabels()
        {
            showLabels = SettingsService.ToggleStarLabels();
            RefreshTexts();
        }

        private void CycleDistance()
        {
            float current = SettingsService.GetPanelDistance();
            float[] steps = { 1.5f, 2.0f, 2.4f, 3.0f };
            float next = steps[0];
            for (int i = 0; i < steps.Length; i++)
            {
                if (Mathf.Abs(current - steps[i]) < 0.01f)
                {
                    next = steps[(i + 1) % steps.Length];
                    break;
                }
            }
            SettingsService.SetPanelDistance(next);
            RefreshTexts();
        }

        private void CycleVolume()
        {
            int cur = SettingsService.GetVolumePercent();
            int next = (cur + 25) % 125; // 0→25→50→75→100→0
            SettingsService.SetVolumePercent(next);
            RefreshTexts();
        }

        private void CycleVignette()
        {
            SettingsService.CycleVignette();
            RefreshTexts();
        }

        private void CycleLocomotion()
        {
            string current = SettingsService.GetLocomotion();
            SettingsService.SetLocomotion(current == "teleport" ? "smooth" : "teleport");
            RefreshTexts();
        }

        /// <summary>R20：讲解音量档位循环（0→50→100→0），即时生效（讲解源 Update 跟随 NarrationVolume）。</summary>
        private void CycleNarrationVolume()
        {
            int cur = AudioSettingsService.GetNarrationVolumePercent();
            int next = cur == 0 ? 50 : (cur == 50 ? 100 : 0);
            AudioSettingsService.SetNarrationVolumePercent(next);
            RefreshTexts();
        }

        /// <summary>R19 P2：时间轴回放启停（点击切换，运行态由 TimelineReplayController.IsReplaying 决定文案）。</summary>
        private void CycleTimelineReplay()
        {
            Media.TimelineReplayController tc = FindTimelineReplay();
            if (tc == null) return;
            if (tc.IsReplaying) tc.StopReplay();
            else tc.StartReplay();
            RefreshTexts();
        }

        private static Media.TimelineReplayController FindTimelineReplay()
        {
            var found = FindObjectsByType<Media.TimelineReplayController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            return found.Length > 0 ? found[0] : null;
        }

        /// <summary>R17-1：场景主题循环切换（default→rain→default，0.3s 淡入即时生效）。</summary>
        private void CycleThemeRow()
        {
            RoomManager rm = FindRoomManager();
            if (rm != null) rm.CycleTheme(true);
            RefreshTexts();
        }

        private static RoomManager FindRoomManager()
        {
            RoomManager[] found = FindObjectsByType<RoomManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return found.Length > 0 ? found[0] : null;
        }

        private void RefreshTexts()
        {
            if (volumeToggleText != null) volumeToggleText.text = "主音量：" + SettingsService.GetVolumePercent() + "%";
            if (pointsToggleText != null) pointsToggleText.text = "光点标记：" + (showPoints ? "开" : "关");
            if (labelsToggleText != null) labelsToggleText.text = "星点标签：" + (showLabels ? "开" : "关");
            if (distanceToggleText != null)
                distanceToggleText.text = "面板距离：" + SettingsService.GetPanelDistance().ToString("0.0") + " 米";
            if (vignetteToggleText != null)
                vignetteToggleText.text = "舒适暗角：" + FormatVignette();
            if (locomotionToggleText != null)
                locomotionToggleText.text = "移动方式：" + (SettingsService.GetLocomotion() == "smooth" ? "平滑" : "传送");
            if (themeToggleText != null) themeToggleText.text = "场景主题：" + ThemeDisplayName();
            if (timelineReplayText != null)
            {
                Media.TimelineReplayController tc = FindTimelineReplay();
                timelineReplayText.text = "时间轴回放：" + (tc != null && tc.IsReplaying ? "回放中" : "关");
            }
            if (narrationVolumeText != null)
                narrationVolumeText.text = "讲解音量：" + AudioSettingsService.GetNarrationVolumePercent() + "%";

            if (pointsToggleText != null) pointsToggleText.color = showPoints ? OnColor : OffColor;
            if (labelsToggleText != null) labelsToggleText.color = showLabels ? OnColor : OffColor;
        }

        /// <summary>当前主题显示名（读 RoomManager 运行时选择，缺省回退持久化值）。</summary>
        private string ThemeDisplayName()
        {
            RoomManager rm = FindRoomManager();
            string id = rm != null ? rm.ActiveThemeId : SaveDataService.LoadAppData().settings.activeThemeId;
            return id == "rain" ? "雨夜校园" : "默认";
        }

        private string FormatVignette()
        {
            AppData data = SaveDataService.LoadAppData();
            if (data == null || data.settings == null || !data.settings.vignetteEnabled) return "关";
            return data.settings.vignetteIntensity >= 0.35f ? "0.4 强" : "0.2 弱";
        }

        /// <summary>R11 c 单焦点：记忆面板出现时淡化本面板（降低交互干扰）。</summary>
        public void SetDimmed(bool dim)
        {
            dimmed = dim;
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = dim ? 0.35f : 1f;
            for (int i = 0; i < interactables.Count; i++)
            {
                if (interactables[i] != null) interactables[i].enabled = !dim;
            }
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

        private void EnsureBuilt()
        {
            if (pointsToggleText != null) return;

            MeshRenderer boardRenderer = GetComponent<MeshRenderer>();
            if (boardRenderer == null) boardRenderer = gameObject.AddComponent<MeshRenderer>();
            Shader spriteShader = Shader.Find("MemoryCorridor/Sprite3D");
            Material boardMat = new Material(spriteShader);
            boardMat.mainTexture = RoundedRectTexture.RoundedRect(
                460, 1080, 18, UiTokens.CardBg, UiTokens.LineSoft, 2);
            boardMat.SetColor("_Color", Color.white);
            boardRenderer.material = boardMat;
            transform.localScale = new Vector3(0.46f, 1.08f, 1f);

            Font font = UiTextFactory.Legacy;

            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(transform, false);
            titleGo.transform.localPosition = new Vector3(0f, 380f, -0.01f);
            MeshRenderer titleRenderer = titleGo.AddComponent<MeshRenderer>();
            TextMesh titleTm = titleGo.AddComponent<TextMesh>();
            titleTm.font = font;
            titleRenderer.sharedMaterial = font.material;
            titleTm.text = "设置";
            titleTm.fontSize = UiTokens.SettingsTitleFontSize;
            titleTm.characterSize = UiTokens.SettingsTitleCharSize;
            titleTm.anchor = TextAnchor.MiddleCenter;
            titleTm.alignment = TextAlignment.Center;
            titleTm.color = UiTokens.Amber;

            volumeToggleText = BuildToggleRow("RowVolume", 300f, font, "主音量：100%", CycleVolume);
            pointsToggleText = BuildToggleRow("TogglePoints", 200f, font, "光点标记：开", TogglePoints);
            labelsToggleText = BuildToggleRow("ToggleLabels", 100f, font, "星点标签：开", ToggleLabels);
            distanceToggleText = BuildToggleRow("RowDistance", 0f, font, "面板距离：2.4 米", CycleDistance);
            vignetteToggleText = BuildToggleRow("RowVignette", -100f, font, "舒适暗角：关", CycleVignette);
            locomotionToggleText = BuildToggleRow("RowLocomotion", -200f, font, "移动方式：传送", CycleLocomotion);
            themeToggleText = BuildToggleRow("RowTheme", -300f, font, "场景主题：默认", CycleThemeRow);
            timelineReplayText = BuildToggleRow("RowTimelineReplay", -400f, font, "时间轴回放：关", CycleTimelineReplay);
            narrationVolumeText = BuildToggleRow("RowNarrationVolume", -500f, font, "讲解音量：100%", CycleNarrationVolume);

            RefreshTexts();
        }

        private TextMesh BuildToggleRow(string rowName, float yPos, Font font,
            string initialText, System.Action onTapped)
        {
            GameObject rowGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            rowGo.name = rowName;
            rowGo.transform.SetParent(transform, false);
            rowGo.transform.localPosition = new Vector3(0f, yPos, -0.01f);
            rowGo.transform.localScale = new Vector3(0.87f, 0.155f, 1f);
            // 坑-043：Quad 自带 MeshCollider（凹面）做 trigger/XRI 会触发 PhysX abort——一律凸面 BoxCollider
            MeshCollider rowMeshCollider = rowGo.GetComponent<MeshCollider>();
            if (rowMeshCollider != null) Destroy(rowMeshCollider);
            BoxCollider rowBox = rowGo.AddComponent<BoxCollider>();
            rowBox.isTrigger = true;
            MeshRenderer rowRenderer = rowGo.GetComponent<MeshRenderer>();
            Material rowMat = new Material(Shader.Find("MemoryCorridor/Sprite3D"));
            rowMat.mainTexture = RoundedRectTexture.RoundedRect(
                300, 64, 12, UiTokens.RowBg, UiTokens.LineSoft, 2);
            rowMat.SetColor("_Color", Color.white);
            rowRenderer.material = rowMat;

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(rowGo.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            textGo.transform.localScale = new Vector3(0.36f, 0.95f, 1f);
            MeshRenderer textRenderer = textGo.AddComponent<MeshRenderer>();
            TextMesh tm = textGo.AddComponent<TextMesh>();
            tm.font = font;
            textRenderer.sharedMaterial = font.material;
            tm.fontSize = UiTokens.RowFontSize;
            tm.characterSize = UiTokens.RowCharSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = UiTokens.Cream;
            tm.text = initialText;

            XRSimpleInteractable interactable = rowGo.AddComponent<XRSimpleInteractable>();
            TextMesh captured = tm;
            System.Action tapped = onTapped;
            interactable.selectEntered.AddListener(delegate (SelectEnterEventArgs args)
            {
                tapped();
                RefreshTexts();
            });
            interactables.Add(interactable);
            return tm;
        }

        private void OnDestroy()
        {
            interactables.Clear();
        }
    }
}
