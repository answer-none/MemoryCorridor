using UnityEngine;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 程序化 UI 贴图（R3 美学升级，零外部素材）：
    /// RoundedRect 抗锯齿圆角矩形（UGUI Image.sprite 与 3D Quad 底板共用），
    /// RadialGlow 径向光晕（光点外圈）。像素直填，一次生成终身复用。
    /// </summary>
    public static class RoundedRectTexture
    {
        /// <summary>生成圆角矩形贴图（可选 1px 抗锯齿边）。角外像素 alpha=0。</summary>
        public static Texture2D RoundedRect(int width, int height, int radius, Color fill, Color borderColor, int borderPx)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[width * height];
            float rx = radius - 0.5f;
            float ry = radius - 0.5f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float alpha = CornerAlpha(x, y, width, height, rx, ry);
                    Color c = fill;
                    c.a *= alpha;
                    // 内描边：距边/圆角边界 borderPx 内混合边色
                    if (borderPx > 0 && alpha > 0.01f)
                    {
                        float innerAlpha = CornerAlpha(x, y, width, height, rx, ry) > 0f
                            ? EdgeDistanceAlpha(x, y, width, height, rx, ry, borderPx)
                            : 0f;
                        if (innerAlpha < 1f)
                        {
                            c = Color.Lerp(borderColor, fill, Mathf.Clamp01(innerAlpha));
                            c.a = alpha;
                        }
                    }
                    pixels[y * width + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>径向光晕：中心 alpha 高、向外平滑衰减至 0（加法混合用）。</summary>
        public static Texture2D RadialGlow(int size, Color core, float falloff)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half + 0.5f) / half;
                    float dy = (y - half + 0.5f) / half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - dist);
                    a = Mathf.Pow(a, falloff);
                    Color c = core;
                    c.a *= a;
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>圆角内=1，圆角外=0，角弧上做 1px 抗锯齿。</summary>
        private static float CornerAlpha(int x, int y, int w, int h, float rx, float ry)
        {
            bool inTopLeft = x < rx && y >= h - ry;
            bool inTopRight = x >= w - rx && y >= h - ry;
            bool inBottomLeft = x < rx && y < ry;
            bool inBottomRight = x >= w - rx && y < ry;

            if (!(inTopLeft || inTopRight || inBottomLeft || inBottomRight))
            {
                return 1f; // 非角区：矩形内
            }

            float cx = inTopLeft || inBottomLeft ? rx : w - rx;
            float cy = inTopLeft || inTopRight ? h - ry : ry;
            float dx = x - cx;
            float dy = y - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(rx - dist + 0.5f); // 1px 抗锯齿
        }

        /// <summary>距边界 <px 内返回 0→1 渐变（描边混合用）。</summary>
        private static float EdgeDistanceAlpha(int x, int y, int w, int h, float rx, float ry, float px)
        {
            float edgeDist = Mathf.Min(
                Mathf.Min(x, w - 1 - x),
                Mathf.Min(y, h - 1 - y));
            // 圆角处粗略用角距离修正
            bool inCorner = (x < rx || x >= w - rx) && (y < ry || y >= h - ry);
            if (inCorner)
            {
                float cx = x < rx ? rx : w - rx;
                float cy = y < ry ? ry : h - ry;
                float dx = x - cx;
                float dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                edgeDist = Mathf.Min(edgeDist, rx - dist);
            }
            return Mathf.Clamp01(edgeDist / Mathf.Max(1f, px));
        }
    }
}
