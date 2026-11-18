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
            public float FogDensity; // R17：指数雾密度（主题间可见差异：0.02 晴 / 0.09 浓）
        }

        private static readonly ThemeSpec Amber = new ThemeSpec
        {
            Name = "amber",
            Accent = UiTokens.Amber,
            Ambient = UiTokens.AmberAmbient,
            Fog = UiTokens.AmberFog,
            FogDensity = 0.02f,
        };

        private static readonly ThemeSpec Rain = new ThemeSpec
        {
            Name = "rain",
            Accent = UiTokens.RainBlue,
            Ambient = UiTokens.RainAmbient,
            Fog = UiTokens.RainFog,
            FogDensity = 0.14f,
        };

        /// <summary>按主题名取色调档（amber=default 别名；未知主题回退 default）。</summary>
        public static ThemeSpec For(string theme)
        {
            if (theme == "rain") return Rain;
            return Amber;
        }

        /// <summary>已知主题 id 序（R17-1 运行时选择循环：default→rain→default）。</summary>
        public static readonly string[] KnownIds = { "default", "rain" };

        /// <summary>循环取下一个主题 id。</summary>
        public static string NextOf(string current)
        {
            int idx = 0;
            for (int i = 0; i < KnownIds.Length; i++) if (KnownIds[i] == current) idx = i;
            return KnownIds[(idx + 1) % KnownIds.Length];
        }
    }
}
