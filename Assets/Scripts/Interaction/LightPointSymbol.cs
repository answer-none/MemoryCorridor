using UnityEngine;

namespace MemoryCorridor.Interaction
{
    /// <summary>
    /// 记忆点符号浮标（R15 对标学习 ①，MoL image 要素——对象-位置显式绑定）：
    /// 光点上方一块 billboard 小牌，程序化贴图按记忆类型区分——
    /// image=山形照片框 / audio=声波弧 / diary=书页 / mixed=菱星 / private=锁形。
    /// 防混淆设计：五种形状互不相似（色形双编码，色沿用 R1 四色），单房间相邻点一眼可辨。
    /// billboard：缓存主相机每帧朝向（1s 刷新相机引用，无 FindObjects 开销）。
    /// 显隐随 LightPointAnchor.SetMarkerVisible 联动（R5 光点显隐设置）。
    /// </summary>
    public class LightPointSymbol : MonoBehaviour
    {
        public enum Kind { Image = 0, Audio = 1, Diary = 2, Mixed = 3, Private = 4 }

        [SerializeField] private Renderer symbolRenderer;
        [SerializeField] private float camRefresh = 1f; // 主相机引用刷新间隔

        private Camera mainCam;
        private float camTimer;
        private Kind kind = Kind.Image;
        private bool visibleState = true;

        public Kind CurrentKind => kind;
        public bool IsVisible => visibleState;

        /// <summary>编辑器搭建注入渲染器。</summary>
        public void Configure(Renderer r) { symbolRenderer = r; }

        /// <summary>显隐（由锚点 SetMarkerVisible 驱动；只隐渲染，billboard 保留）。</summary>
        public void SetVisible(bool v)
        {
            visibleState = v;
            if (symbolRenderer != null) symbolRenderer.enabled = v;
        }

        /// <summary>应用符号类型：生成程序化贴图并写入实例化材质（prefab 共享材质不受污染）。</summary>
        public void Apply(Kind newKind, Color ink)
        {
            kind = newKind;
            if (symbolRenderer == null) return;
            Material mat = symbolRenderer.material; // 实例化
            mat.mainTexture = SymbolTexture(newKind, ink);
        }

        private void Update()
        {
            // billboard：+z 背向相机（quad 正面法线在 -z，Cull Off 双保险）
            camTimer += Time.unscaledDeltaTime;
            if (mainCam == null || camTimer >= camRefresh)
            {
                mainCam = Camera.main;
                camTimer = 0f;
            }
            if (mainCam != null)
            {
                Vector3 away = transform.position - mainCam.transform.position;
                away.y = 0f; // 只水平朝向，避免随头部俯仰翻转
                if (away.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
                }
            }
        }

        /// <summary>类型→符号映射（私密优先，其余按 fragment.type；与锚点配色同源）。</summary>
        public static Kind KindFor(bool isPrivate, string fragmentType)
        {
            if (isPrivate) return Kind.Private;
            switch (fragmentType)
            {
                case "audio": return Kind.Audio;
                case "diary": return Kind.Diary;
                case "mixed": return Kind.Mixed;
                case "image":
                default: return Kind.Image;
            }
        }

        /// <summary>程序化符号贴图（64×64 RGBA32，透明底 + 类型形状，零外部素材）。</summary>
        public static Texture2D SymbolTexture(Kind kind, Color ink)
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    float a = ShapeAlpha(kind, u, v);
                    Color c = ink;
                    c.a = a;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>形状判定：返回该像素的不透明度（0 透明 / 1 实心 / 边缘平滑过渡）。</summary>
        private static float ShapeAlpha(Kind kind, float u, float v)
        {
            switch (kind)
            {
                case Kind.Image: return ImageShape(u, v);
                case Kind.Audio: return AudioShape(u, v);
                case Kind.Diary: return DiaryShape(u, v);
                case Kind.Mixed: return MixedShape(u, v);
                case Kind.Private: return PrivateShape(u, v);
                default: return 0f;
            }
        }

        // —— 形状函数（u,v ∈ [0,1]，v 向上；边缘 1.5px 平滑）——

        /// <summary>图片：矩形边框 + 山形三角 + 太阳圆。</summary>
        private static float ImageShape(float u, float v)
        {
            float frame = RectEdge(u, v, 0.10f, 0.10f, 0.90f, 0.90f, 0.055f);
            float mountain = Triangle(u, v, 0.22f, 0.30f, 0.78f, 0.30f, 0.50f, 0.72f);
            float sun = Disc(u, v, 0.70f, 0.72f, 0.075f);
            return Mathf.Max(frame, Mathf.Max(mountain, sun));
        }

        /// <summary>录音：中心声源圆点 + 右侧两道声波弧。</summary>
        private static float AudioShape(float u, float v)
        {
            float core = Disc(u, v, 0.38f, 0.5f, 0.07f);
            float wave1 = ArcBand(u, v, 0.38f, 0.5f, 0.20f, 0.042f, 60f);
            float wave2 = ArcBand(u, v, 0.38f, 0.5f, 0.34f, 0.042f, 60f);
            return Mathf.Max(core, Mathf.Max(wave1, wave2));
        }

        /// <summary>日记：竖矩形边框 + 两条横线（书页）。</summary>
        private static float DiaryShape(float u, float v)
        {
            float page = RectEdge(u, v, 0.26f, 0.12f, 0.74f, 0.88f, 0.05f);
            float line1 = HLine(u, v, 0.34f, 0.66f, 0.62f, 0.035f);
            float line2 = HLine(u, v, 0.34f, 0.66f, 0.46f, 0.035f);
            return Mathf.Max(page, Mathf.Max(line1, line2));
        }

        /// <summary>混合：实心菱形 + 中心透明圆眼（星形母题）。</summary>
        private static float MixedShape(float u, float v)
        {
            float diamond = Mathf.Clamp01((0.40f - (Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.5f))) / 0.025f);
            float hole = Disc(u, v, 0.5f, 0.5f, 0.10f);
            return Mathf.Clamp01(diamond - hole);
        }

        /// <summary>私密：锁环（上半圆弧）+ 锁体 + 透明锁孔。</summary>
        private static float PrivateShape(float u, float v)
        {
            float ring = ArcBand(u, v, 0.5f, 0.58f, 0.17f, 0.05f, 0f, 180f);
            float body = RectFill(u, v, 0.28f, 0.26f, 0.72f, 0.56f);
            float keyhole = Disc(u, v, 0.5f, 0.41f, 0.07f) + VLine(u, v, 0.5f, 0.30f, 0.42f, 0.03f);
            return Mathf.Clamp01(Mathf.Max(ring, body) - Mathf.Clamp01(keyhole));
        }

        // —— 基元 ——
        private static float Disc(float u, float v, float cx, float cy, float r)
        {
            return Mathf.Clamp01((r - Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy))) / 0.02f);
        }

        /// <summary>矩形边框（内外双线带）。</summary>
        private static float RectEdge(float u, float v, float x0, float y0, float x1, float y1, float w)
        {
            bool inside = u >= x0 && u <= x1 && v >= y0 && v <= y1;
            if (!inside) return 0f;
            float d = Mathf.Min(Mathf.Min(u - x0, x1 - u), Mathf.Min(v - y0, y1 - v));
            return Mathf.Clamp01((w - d) / 0.02f);
        }

        private static float RectFill(float u, float v, float x0, float y0, float x1, float y1)
        {
            bool inside = u >= x0 && u <= x1 && v >= y0 && v <= y1;
            if (!inside) return 0f;
            float d = Mathf.Min(Mathf.Min(u - x0, x1 - u), Mathf.Min(v - y0, y1 - v));
            return Mathf.Clamp01(d / 0.02f + 0.5f);
        }

        /// <summary>以 (cx,cy) 为心、半径 R、带宽 w 的圆弧带；angleLimit=角度半幅（60=朝 +x 两侧各 60°），range 限弧角区间。</summary>
        private static float ArcBand(float u, float v, float cx, float cy, float radius, float w, float angleLimit)
        {
            float dx = u - cx;
            float dy = v - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float band = Mathf.Clamp01((w - Mathf.Abs(dist - radius)) / 0.02f);
            if (dx <= 0f) return 0f; // 只画 +x 一侧（声波）
            float ang = Mathf.Abs(Mathf.Atan2(dy, dx)) * Mathf.Rad2Deg;
            return ang <= angleLimit ? band : 0f;
        }

        private static float ArcBand(float u, float v, float cx, float cy, float radius, float w, float a0, float a1)
        {
            float dx = u - cx;
            float dy = v - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float band = Mathf.Clamp01((w - Mathf.Abs(dist - radius)) / 0.02f);
            float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            return (ang >= a0 && ang <= a1) ? band : 0f;
        }

        /// <summary>三角形（三点重心式带边缘平滑）。</summary>
        private static float Triangle(float u, float v, float x0, float y0, float x1, float y1, float x2, float y2)
        {
            float d0 = (u - x1) * (y0 - y1) - (x0 - x1) * (v - y1);
            float d1 = (u - x2) * (y1 - y2) - (x1 - x2) * (v - y2);
            float d2 = (u - x0) * (y2 - y0) - (x2 - x0) * (v - y0);
            bool inside = (d0 >= 0 && d1 >= 0 && d2 >= 0) || (d0 <= 0 && d1 <= 0 && d2 <= 0);
            if (!inside) return 0f;
            // 距最近边的粗略深度
            float depth = Mathf.Min(Mathf.Abs(d0), Mathf.Min(Mathf.Abs(d1), Mathf.Abs(d2)));
            return Mathf.Clamp01(depth / 0.02f);
        }

        private static float HLine(float u, float v, float x0, float x1, float y, float halfW)
        {
            if (u < x0 || u > x1) return 0f;
            return Mathf.Clamp01((halfW - Mathf.Abs(v - y)) / 0.02f);
        }

        private static float VLine(float u, float v, float x, float y0, float y1, float halfW)
        {
            if (v < y0 || v > y1) return 0f;
            return Mathf.Clamp01((halfW - Mathf.Abs(u - x)) / 0.02f);
        }
    }
}
