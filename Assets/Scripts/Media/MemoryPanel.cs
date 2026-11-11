using System.IO;
using MemoryCorridor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 记忆面板（W4 UGUI 磨砂版）：WorldSpace Canvas——暮色半透明底 + 琥珀描边 +
    /// 照片 RawImage + TMP 标题/正文（动态字体资产，CJK 按需填充）。
    /// 显示时定位于视线前方 1.5m 固定不随头（§5.3 防眩晕）；Show/Hide 契约与桥接器不变。
    /// </summary>
    public class MemoryPanel : MonoBehaviour
    {
        private Canvas canvas;
        private RawImage photoImage;
        private TMP_Text titleText;
        private TMP_Text bodyText;
        private Texture2D loadedTexture;
        private TMP_FontAsset fontAsset;

        public bool IsShowing => gameObject.activeSelf;
        public string TitleString => titleText != null ? titleText.text : null;
        public Texture PhotoTexture => photoImage != null ? photoImage.texture : null;

        private static readonly Color Amber = new Color(0.91f, 0.64f, 0.29f, 1f);
        private static readonly Color PanelBg = new Color(0.12f, 0.10f, 0.15f, 0.88f);
        private static readonly Color BodyInk = new Color(0.92f, 0.88f, 0.82f, 1f);

        private void Awake()
        {
            EnsureBuilt();
        }

        /// <summary>按碎片内容刷新面板并显示于视线前方。</summary>
        public void Show(MemoryFragment fragment)
        {
            if (fragment == null) return;
            EnsureBuilt();
            PositionInFrontOfGaze();

            if (titleText != null)
            {
                titleText.text = string.IsNullOrEmpty(fragment.title) ? "未命名记忆" : fragment.title;
            }
            if (bodyText != null)
            {
                bodyText.text = (fragment.diary != null && !string.IsNullOrEmpty(fragment.diary.text))
                    ? fragment.diary.date + "\n" + fragment.diary.text
                    : "";
            }
            ShowFirstImage(fragment);
            gameObject.SetActive(true);
        }

        /// <summary>隐藏面板。</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void EnsureBuilt()
        {
            if (canvas != null) return;

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform root = canvas.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(620f, 460f);
            transform.localScale = Vector3.one * 0.0016f; // ≈1.0m × 0.74m

            Image background = gameObject.AddComponent<Image>();
            background.color = PanelBg;
            background.raycastTarget = false;
            Outline outline = gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Amber.r, Amber.g, Amber.b, 0.6f);
            outline.effectDistance = new Vector2(2f, -2f); // 1px 级描边（磨砂卡片 §4.2）

            GameObject photoGo = new GameObject("Photo");
            photoGo.transform.SetParent(transform, false);
            photoImage = photoGo.AddComponent<RawImage>();
            photoImage.raycastTarget = false;
            Stretch(photoGo.GetComponent<RectTransform>(), 0.05f, 0.44f, 0.95f, 0.94f);

            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(transform, false);
            titleText = titleGo.AddComponent<TextMeshProUGUI>();
            EnsureFont(titleText);
            titleText.fontSize = 42f;
            titleText.color = Amber;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.raycastTarget = false;
            Stretch(titleGo.GetComponent<RectTransform>(), 0.03f, 0.30f, 0.97f, 0.42f);

            GameObject bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            bodyText = bodyGo.AddComponent<TextMeshProUGUI>();
            EnsureFont(bodyText);
            bodyText.fontSize = 28f;
            bodyText.color = BodyInk;
            bodyText.alignment = TextAlignmentOptions.Top;
            bodyText.enableWordWrapping = true; // TMP 3.0.9：无 textWrappingMode（坑-029）
            bodyText.raycastTarget = false;
            Stretch(bodyGo.GetComponent<RectTransform>(), 0.05f, 0.04f, 0.95f, 0.28f);
        }

        private void EnsureFont(TMP_Text text)
        {
            if (fontAsset == null)
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(
                    Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            }
            text.font = fontAsset;
        }

        private static void Stretch(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void PositionInFrontOfGaze()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.position = cam.transform.position + cam.transform.forward * 1.5f;
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            }
        }

        private void ShowFirstImage(MemoryFragment fragment)
        {
            if (photoImage == null) return;

            string imagePath = FindFirstImagePath(fragment);
            if (imagePath == null)
            {
                photoImage.texture = null;
                photoImage.enabled = false;
                return;
            }

            byte[] bytes = File.ReadAllBytes(imagePath);
            if (loadedTexture == null)
            {
                loadedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            }
            if (!loadedTexture.LoadImage(bytes))
            {
                Debug.LogWarning("[MemoryPanel] 图片解码失败: " + imagePath);
                photoImage.texture = null;
                photoImage.enabled = false;
                return;
            }

            photoImage.texture = loadedTexture;
            photoImage.enabled = true;
        }

        private static string FindFirstImagePath(MemoryFragment fragment)
        {
            if (fragment.mediaItems == null) return null;
            for (int i = 0; i < fragment.mediaItems.Count; i++)
            {
                MediaItem item = fragment.mediaItems[i];
                if (item.kind != "image" || string.IsNullOrEmpty(item.path)) continue;
                string resolved = MediaResolver.ResolveExisting(item.path);
                if (resolved != null) return resolved;
                Debug.LogWarning("[MemoryPanel] 素材缺失: " + MediaResolver.DescribeMiss(item.path));
            }
            return null;
        }
    }
}
