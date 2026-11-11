using System;
using System.IO;
using MemoryCorridor.Core;
using UnityEngine;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 记忆面板：射线点开光点后弹出，按碎片类型呈现图片/录音/日记。
    /// W2 版：素材经 MediaResolver 解析（持久化目录优先、随包 Demo 回退）；
    /// 显示时定位于视线前方 1.5m（TECH_DESIGN_V2 §5.3 防眩晕：不随头转）。
    /// W4 视觉打磨时替换为 UGUI 磨砂卡片（§4.2），对外方法签名保持不变。
    /// </summary>
    public class MemoryPanel : MonoBehaviour
    {
        [SerializeField] private Renderer imageRenderer;
        [SerializeField] private TextMesh titleText;
        [SerializeField] private TextMesh bodyText;

        private Texture2D loadedTexture;
        private Material imageMaterial;

        /// <summary>编辑器场景搭建注入引用（运行时直接使用）。</summary>
        public void Configure(Renderer rendererRef, TextMesh titleRef, TextMesh bodyRef)
        {
            imageRenderer = rendererRef;
            titleText = titleRef;
            bodyText = bodyRef;
        }

        /// <summary>按碎片内容刷新面板并显示于视线前方。</summary>
        public void Show(MemoryFragment fragment)
        {
            if (fragment == null) return;

            PositionInFrontOfGaze();

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

        /// <summary>定位于视线前方 1.5m；面板本身不跟随头部（防眩晕 §5.3）。</summary>
        private void PositionInFrontOfGaze()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.position = cam.transform.position + cam.transform.forward * 1.5f;
            }
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
                Debug.LogWarning("[MemoryPanel] 图片解码失败: " + imagePath);
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
                string resolved = MediaResolver.ResolveExisting(item.path);
                if (resolved != null) return resolved;
                Debug.LogWarning("[MemoryPanel] 素材缺失: " + MediaResolver.DescribeMiss(item.path));
            }
            return null;
        }

        private void Update()
        {
            // 仅做面向（不改变位置）；位置只在 Show 时刻固定
            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.LookAt(cam.transform);
            }
        }
    }
}
