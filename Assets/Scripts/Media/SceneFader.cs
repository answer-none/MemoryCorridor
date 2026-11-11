using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 转场淡入淡出（TECH_DESIGN_V2 §5.3 防眩晕"零硬切"）：
    /// ScreenSpaceOverlay 黑幕 alpha 驱动；房间切换/回忆重演转场共用。
    /// 坑-018：按真实时间推进；W4 换成带星尘粒子的磨砂黑幕。
    /// </summary>
    public class SceneFader : MonoBehaviour
    {
        private Image overlay;

        /// <summary>当前黑幕 alpha（0=透明，1=全黑）。</summary>
        public float Alpha => overlay != null ? overlay.color.a : 0f;

        private void Awake()
        {
            EnsureOverlay();
        }

        private void EnsureOverlay()
        {
            if (overlay != null) return;

            GameObject canvasGo = new GameObject("FadeCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            overlay = canvasGo.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0f);
            overlay.raycastTarget = false;
            overlay.rectTransform.anchorMin = Vector2.zero;
            overlay.rectTransform.anchorMax = Vector2.one;
            overlay.rectTransform.offsetMin = Vector2.zero;
            overlay.rectTransform.offsetMax = Vector2.zero;
        }

        public IEnumerator FadeOut(float duration)
        {
            yield return Fade(1f, duration);
        }

        public IEnumerator FadeIn(float duration)
        {
            yield return Fade(0f, duration);
        }

        private IEnumerator Fade(float target, float duration)
        {
            EnsureOverlay();
            float start = overlay.color.a;
            float startTime = Time.realtimeSinceStartup;
            while (true)
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - startTime) / Mathf.Max(0.01f, duration));
                Color c = overlay.color;
                c.a = Mathf.Lerp(start, target, t);
                overlay.color = c;
                if (t >= 1f) break;
                yield return null;
            }
        }
    }
}
