using System.IO;
using MemoryCorridor.Core;
using UnityEngine;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 悬停预览小卡（R1，TEAM_PLAN E2）：射线悬停光点 0.3s 后浮现——
    /// 缩略图 + 标题 + 日期；由 XRIAnchorBridge 的 OnHoverPreview 驱动，hoverExited 隐藏。
    /// 3D 轻量实现（Quad 底板 + 子 Quad 图 + TextMesh），Billboard 朝向相机。
    /// </summary>
    public class HoverPreviewCard : MonoBehaviour
    {
        private GameObject rootObject;
        private MeshRenderer thumbRenderer;
        private TextMesh titleText;
        private TextMesh dateText;
        private Texture2D loadedTexture;

        public bool IsShowing => rootObject != null && rootObject.activeSelf;
        public string TitleString => titleText != null ? titleText.text : null;

        private void Awake()
        {
            EnsureBuilt();
        }

        /// <summary>在光点上方显示预览卡。</summary>
        public void Show(MemoryFragment fragment, Vector3 anchorPosition)
        {
            if (fragment == null) return;
            EnsureBuilt();

            rootObject.transform.position = anchorPosition + Vector3.up * 0.35f;
            if (titleText != null)
            {
                titleText.text = string.IsNullOrEmpty(fragment.title) ? "未命名记忆" : fragment.title;
            }
            string date = fragment.diary != null && !string.IsNullOrEmpty(fragment.diary.date)
                ? fragment.diary.date
                : (fragment.createdAt != null && fragment.createdAt.Length >= 10
                    ? fragment.createdAt.Substring(0, 10)
                    : "");
            if (dateText != null) dateText.text = date;
            ShowThumb(fragment);

            rootObject.SetActive(true);
        }

        /// <summary>隐藏预览卡。</summary>
        public void Hide()
        {
            if (rootObject != null) rootObject.SetActive(false);
        }

        private void EnsureBuilt()
        {
            if (rootObject != null) return;

            rootObject = gameObject;
            MeshRenderer boardRenderer = GetComponent<MeshRenderer>();
            if (boardRenderer == null) boardRenderer = gameObject.AddComponent<MeshRenderer>();
            Material boardMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            boardMat.SetColor("_BaseColor", new Color(0.12f, 0.10f, 0.15f, 1f));
            boardRenderer.material = boardMat;
            transform.localScale = new Vector3(0.36f, 0.16f, 1f);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");

            GameObject thumbGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            thumbGo.name = "Thumb";
            Collider thumbCollider = thumbGo.GetComponent<Collider>();
            if (thumbCollider != null) Destroy(thumbCollider);
            thumbGo.transform.SetParent(transform, false);
            thumbGo.transform.localPosition = new Vector3(-0.12f, 0f, -0.01f);
            thumbGo.transform.localScale = new Vector3(0.42f, 0.62f, 1f);
            thumbRenderer = thumbGo.GetComponent<MeshRenderer>();
            Material thumbMat = new Material(lit);
            thumbMat.SetColor("_BaseColor", new Color(0.2f, 0.18f, 0.22f, 1f));
            thumbRenderer.material = thumbMat;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText = CreateText("TitleText", new Vector3(0.02f, 0.038f, -0.01f), font,
                44, 0.012f, new Color(0.91f, 0.64f, 0.29f));
            dateText = CreateText("DateText", new Vector3(0.02f, -0.042f, -0.01f), font,
                32, 0.010f, new Color(0.92f, 0.88f, 0.82f));

            rootObject.SetActive(false);
        }

        private TextMesh CreateText(string name, Vector3 localPos, Font font,
            int fontSize, float charSize, Color color)
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
            tm.alignment = TextAlignment.Left;
            tm.color = color;
            return tm;
        }

        private void ShowThumb(MemoryFragment fragment)
        {
            if (thumbRenderer == null) return;
            if (fragment.mediaItems == null)
            {
                thumbRenderer.material.mainTexture = null;
                return;
            }
            for (int i = 0; i < fragment.mediaItems.Count; i++)
            {
                MediaItem item = fragment.mediaItems[i];
                if (item.kind != "image" || string.IsNullOrEmpty(item.path)) continue;
                string resolved = MediaResolver.ResolveExisting(item.path);
                if (resolved == null) continue;

                if (loadedTexture == null)
                {
                    loadedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                }
                if (loadedTexture.LoadImage(File.ReadAllBytes(resolved)))
                {
                    thumbRenderer.material.mainTexture = loadedTexture;
                    return;
                }
            }
            thumbRenderer.material.mainTexture = null; // 无图碎片：暗色占位
        }

        private void Update()
        {
            if (rootObject == null || !rootObject.activeSelf) return;
            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.LookAt(cam.transform);
            }
        }
    }
}
