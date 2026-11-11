using System.Collections;
using MemoryCorridor.Interaction;
using MemoryCorridor.Media;
using UnityEngine;
using UnityEngine.Rendering;

namespace MemoryCorridor.Core
{
    /// <summary>
    /// W2 自动冒烟探针（Editor Play）：XRI 装配 / 悬停预览 / 选中 / 褪色 / 排序 /
    /// 走读信标 / 回访复位 / 房间切换 / 重演四拍 / ImportServer HTTP。
    /// batchmode PASS→Exit(0)，FAIL→Exit(1)；60s 看门狗防挂死。
    /// 坑-017（yield 不进 try/catch）、坑-018（真实时间预算）、坑-021（含未激活搜索）。
    /// </summary>
    public class SmokeProbe : MonoBehaviour
    {
        private bool previewFired;
        private bool selectedFired;

        private void Awake()
        {
            StartCoroutine(Watchdog());
        }

        private IEnumerator Start()
        {
            yield return null;
            yield return null; // 等全部 Start（RoomManager 生成光点、桥接懒接线首帧）

            bool pass = true;
            int failedSections = 0;

            // ========== A. 房间与光点初始态 ==========
            RoomManager roomManager = FindFirst<RoomManager>();
            LightPointAnchor fresh = FindAnchor("LP_frag_01");
            LightPointAnchor faded = FindAnchor("LP_frag_03");
            PanoramaDome dome = FindFirst<PanoramaDome>();

            if (roomManager == null || roomManager.CurrentRoomIndex != 0 || roomManager.SpawnedAnchorCount != 3)
            {
                Debug.LogError("[SmokeProbe] FAIL-A: 房间初始态异常 (mgr=" + (roomManager == null)
                    + " idx=" + (roomManager != null ? roomManager.CurrentRoomIndex : -99)
                    + " count=" + (roomManager != null ? roomManager.SpawnedAnchorCount : -99) + ")");
                pass = false; failedSections++;
            }
            if (fresh == null || faded == null)
            {
                Debug.LogError("[SmokeProbe] FAIL-A: 演示锚点缺失 (fresh=" + (fresh == null) + " faded=" + (faded == null) + ")");
                pass = false; failedSections++;
            }
            if (dome == null || dome.GetComponent<MeshRenderer>().sharedMaterial.mainTexture == null)
            {
                Debug.LogError("[SmokeProbe] FAIL-A: 全景穹顶未加载");
                pass = false; failedSections++;
            }
            if (pass)
            {
                Debug.Log("[SmokeProbe] A 房间初始态 OK（room0×3 光点，穹顶已加载）");
            }

            // ========== B. XRI 装配 + 悬停预览 + 选中 + 褪色 ==========
            if (pass)
            {
                pass = TestXriAssembly();
                if (!pass) failedSections++;
            }
            if (pass)
            {
                // 悬停预览（真实时间预算，坑-018）
                previewFired = false;
                selectedFired = false;
                string previewId = "";
                string selectedId = "";
                fresh.OnHoverPreview += delegate (string id) { previewFired = true; previewId = id; };
                fresh.OnSelected += delegate (string id) { selectedFired = true; selectedId = id; };

                fresh.SetHighlight(true);
                float deadline = Time.realtimeSinceStartup + 2f;
                while (!previewFired && Time.realtimeSinceStartup < deadline)
                {
                    fresh.TickHover();
                    yield return null;
                }
                if (!previewFired)
                {
                    Debug.LogError("[SmokeProbe] FAIL-B: 悬停预览 0.3s 未触发");
                    pass = false; failedSections++;
                }
                else
                {
                    Debug.Log("[SmokeProbe] B 悬停预览 OK (fragment=" + previewId + ")");
                }

                if (pass)
                {
                    fresh.Select();
                    if (!selectedFired)
                    {
                        Debug.LogError("[SmokeProbe] FAIL-B: Select() 未触发 OnSelected");
                        pass = false; failedSections++;
                    }
                    else
                    {
                        Debug.Log("[SmokeProbe] B 选中 OK (fragment=" + selectedId + ")");
                    }
                }

                if (pass)
                {
                    float fadeFresh = fresh.CurrentFade;
                    float fadeFaded = faded.CurrentFade;
                    if (fadeFaded >= fadeFresh)
                    {
                        Debug.LogError("[SmokeProbe] FAIL-B: 褪色差异无效 fresh=" + fadeFresh
                            + " faded=" + fadeFaded);
                        pass = false; failedSections++;
                    }
                    else
                    {
                        Debug.Log("[SmokeProbe] B 褪色差异 OK: fresh=" + fadeFresh.ToString("F2")
                            + " faded=" + fadeFaded.ToString("F2"));
                    }
                }
            }

            // ========== C. RouteEditor 手动排序 ==========
            if (pass)
            {
                pass = TestRouteEditor();
                if (!pass) failedSections++;
            }

            // ========== D+E. 走读信标 + 回访复位 ==========
            if (pass)
            {
                pass = TestRoutePlayer(roomManager);
                if (!pass) failedSections++;
            }

            // ========== F. 房间切换 ==========
            if (pass)
            {
                yield return TestRoomSwitch(roomManager);
                // 协程版无法直接回传 bool，读取标志
                pass = sectionPass;
                if (!pass) failedSections++;
            }

            // ========== G. 回忆重演四拍 ==========
            if (pass)
            {
                yield return TestReplaySequencer(fresh);
                pass = sectionPass;
                if (!pass) failedSections++;
            }

            // ========== H. ImportServer HTTP ==========
            if (pass)
            {
                yield return new WaitForSecondsRealtime(0.6f); // 等监听线程
                try
                {
                    TestImportServer();
                    Debug.Log("[SmokeProbe] H ImportServer HTTP 冒烟 OK（GET/POST 上传落盘/POST 日记入档）");
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[SmokeProbe] FAIL-H: ImportServer 异常 " + FormatWebError(e));
                    pass = false; failedSections++;
                }
            }

            Debug.Log(pass
                ? "[SmokeProbe] PASS: 房间/XRI/悬停/选中/褪色/排序/走读/回访/切换/重演/导入 全部通过"
                : "[SmokeProbe] 结果: FAIL（失败段数=" + failedSections + "）");

#if UNITY_EDITOR
            yield return new WaitForSeconds(0.3f);
            UnityEditor.EditorApplication.Exit(pass ? 0 : 1);
#endif
        }

        private bool sectionPass;

        // ---------- B（装配断言） ----------
        private bool TestXriAssembly()
        {
            Unity.XR.CoreUtils.XROrigin origin = FindFirst<Unity.XR.CoreUtils.XROrigin>();
            UnityEngine.XR.Interaction.Toolkit.XRRayInteractor[] rays =
                UnityEngine.Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.XRRayInteractor>(
                    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
            UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable[] interactables =
                FindAll<UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable>();
            XRIAnchorBridge bridge = FindFirst<XRIAnchorBridge>();

            if (origin == null || rays.Length < 1 || interactables.Length < 3 || bridge == null)
            {
                Debug.LogError("[SmokeProbe] FAIL-B: XRI 装配缺失 (origin=" + (origin == null)
                    + " rays=" + rays.Length + " interactables=" + interactables.Length
                    + " bridge=" + (bridge == null) + ")");
                return false;
            }
            Debug.Log("[SmokeProbe] B XRI 装配 OK（rays×" + rays.Length + " 含未激活——ModalityManager 预期）");
            return true;
        }

        // ---------- C ----------
        private bool TestRouteEditor()
        {
            bool moved = RouteEditor.MoveFragment("frag_01", 1);
            AppData reloaded = SaveDataService.LoadAppData();
            MemoryFragment f1 = FindFragmentById(reloaded, "frag_01");
            MemoryFragment f2 = FindFragmentById(reloaded, "frag_02");
            bool swapped = moved && f1.order == 1 && f2.order == 0;
            if (!swapped)
            {
                Debug.LogError("[SmokeProbe] FAIL-C: 手动排序失败 (moved=" + moved
                    + " f1.order=" + (f1 != null ? f1.order : -99) + ")");
                return false;
            }

            RouteEditor.MoveFragment("frag_01", -1); // 恢复
            reloaded = SaveDataService.LoadAppData();
            f1 = FindFragmentById(reloaded, "frag_01");
            if (f1.order != 0)
            {
                Debug.LogError("[SmokeProbe] FAIL-C: 排序恢复失败 (f1.order=" + f1.order + ")");
                return false;
            }
            Debug.Log("[SmokeProbe] C 手动排序 OK（交换+恢复落盘）");
            return true;
        }

        // ---------- D+E ----------
        private bool TestRoutePlayer(RoomManager roomManager)
        {
            RoutePlayer player = FindFirst<RoutePlayer>();
            if (player == null)
            {
                Debug.LogError("[SmokeProbe] FAIL-D: 缺少 RoutePlayer");
                return false;
            }

            if (!player.StartTour() || !player.IsTouring || player.RouteCount != 3)
            {
                Debug.LogError("[SmokeProbe] FAIL-D: StartTour 异常 (touring=" + player.IsTouring
                    + " count=" + player.RouteCount + ")");
                return false;
            }

            StardustBeacon beaconComponent = FindFirst<StardustBeacon>();
            GameObject beacon = beaconComponent != null ? beaconComponent.gameObject : null;
            if (beacon == null || !beacon.activeSelf)
            {
                Debug.LogError("[SmokeProbe] FAIL-D: 星尘信标未激活");
                return false;
            }
            GameObject firstStop = GameObject.Find("LP_frag_01");
            if (firstStop == null || Vector3.Distance(beacon.transform.position, firstStop.transform.position) > 0.05f)
            {
                Debug.LogError("[SmokeProbe] FAIL-D: 信标未落在第一站");
                return false;
            }
            Debug.Log("[SmokeProbe] D 走读开始 OK（信标→第 1 站）");

            if (!player.Advance() || player.Cursor != 1)
            {
                Debug.LogError("[SmokeProbe] FAIL-D: Advance 异常 (cursor=" + player.Cursor + ")");
                return false;
            }
            GameObject secondStop = GameObject.Find("LP_frag_02");
            if (secondStop == null || Vector3.Distance(beacon.transform.position, secondStop.transform.position) > 0.05f)
            {
                Debug.LogError("[SmokeProbe] FAIL-D: 信标未推进到第 2 站");
                return false;
            }
            Debug.Log("[SmokeProbe] D 走读推进 OK（信标→第 2 站）");

            // E. 回访复位
            AppData before = SaveDataService.LoadAppData();
            string visitedBefore = FindFragmentById(before, "frag_01").lastVisitedAt;
            if (!player.MarkVisited("frag_01"))
            {
                Debug.LogError("[SmokeProbe] FAIL-E: MarkVisited 返回 false");
                return false;
            }
            AppData after = SaveDataService.LoadAppData();
            string visitedAfter = FindFragmentById(after, "frag_01").lastVisitedAt;
            GameObject freshGo = GameObject.Find("LP_frag_01");
            LightPointAnchor freshAnchor = freshGo != null ? freshGo.GetComponent<LightPointAnchor>() : null;
            if (visitedAfter == visitedBefore || freshAnchor == null || freshAnchor.CurrentFade < 0.99f)
            {
                Debug.LogError("[SmokeProbe] FAIL-E: 回访未更新/未复位 (before=" + visitedBefore
                    + " after=" + visitedAfter + " fade=" + (freshAnchor != null ? freshAnchor.CurrentFade : -1f) + ")");
                return false;
            }
            Debug.Log("[SmokeProbe] E 回访复位 OK（lastVisitedAt 已更新，褪色=满亮）");
            return true;
        }

        // ---------- F ----------
        private IEnumerator TestRoomSwitch(RoomManager roomManager)
        {
            sectionPass = true;
            PanoramaDome dome = FindFirst<PanoramaDome>();
            Texture2D texBefore = dome != null
                ? dome.GetComponent<MeshRenderer>().sharedMaterial.mainTexture as Texture2D
                : null;

            if (!roomManager.SwitchToRoom(1) || roomManager.CurrentRoomIndex != 1
                || roomManager.SpawnedAnchorCount != 2)
            {
                Debug.LogError("[SmokeProbe] FAIL-F: 切房间异常 (idx=" + roomManager.CurrentRoomIndex
                    + " count=" + roomManager.SpawnedAnchorCount + ")");
                sectionPass = false;
                yield break;
            }

            // 等桥接懒接线新光点（1 帧足够，给 2s 预算）
            float deadline = Time.realtimeSinceStartup + 2f;
            GameObject frag04 = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                frag04 = GameObject.Find("LP_frag_04");
                if (frag04 != null) break;
                yield return null;
            }
            Texture2D texAfter = dome != null
                ? dome.GetComponent<MeshRenderer>().sharedMaterial.mainTexture as Texture2D
                : null;
            if (frag04 == null || texAfter == null || ReferenceEquals(texBefore, texAfter))
            {
                Debug.LogError("[SmokeProbe] FAIL-F: 新房间光点/穹顶未切换 (frag04=" + (frag04 == null) + ")");
                sectionPass = false;
                yield break;
            }

            RoutePlayer player = FindFirst<RoutePlayer>();
            if (player != null && player.IsTouring)
            {
                Debug.LogError("[SmokeProbe] FAIL-F: 切房间后走读未终止");
                sectionPass = false;
                yield break;
            }

            if (!roomManager.SwitchToRoom(0) || roomManager.SpawnedAnchorCount != 3)
            {
                Debug.LogError("[SmokeProbe] FAIL-F: 切回房间异常");
                sectionPass = false;
                yield break;
            }
            Debug.Log("[SmokeProbe] F 房间切换 OK（穹顶纹理已换，光点 3↔2，走读自动终止）");
        }

        // ---------- G ----------
        private IEnumerator TestReplaySequencer(LightPointAnchor fresh)
        {
            sectionPass = true;
            ReplaySequencer sequencer = FindFirst<ReplaySequencer>();
            if (sequencer == null)
            {
                Debug.LogError("[SmokeProbe] FAIL-G: 缺少 ReplaySequencer");
                sectionPass = false;
                yield break;
            }

            // 批处理快速拍（beat1 留 0.3s 保证能观察到 Beat2 窗口）
            sequencer.beatDurations = new float[] { 0.3f, 1.0f, 0.2f, 0.1f };
            Color ambientBefore = RenderSettings.ambientLight;

            AppData data = SaveDataService.LoadAppData();
            MemoryFragment frag = FindFragmentById(data, "frag_01");
            Vector3 anchorPos = fresh != null ? fresh.transform.position : Vector3.zero;
            sequencer.Play(frag, anchorPos);

            float deadline = Time.realtimeSinceStartup + 5f;
            bool sawBloom = false;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (sequencer.State == ReplaySequencer.ReplayState.Beat2_Bloom)
                {
                    sawBloom = true;
                    break;
                }
                if (sequencer.State == ReplaySequencer.ReplayState.Done) break;
                yield return null;
            }

            if (!sawBloom)
            {
                Debug.LogError("[SmokeProbe] FAIL-G: 未观察到回忆绽放拍 (state=" + sequencer.State + ")");
                sectionPass = false;
                yield break;
            }
            LayeredPhoto[] layers = FindAll<LayeredPhoto>();
            if (layers.Length < 1)
            {
                Debug.LogError("[SmokeProbe] FAIL-G: 绽放拍无分层场景");
                sectionPass = false;
                yield break;
            }

            sequencer.SkipToEnd();
            deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline && sequencer.State != ReplaySequencer.ReplayState.Done)
            {
                yield return null;
            }

            LayeredPhoto[] layersAfter = FindAll<LayeredPhoto>();
            if (sequencer.State != ReplaySequencer.ReplayState.Done
                || layersAfter.Length > 0
                || RenderSettings.ambientLight != ambientBefore)
            {
                Debug.LogError("[SmokeProbe] FAIL-G: 跳过未复原 (state=" + sequencer.State
                    + " layers=" + layersAfter.Length + ")");
                sectionPass = false;
                yield break;
            }
            Debug.Log("[SmokeProbe] G 回忆重演 OK（绽放→跳过→复原）");
        }

        // ---------- 工具 ----------
        private static T FindFirst<T>() where T : UnityEngine.Object
        {
            T[] found = UnityEngine.Object.FindObjectsByType<T>(UnityEngine.FindObjectsSortMode.None);
            return found.Length > 0 ? found[0] : null;
        }

        private static T[] FindAll<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsByType<T>(UnityEngine.FindObjectsSortMode.None);
        }

        private static LightPointAnchor FindAnchor(string objectName)
        {
            GameObject go = GameObject.Find(objectName);
            if (go == null) return null;
            return go.GetComponent<LightPointAnchor>();
        }

        private static MemoryFragment FindFragmentById(AppData data, string id)
        {
            if (data == null || data.rooms == null) return null;
            for (int r = 0; r < data.rooms.Count; r++)
            {
                if (data.rooms[r].fragments == null) continue;
                for (int f = 0; f < data.rooms[r].fragments.Count; f++)
                {
                    if (data.rooms[r].fragments[f].id == id) return data.rooms[r].fragments[f];
                }
            }
            return null;
        }

        private static string FormatWebError(System.Exception e)
        {
            string detail = e.Message;
            System.Net.WebException webEx = e as System.Net.WebException;
            if (webEx != null && webEx.Response != null)
            {
                using (System.IO.StreamReader reader = new System.IO.StreamReader(webEx.Response.GetResponseStream()))
                {
                    detail = detail + " | body: " + reader.ReadToEnd();
                }
            }
            return detail;
        }

        /// <summary>ImportServer 三连测：GET 首页、POST 上传落盘、POST 日记写档。失败抛异常。</summary>
        private static void TestImportServer()
        {
            const string baseUrl = "http://127.0.0.1:8899";

            System.Net.HttpWebResponse home = Request("GET", baseUrl + "/", null, null);
            if (home.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new System.Exception("首页状态码 " + (int)home.StatusCode);
            }
            home.Close();

            byte[] fakeJpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
            System.Net.HttpWebResponse up = Request("POST",
                baseUrl + "/upload?fragmentId=frag_01&name=smoke_test.jpg",
                "application/octet-stream", fakeJpeg);
            if (up.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new System.Exception("上传状态码 " + (int)up.StatusCode);
            }
            up.Close();
            string saved = System.IO.Path.Combine(SaveDataService.PersistentRoot,
                System.IO.Path.Combine("Media", System.IO.Path.Combine("frag_01", "smoke_test.jpg")));
            if (!System.IO.File.Exists(saved))
            {
                throw new System.Exception("上传文件未落盘: " + saved);
            }

            byte[] diaryBody = System.Text.Encoding.UTF8.GetBytes(
                "{\"fragmentId\":\"frag_01\",\"date\":\"2026-11-12\",\"text\":\"冒烟日记\"}");
            System.Net.HttpWebResponse diary = Request("POST", baseUrl + "/diary",
                "application/json", diaryBody);
            if (diary.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new System.Exception("日记状态码 " + (int)diary.StatusCode);
            }
            diary.Close();

            AppData reloaded = SaveDataService.LoadAppData();
            MemoryFragment frag01 = FindFragmentById(reloaded, "frag_01");
            if (frag01 == null || frag01.diary == null || frag01.diary.text != "冒烟日记")
            {
                throw new System.Exception("日记未写回 appdata.json");
            }
        }

        private static System.Net.HttpWebResponse Request(string method, string url, string contentType, byte[] body)
        {
            System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
            req.Method = method;
            if (body != null)
            {
                req.ContentType = contentType;
                req.ContentLength = body.Length;
                using (System.IO.Stream stream = req.GetRequestStream())
                {
                    stream.Write(body, 0, body.Length);
                }
            }
            return (System.Net.HttpWebResponse)req.GetResponse();
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(60f);
            Debug.LogError("[SmokeProbe] WATCHDOG: 60s 超时强制退出(1)");
            UnityEditor.EditorApplication.Exit(1);
        }
    }
}
