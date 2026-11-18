using System.Collections.Generic;
using MemoryCorridor.Core;
using MemoryCorridor.Interaction;
using UnityEngine;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 回忆卡片合成器（R15 ②多线索卡片 + R16-1 音频角标）：
    /// 走读结束时本地合成 720×960 卡片版式 PNG（零外部素材、不联网）：
    /// 房间标题 / 走读完成瞬间截图缩略 / 路径点串（每站一圆点，末站高亮）/ 关键词 / 时间 / 站数；
    /// 碎片含音频时右上角"含音频 时长"角标（WAV 头精确解析 / MP3 比特率估算），无音频完全不渲染。
    /// 渲染管线：专用正交相机渲染临时卡板 → ReadPixels 从 RT 读取落盘，
    /// 绕开坑-041（batchmode ScreenCapture 不落盘）；渲染期间禁用其他相机并保存恢复。
    /// 尺寸令牌见 UiTokens（R16-4 收敛）。
    /// </summary>
    public static class MemoryCardComposer
    {
        /// <summary>卡片元数据（探针断言与文档证据读取）。</summary>
        public class CardMeta
        {
            public string Title;
            public string Keywords;
            public string TimeText;
            public int StationCount;
            public string PngPath;
            public int PngBytes;      // 落盘字节数（>0 即落盘证据）
            public bool ShotCaptured; // 截图缩略是否真实渲染（false=占位）
            public bool BadgeVisible;       // R16-1：音频角标是否渲染（无音频=false）
            public string BadgeDurationText; // R16-1：角标时长文案（如 "0:12"，无音频=null）
        }

        public static CardMeta LastComposeMeta { get; private set; }

        private const int Width = 720;
        private const int Height = 960;
        private const int ShotWidth = 560;
        private const int ShotHeight = 320;
        private const string ComposePos = "0,-500,0"; // 远离场景几何，专用相机只拍到卡板

        /// <summary>合成卡片：返回元数据（null=失败）；PNG 落盘 persistentDataPath/screenshots/。</summary>
        public static CardMeta ComposeCard(RoomData room, List<MemoryFragment> route)
        {
            if (room == null || route == null || route.Count == 0) return null;

            Camera mainCam = Camera.main;
            // 1) 主相机渲染一帧截图缩略（失败降级占位，不阻断卡片生成）
            Texture2D shotTex = null;
            RenderTexture shotRT = null;
            if (mainCam != null)
            {
                try
                {
                    shotRT = new RenderTexture(ShotWidth, ShotHeight, 24, RenderTextureFormat.ARGB32);
                    RenderTexture prevTarget = mainCam.targetTexture;
                    mainCam.targetTexture = shotRT;
                    mainCam.Render();
                    mainCam.targetTexture = prevTarget;
                    RenderTexture.active = shotRT;
                    shotTex = new Texture2D(ShotWidth, ShotHeight, TextureFormat.RGBA32, false);
                    shotTex.ReadPixels(new Rect(0, 0, ShotWidth, ShotHeight), 0, 0);
                    shotTex.Apply();
                    RenderTexture.active = null;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[CardComposer] 截图缩略渲染失败，用占位: " + e.Message);
                    shotTex = null;
                }
            }

            // 2) 禁用其他相机（保存状态，渲染完恢复）
            Camera[] allCams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            bool[] savedEnabled = new bool[allCams.Length];
            for (int i = 0; i < allCams.Length; i++)
            {
                savedEnabled[i] = allCams[i].enabled;
                allCams[i].enabled = false;
            }

            CardMeta meta = null;
            try
            {
                meta = ComposeWithDedicatedCamera(room, route, shotTex);
            }
            finally
            {
                for (int i = 0; i < allCams.Length; i++) allCams[i].enabled = savedEnabled[i];
                if (shotRT != null) shotRT.Release();
            }
            LastComposeMeta = meta;
            return meta;
        }

        /// <summary>R16-1：汇总路线音频时长（首个可解析的音频媒体项）。</summary>
        public static void ComputeBadge(List<MemoryFragment> route, CardMeta meta)
        {
            if (route == null) return;
            for (int i = 0; i < route.Count && meta.BadgeDurationText == null; i++)
            {
                List<MediaItem> items = route[i].mediaItems;
                if (items == null) continue;
                for (int m = 0; m < items.Count; m++)
                {
                    if (items[m].kind != "audio") continue;
                    string path = MediaResolver.ResolveExisting(items[m].path);
                    if (path == null) continue;
                    float seconds = path.ToLower().EndsWith(".wav")
                        ? WavDurationSeconds(path)
                        : EstimateSecondsBySize(path, 128000);
                    if (seconds <= 0f) continue;
                    meta.BadgeVisible = true;
                    meta.BadgeDurationText = FormatTime(seconds);
                    return;
                }
            }
            meta.BadgeVisible = false; // 无音频：完全不渲染角标（不留灰占位）
        }

        /// <summary>解析 WAV 头时长（RIFF→fmt.byteRate + data.size，秒）。</summary>
        public static float WavDurationSeconds(string path)
        {
            try
            {
                byte[] b = System.IO.File.ReadAllBytes(path);
                int fmtPos = FindChunk(b, "fmt ");
                int dataPos = FindChunk(b, "data");
                if (fmtPos < 0 || dataPos < 0) return 0f;
                int byteRate = System.BitConverter.ToInt32(b, fmtPos + 16); // fmt 块内偏移 16=AvgBytesPerSec
                int dataSize = System.BitConverter.ToInt32(b, dataPos + 4);
                return byteRate > 0 ? dataSize / (float)byteRate : 0f;
            }
            catch (System.Exception)
            {
                return 0f;
            }
        }

        /// <summary>MP3 无头解析不可靠，按文件大小/比特率估算（秒）。</summary>
        public static float EstimateSecondsBySize(string path, int bitRate)
        {
            try
            {
                long bytes = new System.IO.FileInfo(path).Length;
                return bitRate > 0 ? bytes * 8f / bitRate : 0f;
            }
            catch (System.Exception)
            {
                return 0f;
            }
        }

        /// <summary>m:ss 文案（1.5s→"0:01"，floor）。</summary>
        public static string FormatTime(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        private static int FindChunk(byte[] b, string tag)
        {
            for (int i = 12; i + 4 < b.Length; i++)
            {
                if (b[i] == tag[0] && b[i + 1] == tag[1] && b[i + 2] == tag[2] && b[i + 3] == tag[3])
                {
                    return i;
                }
            }
            return -1;
        }

        private static CardMeta ComposeWithDedicatedCamera(RoomData room, List<MemoryFragment> route, Texture2D shotTex)
        {
            Vector3 rootPos;
            {
                string[] parts = ComposePos.Split(',');
                rootPos = new Vector3(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
            }
            GameObject root = BuildCardRoot(rootPos, room, route, shotTex, out CardMeta meta);

            // 专用相机渲染到 RT → ReadPixels → PNG 落盘
            GameObject camGo = new GameObject("CardComposeCamera");
            camGo.transform.position = rootPos + new Vector3(0, 0, -2f);
            camGo.transform.rotation = Quaternion.identity;
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = UiTokens.CardBoardH * 0.5f; // 半高 0.48 → 视野高 0.96
            cam.aspect = (float)Width / Height;  // 0.75 → 视野宽 0.72
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 4f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0); // 卡板外透明

            RenderTexture cardRT = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = cardRT;
            cam.Render();
            RenderTexture.active = cardRT;
            Texture2D outTex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            outTex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            outTex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            cardRT.Release();

            string dir = System.IO.Path.Combine(Application.persistentDataPath, "screenshots");
            System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, "memory_card_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png");
            byte[] png = outTex.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, png);
            meta.PngPath = path;
            meta.PngBytes = png.Length;

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(camGo);

            Debug.Log("[CardComposer] 回忆卡片合成: " + path + "（" + png.Length + " 字节，缩略=" + (shotTex != null ? "真实截图" : "占位")
                + "，角标=" + (meta.BadgeVisible ? meta.BadgeDurationText : "无") + "）");
            return meta;
        }

        /// <summary>
        /// 构建卡片版式世界空间根（R16-4：与渲染分离，供距离实测等复用）。
        /// 坐标系：根在 rootPos，卡板 0.72×0.96 居中，y 向上。
        /// </summary>
        public static GameObject BuildCardRoot(Vector3 rootPos, RoomData room, List<MemoryFragment> route,
            Texture2D shotTex, out CardMeta meta)
        {
            meta = new CardMeta
            {
                Title = room.title,
                StationCount = route.Count,
                ShotCaptured = shotTex != null,
            };
            ComputeBadge(route, meta);

            GameObject root = new GameObject("MemoryCardComposeRoot");
            root.transform.position = rootPos;

            Shader spriteShader = Shader.Find("MemoryCorridor/Sprite3D");
            // 底板（圆角 + 柔描边）
            Texture2D cardTex = RoundedRectTexture.RoundedRect(Width, Height, 40, UiTokens.CardBg, UiTokens.LineSoft, 3);
            Material cardMat = new Material(spriteShader);
            cardMat.mainTexture = cardTex;
            Material flatMat = new Material(spriteShader); // 纯色元素共用（白底 tint 上色）

            CreateQuad(root.transform, "CardBoard", new Vector3(0, 0, 0),
                new Vector3(UiTokens.CardBoardW, UiTokens.CardBoardH, 1), cardMat);

            // 标题
            UiTextFactory.CreateTextMesh(root.transform, "Title", new Vector3(0, 0.43f, -0.01f),
                64, 0.010f, TextAnchor.MiddleCenter, TextAlignment.Center, UiTokens.Amber,
                "漫步回望 · " + room.title);

            // 截图缩略（无截图→ThumbBg 占位）
            Texture2D shotFrameTex = RoundedRectTexture.RoundedRect(ShotWidth, ShotHeight, 24, UiTokens.FrameBg, UiTokens.LineSoft, 2);
            Material shotFrameMat = new Material(spriteShader);
            shotFrameMat.mainTexture = shotFrameTex;
            CreateQuad(root.transform, "ShotFrame", new Vector3(0, 0.21f, -0.01f),
                new Vector3(UiTokens.CardShotW, UiTokens.CardShotH, 1), shotFrameMat);
            if (shotTex != null)
            {
                Material shotMat = new Material(spriteShader);
                shotMat.mainTexture = shotTex;
                CreateQuad(root.transform, "ShotThumb", new Vector3(0, 0.21f, -0.02f),
                    new Vector3(UiTokens.CardShotW - 0.04f, UiTokens.CardShotH - 0.04f, 1), shotMat);
            }

            // R16-1：音频角标（叠在截图缩略右上角内侧：声波图标 + 时长；无音频不渲染任何元素）
            if (meta.BadgeVisible)
            {
                float badgeX = 0.20f;
                float badgeY = 0.33f;
                Texture2D badgeTex = RoundedRectTexture.RoundedRect(200, 64, (int)UiTokens.BadgeRadius, UiTokens.LabelBg, UiTokens.LineSoft, 1);
                Material badgeMat = new Material(spriteShader);
                badgeMat.mainTexture = badgeTex;
                CreateQuad(root.transform, "BadgeBoard", new Vector3(badgeX, badgeY, -0.01f),
                    new Vector3(0.20f, 0.052f, 1), badgeMat);
                Material iconMat = new Material(spriteShader);
                iconMat.mainTexture = LightPointSymbol.SymbolTexture(LightPointSymbol.Kind.Audio, UiTokens.Cyan);
                CreateQuad(root.transform, "BadgeIcon", new Vector3(badgeX - 0.075f, badgeY, -0.02f),
                    new Vector3(UiTokens.BadgeIconSize, UiTokens.BadgeIconSize, 1), iconMat);
                UiTextFactory.CreateTextMesh(root.transform, "BadgeText",
                    new Vector3(badgeX + 0.012f, badgeY, -0.02f),
                    34, 0.009f, TextAnchor.MiddleCenter, TextAlignment.Left, UiTokens.Cyan,
                    "含音频 " + meta.BadgeDurationText);
            }

            // 路径点串：每站一圆点 + 连线，末站高亮（MoL path 要素的卡片投影）
            int n = Mathf.Min(route.Count, 12);
            float spacing = 0.055f;
            float startX = -((n - 1) * spacing) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float x = startX + i * spacing;
                Color dotColor = (i == n - 1) ? UiTokens.Amber : UiTokens.LabelInk;
                if (i > 0)
                {
                    Material lineMat = new Material(flatMat);
                    Color lineSolid = UiTokens.LineSoft; lineSolid.a = 1f; lineMat.SetColor("_Color", lineSolid); // LineSoft 不透明派生（R18-2 去字面量）
                    CreateQuad(root.transform, "DotLine" + i, new Vector3(x - spacing * 0.5f, -0.02f, -0.005f),
                        new Vector3(spacing, 0.006f, 1), lineMat);
                }
                Material dotMat = new Material(flatMat);
                dotMat.mainTexture = DotTexture();
                dotMat.SetColor("_Color", dotColor);
                CreateQuad(root.transform, "Dot" + i, new Vector3(x, -0.02f, -0.006f),
                    new Vector3(0.024f, 0.024f, 1), dotMat);
            }

            // 关键词（最多 3 个碎片标题）
            string keywords = "";
            for (int i = 0; i < route.Count && i < 3; i++)
            {
                string t = route[i].title ?? "";
                if (t.Length > 10) t = t.Substring(0, 10) + "…";
                keywords += (i > 0 ? " · " : "") + t;
            }
            meta.Keywords = keywords;
            UiTextFactory.CreateTextMesh(root.transform, "Keywords", new Vector3(0, -0.13f, -0.01f),
                44, 0.009f, TextAnchor.MiddleCenter, TextAlignment.Center, UiTokens.LabelInk, keywords);

            // 时间 + 站数
            string timeText = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            meta.TimeText = timeText;
            UiTextFactory.CreateTextMesh(root.transform, "TimeText", new Vector3(0, -0.24f, -0.01f),
                40, 0.009f, TextAnchor.MiddleCenter, TextAlignment.Center, UiTokens.CaptionInk, timeText);
            UiTextFactory.CreateTextMesh(root.transform, "StationText", new Vector3(0, -0.33f, -0.01f),
                36, 0.009f, TextAnchor.MiddleCenter, TextAlignment.Center, UiTokens.DimInk, "全程 " + route.Count + " 站");

            return root;
        }

        /// <summary>渲染已建卡板到 PNG（R16-4：与构建分离；dist/fov 由调用方配相机）。</summary>
        public static byte[] RenderCardRoot(GameObject root, Camera cam, int width, int height)
        {
            RenderTexture cardRT = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = cardRT;
            cam.Render();
            RenderTexture.active = cardRT;
            Texture2D outTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            outTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            outTex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            cardRT.Release();
            return outTex.EncodeToPNG();
        }

        private static void CreateQuad(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col); // 坑-043：不进物理系统
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static Texture2D _dot;
        /// <summary>实心圆点贴图（32×32，一次性生成复用）。</summary>
        private static Texture2D DotTexture()
        {
            if (_dot != null) return _dot;
            const int size = 32;
            _dot = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] px = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((0.92f - d) / 0.08f); // 边缘 1px 平滑
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            _dot.SetPixels(px);
            _dot.Apply();
            return _dot;
        }
    }
}
