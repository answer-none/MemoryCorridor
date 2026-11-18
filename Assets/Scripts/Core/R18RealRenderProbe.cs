using System.Collections;
using MemoryCorridor.Media;
using UnityEngine;

namespace MemoryCorridor.Core
{
    /// <summary>
    /// R18-1 真渲染采样探针（GUI Play 模式，区别于 batchmode 口径）：
    /// 3 机位各采 30 帧——平均帧耗时 / 1% Low（最差帧，30 样本口径）/ 可见 Renderer 数 / 可见三角面；
    /// 主题对照真渲染重拍（r18_theme_*，量化色差如实报告，门槛不改）。
    /// 入口：R18RealRenderProbe.Run（编辑器态）——开场景→禁用 SmokeProbe→挂本组件→isPlaying=true。
    /// 输出：Temp/r18_realrender.txt + docs/evidence/r18_*。用法（无 -batchmode）：
    /// Unity.exe -projectPath … -executeMethod MemoryCorridor.Core.R18RealRenderProbe.Run -logFile …
    /// </summary>
    public class R18RealRenderProbe : MonoBehaviour
    {
#if UNITY_EDITOR
        /// <summary>编辑器入口：装配场景并进入 Play（GUI 模式）。</summary>
        public static void Run()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Scenes/EditorScene.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            foreach (SmokeProbe sp in Object.FindObjectsByType<SmokeProbe>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(sp); // 防冒烟断言与采样竞争退出
            }
            var host = new GameObject("R18ProbeHost");
            host.AddComponent<R18RealRenderProbe>();
            Debug.Log("[R18] 场景装配完成，进入 Play（真渲染 GUI 口径）…");
            UnityEditor.EditorApplication.isPlaying = true;
        }
#endif

        private IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(4f); // 等光点 spawn/主题/桥接稳定

            Camera cam = Camera.main;
            Vector3 savedP = cam.transform.position;
            Quaternion savedR = cam.transform.rotation;
            Vector3[] spots =
            {
                new Vector3(0f, 1.4f, 0.5f),
                new Vector3(0f, 1.4f, 1.5f),
                new Vector3(0f, 1.4f, 2.6f),
            };
            string[] names = { "出生点", "房间中心", "窗面全览" };
            var lines = new System.Collections.Generic.List<string>
            {
                "| 机位 | 平均帧耗时 | 1% Low | Draw Call（口径） | 可见三角面 |",
                "|---|---|---|---|---|",
            };
            float worstAvg = 0f, worstLow = 0f;
            int worstDc = 0;
            long worstTri = 0;

            for (int s = 0; s < spots.Length; s++)
            {
                cam.transform.position = spots[s];
                cam.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                yield return new WaitForSecondsRealtime(0.6f);

                float[] dt = new float[30];
                float prev = Time.realtimeSinceStartup;
                for (int f = 0; f < 30; f++)
                {
                    yield return null;
                    float now = Time.realtimeSinceStartup;
                    dt[f] = now - prev;
                    prev = now;
                }
                float sum = 0f, worst = 0f;
                for (int i = 0; i < 30; i++) { sum += dt[i]; if (dt[i] > worst) worst = dt[i]; }
                float avgMs = sum / 30f * 1000f;
                float low1Ms = worst * 1000f; // 30 样本的 1% Low = 最差帧（样本小，真机需 300+ 帧）

                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
                long tri = 0;
                int rc = 0;
                Renderer[] rs = Object.FindObjectsByType<Renderer>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < rs.Length; i++)
                {
                    if (!GeometryUtility.TestPlanesAABB(planes, rs[i].bounds)) continue;
                    rc++;
                    MeshFilter mf = rs[i].GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null) tri += mf.sharedMesh.triangles.Length / 3;
                    if (rs[i].GetComponent<TextMesh>() != null) tri += 24;
                }
                if (avgMs > worstAvg) worstAvg = avgMs;
                if (low1Ms > worstLow) worstLow = low1Ms;
                if (rc > worstDc) worstDc = rc;
                if (tri > worstTri) worstTri = tri;
                lines.Add("| " + names[s] + " | " + avgMs.ToString("F1") + " ms | " + low1Ms.ToString("F1")
                    + " ms | " + rc + "（Renderer 上界） | " + (tri / 1000) + "k |");
                Debug.Log("[R18] " + names[s] + ": avg=" + avgMs.ToString("F1") + "ms 1%Low=" + low1Ms.ToString("F1")
                    + "ms DC(上界)=" + rc + " tri=" + tri);
                yield return null;
            }

            cam.transform.position = savedP;
            cam.transform.rotation = savedR;

            // 主题对照真渲染重拍（room_03 同机位，量化色差如实报告——门槛不改变）
            RoomManager rm = Object.FindFirstObjectByType<RoomManager>(FindObjectsInactive.Include);
            int rainIdx = -1;
            for (int i = 0; i < rm.Rooms.Count; i++) if (rm.Rooms[i].id == "room_03") rainIdx = i;
            rm.SwitchToRoom(rainIdx);
            yield return new WaitForSecondsRealtime(1.0f);
            rm.SetTheme("default", false);
            yield return new WaitForSecondsRealtime(0.5f);
            byte[] shotA = RenderMain(800, 600);
            SavePng("docs/evidence/r18_theme_default.png", shotA);
            rm.SetTheme("rain", false);
            yield return new WaitForSecondsRealtime(0.5f);
            byte[] shotB = RenderMain(800, 600);
            SavePng("docs/evidence/r18_theme_rain.png", shotB);

            float avgDiff, ratio;
            ComputePngDiff(shotA, shotB, out avgDiff, out ratio);
            lines.Add("");
            lines.Add("**主题对照真渲染量化**：平均色差 " + (avgDiff * 255).ToString("F1") + "/255（门槛 ≥20）"
                + "，色差>25/255 像素占比 " + (ratio * 100).ToString("F0") + "%（门槛 ≥30%）"
                + (avgDiff * 255 >= 20f && ratio >= 0.30f ? "——达标 ✅" : "——未达标（如实报告，不调门槛）"));
            SaveCompareCard(shotA, shotB, avgDiff, ratio);

            rm.SetTheme("default", false);
            rm.SwitchToRoom(0);

            System.IO.File.WriteAllLines("Temp/r18_realrender.txt", lines);
            Debug.Log("[R18] 完成。\n" + string.Join("\n", lines.ToArray()));
#if UNITY_EDITOR
            yield return new WaitForSecondsRealtime(0.5f);
            UnityEditor.EditorApplication.Exit(0);
#endif
        }

        private static byte[] RenderMain(int w, int h)
        {
            Camera cam = Camera.main;
            RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            RenderTexture prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            RenderTexture.active = rt;
            Texture2D shot = new Texture2D(w, h, TextureFormat.RGBA32, false);
            shot.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            shot.Apply();
            RenderTexture.active = null;
            rt.Release();
            return shot.EncodeToPNG();
        }

        private static void SavePng(string path, byte[] png)
        {
            System.IO.File.WriteAllBytes(path, png);
            Debug.Log("[R18] 证据: " + path + "（" + png.Length + " 字节）");
        }

        private static void ComputePngDiff(byte[] ba, byte[] bb, out float avgDiff, out float ratio)
        {
            avgDiff = 0f;
            ratio = 0f;
            Texture2D a = new Texture2D(2, 2);
            a.LoadImage(ba);
            Texture2D b = new Texture2D(2, 2);
            b.LoadImage(bb);
            int w = Mathf.Min(a.width, b.width);
            int h = Mathf.Min(a.height, b.height);
            Color[] pa = a.GetPixels(0, 0, w, h);
            Color[] pb = b.GetPixels(0, 0, w, h);
            float total = 0f;
            int over = 0;
            for (int i = 0; i < pa.Length; i++)
            {
                float d = (Mathf.Abs(pa[i].r - pb[i].r) + Mathf.Abs(pa[i].g - pb[i].g)
                    + Mathf.Abs(pa[i].b - pb[i].b)) / 3f;
                total += d;
                if (d > 25f / 255f) over++;
            }
            avgDiff = total / pa.Length;
            ratio = over / (float)pa.Length;
        }

        /// <summary>对照卡板（R17 同款管线，标题标注 R18 真渲染）。</summary>
        private static void SaveCompareCard(byte[] shotA, byte[] shotB, float avgDiff, float ratio)
        {
            Shader sh = Shader.Find("MemoryCorridor/Sprite3D");
            Vector3 pos = new Vector3(-12f, -500f, 0f);
            GameObject root = new GameObject("R18CompareRoot");
            root.transform.position = pos;
            Material bm = new Material(sh);
            bm.mainTexture = RoundedRectTexture.RoundedRect(1500, 960, 40, UiTokens.CardBg, UiTokens.LineSoft, 3);
            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Collider bc = board.GetComponent<Collider>();
            if (bc != null) Object.DestroyImmediate(bc);
            board.transform.SetParent(root.transform, false);
            board.transform.localScale = new Vector3(1.5f, 0.96f, 1);
            board.GetComponent<MeshRenderer>().sharedMaterial = bm;

            UiTextFactory.CreateTextMesh(root.transform, "T", new Vector3(0, 0.40f, -0.01f),
                54, 0.010f, TextAnchor.MiddleCenter, TextAlignment.Center, UiTokens.Amber,
                "主题对照（R18 真渲染 GUI 口径）");
            Texture2D ta = new Texture2D(2, 2); ta.LoadImage(shotA);
            Texture2D tb = new Texture2D(2, 2); tb.LoadImage(shotB);
            Material ma = new Material(sh); ma.mainTexture = ta;
            Material mb = new Material(sh); mb.mainTexture = tb;
            MakeQuad(root.transform, "A", new Vector3(-0.36f, 0.02f, -0.01f), new Vector3(0.66f, 0.50f, 1), ma, "默认主题");
            MakeQuad(root.transform, "B", new Vector3(0.36f, 0.02f, -0.01f), new Vector3(0.66f, 0.50f, 1), mb, "雨夜校园");
            ThemeColors.ThemeSpec sa = ThemeColors.For("default");
            ThemeColors.ThemeSpec sr = ThemeColors.For("rain");
            UiTextFactory.CreateTextMesh(root.transform, "VA", new Vector3(-0.36f, -0.30f, -0.01f),
                30, 0.009f, TextAnchor.MiddleCenter, TextAlignment.Center, UiTokens.CaptionInk,
                "default: ambient(" + sa.Ambient.r.ToString("F2") + "," + sa.Ambient.g.ToString("F2") + ","
                + sa.Ambient.b.ToString("F2") + ") fogD " + sa.FogDensity.ToString("F2"));
            UiTextFactory.CreateTextMesh(root.transform, "VB", new Vector3(0.36f, -0.30f, -0.01f),
                30, 0.009f, TextAnchor.MiddleCenter, TextAlignment.Center, UiTokens.RainBlue,
                "rain: ambient(" + sr.Ambient.r.ToString("F2") + "," + sr.Ambient.g.ToString("F2") + ","
                + sr.Ambient.b.ToString("F2") + ") fogD " + sr.FogDensity.ToString("F2"));
            UiTextFactory.CreateTextMesh(root.transform, "Q", new Vector3(0, -0.40f, -0.01f),
                28, 0.009f, TextAnchor.MiddleCenter, TextAlignment.Center, UiTokens.DimInk,
                "真渲染量化: avg " + (avgDiff * 255).ToString("F1") + "/255（≥20） · ratio "
                + (ratio * 100).ToString("F0") + "%（≥30%）");

            GameObject cg = new GameObject("R18CompareCamera");
            cg.transform.position = pos + new Vector3(0, 0, -2f);
            Camera cam = cg.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.48f;
            cam.aspect = 1500f / 960f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 4f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UiTokens.EvidenceBg;
            byte[] png = RenderCard(cam, 1500, 960, root);
            System.IO.File.WriteAllBytes("docs/evidence/r18_theme_compare.png", png);
            Debug.Log("[R18] 对照卡板: docs/evidence/r18_theme_compare.png（" + png.Length + " 字节）");
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(cg);
        }

        private static void MakeQuad(Transform parent, string name, Vector3 localPos,
            Vector3 scale, Material mat, string label)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            UiTextFactory.CreateTextMesh(go.transform, "Label", new Vector3(0, -0.72f, -0.01f),
                40, 0.010f, TextAnchor.MiddleCenter, TextAlignment.Center, UiTokens.LabelInk, label);
        }

        private static byte[] RenderCard(Camera cam, int w, int h, GameObject root)
        {
            RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outTex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            outTex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();
            return outTex.EncodeToPNG();
        }
    }
}
