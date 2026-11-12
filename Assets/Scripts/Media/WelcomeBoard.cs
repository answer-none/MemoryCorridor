using UnityEngine;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 欢迎告示牌（R2，TEAM_PLAN E1）：入口处的三句引导（走近光点/扳机选择/沿星尘走读）。
    /// 评委/内测用户的前 30 秒引导；用户选中第一条记忆后由桥接器调 NotifyUsed() 收起。
    /// 3D 轻量实现：Quad 底板 + TextMesh，billboard 朝向相机。
    /// </summary>
    public class WelcomeBoard : MonoBehaviour
    {
        private GameObject rootObject;
        private TextMesh titleText;
        private TextMesh line1Text;
        private TextMesh line2Text;
        private TextMesh line3Text;
        private bool used;

        public bool IsShowing => rootObject != null && rootObject.activeSelf;

        private static readonly Color Amber = new Color(0.91f, 0.64f, 0.29f, 1f);
        private static readonly Color Ink = new Color(0.92f, 0.88f, 0.82f, 1f);

        private void Awake()
        {
            EnsureBuilt();
        }

        private void Start()
        {
            EnsureBuilt();
            rootObject.SetActive(true); // 每次启动都引导（内测/评委场景合理）
        }

        /// <summary>用户选中第一条记忆后收起（桥接器调用）。</summary>
        public void NotifyUsed()
        {
            used = true;
            if (rootObject != null) rootObject.SetActive(false);
        }

        private void EnsureBuilt()
        {
            if (rootObject != null) return;
            rootObject = gameObject;

            MeshRenderer boardRenderer = GetComponent<MeshRenderer>();
            if (boardRenderer == null) boardRenderer = gameObject.AddComponent<MeshRenderer>();
            Material boardMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            boardMat.SetColor("_BaseColor", new Color(0.10f, 0.08f, 0.13f, 1f));
            boardRenderer.material = boardMat;
            transform.localScale = new Vector3(0.9f, 0.34f, 1f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText = CreateText("Title", new Vector3(0f, 0.055f, -0.01f), font, 52, 0.014f, Amber,
                "回响长廊 · 记忆策展");
            line1Text = CreateText("Line1", new Vector3(0f, 0.005f, -0.01f), font, 34, 0.0105f, Ink,
                "走近发光的光点，它会先向你传来声音");
            line2Text = CreateText("Line2", new Vector3(0f, -0.045f, -0.01f), font, 34, 0.0105f, Ink,
                "扣下扳机，回到那一刻");
            line3Text = CreateText("Line3", new Vector3(0f, -0.095f, -0.01f), font, 34, 0.0105f, Ink,
                "跟随星尘走完一条记忆路线");
        }

        private TextMesh CreateText(string name, Vector3 localPos, Font font,
            int fontSize, float charSize, Color color, string content)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.font = font;
            renderer.sharedMaterial = font.material;
            tm.fontSize = fontSize;
            tm.characterSize = charSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.text = content;
            return tm;
        }

        private void Update()
        {
            if (rootObject == null || !rootObject.activeSelf || used) return;
            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.LookAt(cam.transform);
            }
        }
    }
}
