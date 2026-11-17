using MemoryCorridor.Media;
using UnityEngine;

namespace MemoryCorridor.Core
{
    /// <summary>
    /// 房间主题色调（R16-3 第二条主题路线）：
    /// theme 字段此前闲置，本轮真落地——RoomManager.ApplyTheme 按 theme 调环境光与雾色，
    /// 全景贴图承担主视觉（雨夜校园用程序化雨夜全景），本表承担数据断言与色调一致性。
    /// 新增主题：在 Specs 加一档 + builder 生成对应全景即可。
    /// </summary>
    public static class ThemeColors
    {
        public class ThemeSpec
        {
            public string Name;
            public Color Accent;   // 主题强调色（UI/引导）
            public Color Ambient;  // 环境光色调
            public Color Fog;      // 雾色（远景氛围）
        }

        private static readonly ThemeSpec Amber = new ThemeSpec
        {
            Name = "amber",
            Accent = UiTokens.Amber,
            Ambient = UiTokens.AmberAmbient,
            Fog = UiTokens.AmberFog,
        };

        private static readonly ThemeSpec Rain = new ThemeSpec
        {
            Name = "rain",
            Accent = UiTokens.RainBlue,
            Ambient = UiTokens.RainAmbient,
            Fog = UiTokens.RainFog,
        };

        /// <summary>按主题名取色调档（未知主题回退 amber——断言只对已知名比对）。</summary>
        public static ThemeSpec For(string theme)
        {
            if (theme == "rain") return Rain;
            return Amber;
        }
    }
}
