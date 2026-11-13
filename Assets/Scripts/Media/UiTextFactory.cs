using UnityEngine;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// TextMesh 工厂（R9 一致性收敛）：字体/字号/对齐/配色统一入口，
    /// 替代 WelcomeBoard/TutorialGuide/SettingsPanel/HoverPreviewCard 各自的重复创建逻辑。
    /// </summary>
    public static class UiTextFactory
    {
        private static Font _legacy;

        /// <summary>内置 LegacyRuntime 字体单例（坑-013 已验证本版本资源名）。</summary>
        public static Font Legacy => _legacy != null
            ? _legacy
            : _legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>创建 TextMesh 及其渲染器（返回文字组件；渲染器已绑字体材质）。</summary>
        public static TextMesh CreateTextMesh(Transform parent, string objectName, Vector3 localPos,
            int fontSize, float characterSize, TextAnchor anchor, TextAlignment alignment,
            Color color, string content = "")
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.font = Legacy;
            renderer.sharedMaterial = Legacy.material;
            tm.fontSize = fontSize;
            tm.characterSize = characterSize;
            tm.anchor = anchor;
            tm.alignment = alignment;
            tm.color = color;
            tm.text = content;
            return tm;
        }
    }
}
