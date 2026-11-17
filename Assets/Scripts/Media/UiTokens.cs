using UnityEngine;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// UI 设计令牌单一来源（R9 一致性收敛，UI_DESIGN.md §1）。
    /// 之前 Amber/Ink 等色值在 5 个文件重复定义——任何调色改这里一处即全局生效。
    /// </summary>
    public static class UiTokens
    {
        // —— 色彩 ——
        public static readonly Color Amber = new Color(0.91f, 0.64f, 0.29f, 1f);        // 主强调 #E8A24B
        public static readonly Color Cyan = new Color(0.35f, 0.78f, 0.88f, 1f);         // 录音类/悬停态
        public static readonly Color Cream = new Color(0.92f, 0.88f, 0.82f, 1f);        // 日记类/正文墨色
        public static readonly Color WarmOrange = new Color(0.95f, 0.55f, 0.35f, 1f);   // 混合类
        public static readonly Color WarmRed = new Color(0.78f, 0.42f, 0.42f, 1f);      // 私密
        public static readonly Color PanelBg = new Color(0.12f, 0.10f, 0.15f, 0.88f);   // 面板底
        public static readonly Color CardBg = new Color(0.10f, 0.08f, 0.13f, 0.94f);    // 卡片/告示牌底
        public static readonly Color HudBg = new Color(0.10f, 0.08f, 0.13f, 0.86f);     // HUD 引导条底
        public static readonly Color RowBg = new Color(0.18f, 0.15f, 0.22f, 0.9f);      // 开关行底
        public static readonly Color LineSoft = new Color(0.91f, 0.64f, 0.29f, 0.55f);  // 柔描边
        public static readonly Color OnColor = new Color(0.55f, 0.85f, 0.55f, 1f);      // 开关"开"
        public static readonly Color OffColor = new Color(0.75f, 0.5f, 0.5f, 1f);       // 开关"关"
        public static readonly Color DustAmber = new Color(0.91f, 0.72f, 0.45f, 0.85f); // 星尘粒子
        public static readonly Color DustAmberDim = new Color(0.91f, 0.72f, 0.45f, 0.55f); // 星尘粒子（半透明）
        public static readonly Color FrameBg = new Color(0.16f, 0.13f, 0.19f, 1f);      // 照片框底
        public static readonly Color ThumbBg = new Color(0.2f, 0.18f, 0.22f, 1f);       // 缩略占位
        public static readonly Color CloseBg = new Color(0.5f, 0.3f, 0.3f, 1f);         // ✕ 底
        public static readonly Color CrossWhite = new Color(0.95f, 0.92f, 0.88f, 1f);   // ✕ 白
        public static readonly Color LabelBg = new Color(0.10f, 0.08f, 0.13f, 0.9f);    // 星点标签底
        public static readonly Color LabelInk = new Color(0.92f, 0.88f, 0.82f, 1f);     // 星点标签墨

        // —— 尺寸/间距（像素，面板 620×460 坐标系）——
        public const float CornerRadius = 24f;    // 面板圆角
        public const float CardRadius = 16f;      // 卡片圆角
        public const float BadgeRadius = 12f;     // 徽章圆角
        public const float BorderPx = 2f;         // 描边宽
        public const float RowHeightPx = 64f;     // 开关行高

        /// <summary>面板标题字号（620 宽面板）。</summary>
        public const float PanelTitleSize = 42f;
        /// <summary>面板正文字号。</summary>
        public const float PanelBodySize = 28f;
    }
}
