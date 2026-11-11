using System;
using System.IO;
using MemoryCorridor.Core;
using UnityEngine;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 记忆面板：射线点开光点后弹出，按碎片类型呈现图片/录音/日记。
    /// W1 冒烟版：WorldSpace Quad 显示图片 + TextMesh 显示标题与日记；
    /// W4 视觉打磨时替换为 UGUI 磨砂卡片（TECH_DESIGN_V2 §4.2），对外方法签名保持不变。
    /// </summary>
    public class MemoryPanel : MonoBehaviour
    {
        [SerializeField] private Renderer imageRenderer;
        [SerializeField] private TextMesh titleText;
        [SerializeField] private TextMesh bodyText;

        private Texture2D loadedTexture;
        private Material imageMaterial;

        /// <summary>按碎片内容刷新面板并显示。</summary>
        public void Show(MemoryFragment fragment)
        {
            if (fragment == null) return;

            if (titleText != null)
            {
                titleText.text = string.IsNullOrEmpty(fragment.title) ? "未命名记忆" : fragment.title;
            }

            ShowDiary(fragment);
            ShowFirstImage(fragment);
            gameObject.SetActive(true);
        }

        /// <summary>隐藏面板。</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void ShowDiary(MemoryFragment fragment)
        {
            if (bodyText == null) return;
            if (fragment.diary != null && !string.IsNullOrEmpty(fragment.diary.text))
            {
                bodyText.text = fragment.diary.date + "\n" + fragment.diary.text;
            }
            else
            {
                bodyText.text = "";
            }
        }

        private void ShowFirstImage(MemoryFragment fragment)
        {
            if (imageRenderer == null) return;

            string imagePath = FindFirstImagePath(fragment);
            if (imagePath == null)
            {
                imageRenderer.enabled = false;
                return;
            }

            byte[] bytes = File.ReadAllBytes(imagePath);
            if (loadedTexture == null)
            {
                loadedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            }
            if (!loadedTexture.LoadImage(bytes))
            {
                Debug.LogWarning($"[MemoryPanel] 图片解码失败: {imagePath}");
                imageRenderer.enabled = false;
                return;
            }

            if (imageMaterial == null)
            {
                imageMaterial = imageRenderer.material;
            }
            imageMaterial.mainTexture = loadedTexture;
            imageRenderer.enabled = true;
        }

        private static string FindFirstImagePath(MemoryFragment fragment)
        {
            if (fragment.mediaItems == null) return null;
            for (int i = 0; i < fragment.mediaItems.Count; i++)
            {
                MediaItem item = fragment.mediaItems[i];
                if (item.kind != "image" || string.IsNullOrEmpty(item.path)) continue;
                string fullPath = Path.Combine(Application.persistentDataPath, item.path);
                if (File.Exists(fullPath)) return fullPath;
            }
            return null;
        }

        private void Update()
        {
            // W1 冒烟：面向主相机；真机阶段改为"固定于触发时视线前方 1.5m"（防眩晕清单 §5.3）
            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.LookAt(cam.transform);
            }
        }
    }
}
