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

            if (roomManager == null || roomManager.CurrentRoomIndex != 0 || roomManager.SpawnedAnchorCount != 4)
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
                Debug.Log("[SmokeProbe] A 房间初始态 OK（room0×4 光点，穹顶已加载）");
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

            // ========== H. 星河回顾墙 ==========
            if (pass)
            {
                yield return TestConstellationWall(roomManager);
                pass = sectionPass;
                if (!pass) failedSections++;
            }

            // ========== H2. 房间二星河墙（日记星无缩略图） ==========
            if (pass)
            {
                yield return TestConstellationWallRoom2(roomManager);
                pass = sectionPass;
                if (!pass) failedSections++;
            }

            // ========== I. 转场淡入淡出 ==========
            if (pass)
            {
                yield return TestSceneFader(roomManager);
                pass = sectionPass;
                if (!pass) failedSections++;
            }

            // ========== J. 传送装配 ==========
            if (pass)
            {
                pass = TestTeleportAssembly();
                if (!pass) failedSections++;
            }

            // ========== L. UGUI 磨砂面板 ==========
            if (pass)
            {
                yield return TestUguiPanel();
                pass = sectionPass;
                if (!pass) failedSections++;
            }

            // ========== M. 星尘粒子场 ==========
            if (pass)
            {
                yield return TestStardustField();
                pass = sectionPass;
                if (!pass) failedSections++;
            }

            // ========== K. ImportServer HTTP ==========
            if (pass)
            {
                yield return new WaitForSecondsRealtime(0.6f); // 等监听线程
                try
                {
                    TestImportServer();
                    Debug.Log("[SmokeProbe] K ImportServer HTTP 冒烟 OK（GET/POST 上传落盘/POST 日记入档）");
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[SmokeProbe] FAIL-K: ImportServer 异常 " + FormatWebError(e));
                    pass = false; failedSections++;
                }
            }

            Debug.Log(pass
                ? "[SmokeProbe] PASS: 房间/XRI/悬停/选中/褪色/排序/走读/回访/切换/重演/星河墙×2/转场/传送/面板/星尘/导入 全部通过"
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

            if (!player.StartTour() || !player.IsTouring || player.RouteCount != 4)
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
                || roomManager.SpawnedAnchorCount != 4)
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

            if (!roomManager.SwitchToRoom(0) || roomManager.SpawnedAnchorCount != 4)
            {
                Debug.LogError("[SmokeProbe] FAIL-F: 切回房间异常");
                sectionPass = false;
                yield break;
            }
            Debug.Log("[SmokeProbe] F 房间切换 OK（穹顶纹理已换，光点 4↔4，走读自动终止）");
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

        // ---------- H ----------
        private IEnumerator TestConstellationWall(RoomManager roomManager)
        {
            sectionPass = true;
            ConstellationWall wall = FindFirst<ConstellationWall>();
            RoutePlayer player = FindFirst<RoutePlayer>();
            if (wall == null || player == null)
            {
                Debug.LogError("[SmokeProbe] FAIL-H: 缺星河墙或走读器 (wall=" + (wall == null) + ")");
                sectionPass = false;
                yield break;
            }

            if (!player.StartTour())
            {
                Debug.LogError("[SmokeProbe] FAIL-H: 走读启动失败");
                sectionPass = false;
                yield break;
            }

            int guard = 0;
            while (player.IsTouring && guard++ < 10)
            {
                player.Advance(); // 最后一站返回 false → EndTour → 建墙
            }

            float deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline && !wall.IsVisible)
            {
                yield return null;
            }
            if (!wall.IsVisible || wall.StarCount != 4)
            {
                Debug.LogError("[SmokeProbe] FAIL-H: 星河墙未生成 (visible=" + wall.IsVisible
                    + " stars=" + wall.StarCount + ")");
                sectionPass = false;
                yield break;
            }

            // 坑-021 变体：FindObjects 顺序不保证，按稳定命名找路线首星
            GameObject firstStarGo = GameObject.Find("Star_frag_01");
            StarNode firstStar = firstStarGo != null ? firstStarGo.GetComponent<StarNode>() : null;
            if (firstStar == null)
            {
                Debug.LogError("[SmokeProbe] FAIL-H: 无星点");
                sectionPass = false;
                yield break;
            }
            bool starFired = false;
            string starId = "";
            System.Action<string> handler = delegate (string id) { starFired = true; starId = id; };
            firstStar.OnSelected += handler;
            firstStar.Select();
            firstStar.OnSelected -= handler;
            if (!starFired || starId != "frag_01")
            {
                Debug.LogError("[SmokeProbe] FAIL-H: 星点选择回调异常 (fired=" + starFired + " id=" + starId + ")");
                sectionPass = false;
                yield break;
            }

            // 缩略图断言：room_01 四星全带卡片（全为 image/图+音/mixed 类型）
            string[] room1Stars = { "Star_frag_01", "Star_frag_02", "Star_frag_03", "Star_frag_06" };
            for (int i = 0; i < room1Stars.Length; i++)
            {
                GameObject starGo = GameObject.Find(room1Stars[i]);
                Transform thumb = starGo != null ? starGo.transform.Find("Thumb") : null;
                bool hasThumb = thumb != null && thumb.GetComponent<MeshRenderer>() != null
                    && thumb.GetComponent<MeshRenderer>().material.mainTexture != null;
                if (!hasThumb)
                {
                    Debug.LogError("[SmokeProbe] FAIL-H: " + room1Stars[i] + " 缩略图缺失");
                    sectionPass = false;
                    yield break;
                }
            }
            Debug.Log("[SmokeProbe] H 星河回顾墙 OK（4 星连线+首星回调+四缩略图）");
        }

        // ---------- H2 ----------
        private IEnumerator TestConstellationWallRoom2(RoomManager roomManager)
        {
            sectionPass = true;
            ConstellationWall wall = FindFirst<ConstellationWall>();
            RoutePlayer player = FindFirst<RoutePlayer>();
            if (wall == null || player == null)
            {
                Debug.LogError("[SmokeProbe] FAIL-H2: 缺星河墙或走读器");
                sectionPass = false;
                yield break;
            }

            // 切房间二：日记星（frag_05）应无缩略图，图星（frag_07 褪色样例）应有
            if (!roomManager.SwitchToRoom(1))
            {
                Debug.LogError("[SmokeProbe] FAIL-H2: 切房间失败");
                sectionPass = false;
                yield break;
            }
            if (!player.StartTour())
            {
                Debug.LogError("[SmokeProbe] FAIL-H2: 房间二走读启动失败");
                sectionPass = false;
                yield break;
            }
            int guard2 = 0;
            while (player.IsTouring && guard2++ < 10)
            {
                player.Advance();
            }
            float deadline2 = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline2 && !wall.IsVisible)
            {
                yield return null;
            }
            if (!wall.IsVisible || wall.StarCount != 4)
            {
                Debug.LogError("[SmokeProbe] FAIL-H2: 房间二星河墙异常 (visible=" + wall.IsVisible
                    + " stars=" + wall.StarCount + ")");
                sectionPass = false;
                yield break;
            }

            GameObject diaryStar = GameObject.Find("Star_frag_05");
            Transform diaryThumb = diaryStar != null ? diaryStar.transform.Find("Thumb") : null;
            GameObject photoStar = GameObject.Find("Star_frag_07");
            Transform photoThumb = photoStar != null ? photoStar.transform.Find("Thumb") : null;
            bool photoThumbOk = photoThumb != null && photoThumb.GetComponent<MeshRenderer>() != null
                && photoThumb.GetComponent<MeshRenderer>().material.mainTexture != null;
            if (diaryThumb != null || !photoThumbOk)
            {
                Debug.LogError("[SmokeProbe] FAIL-H2: 缩略图分类异常 (日记星误加=" + (diaryThumb != null)
                    + " 图星缺失=" + !photoThumbOk + ")");
                sectionPass = false;
                yield break;
            }

            if (!roomManager.SwitchToRoom(0))
            {
                Debug.LogError("[SmokeProbe] FAIL-H2: 切回失败");
                sectionPass = false;
                yield break;
            }
            Debug.Log("[SmokeProbe] H2 房间二星河墙 OK（日记星纯光球/图星带卡+切回）");
        }

        // ---------- I ----------
        private IEnumerator TestSceneFader(RoomManager roomManager)
        {
            sectionPass = true;
            SceneFader fader = FindFirst<SceneFader>();
            if (fader == null)
            {
                Debug.LogError("[SmokeProbe] FAIL-I: 缺 SceneFader");
                sectionPass = false;
                yield break;
            }

            yield return fader.FadeOut(0.2f);
            if (fader.Alpha < 0.99f)
            {
                Debug.LogError("[SmokeProbe] FAIL-I: 淡出未达全黑 (alpha=" + fader.Alpha.ToString("F2") + ")");
                sectionPass = false;
                yield break;
            }
            yield return fader.FadeIn(0.2f);
            if (fader.Alpha > 0.01f)
            {
                Debug.LogError("[SmokeProbe] FAIL-I: 淡入未复原 (alpha=" + fader.Alpha.ToString("F2") + ")");
                sectionPass = false;
                yield break;
            }

            yield return roomManager.SwitchToRoomWithFade(1);
            if (roomManager.CurrentRoomIndex != 1 || roomManager.SpawnedAnchorCount != 4)
            {
                Debug.LogError("[SmokeProbe] FAIL-I: 带淡切房间异常 (idx=" + roomManager.CurrentRoomIndex + ")");
                sectionPass = false;
                yield break;
            }
            yield return roomManager.SwitchToRoomWithFade(0);
            if (roomManager.CurrentRoomIndex != 0 || roomManager.SpawnedAnchorCount != 4)
            {
                Debug.LogError("[SmokeProbe] FAIL-I: 带淡切返回异常");
                sectionPass = false;
                yield break;
            }
            Debug.Log("[SmokeProbe] I 转场淡入淡出 OK（黑幕 0→1→0，带淡切房间往返）");
        }

        // ---------- J ----------
        private bool TestTeleportAssembly()
        {
            UnityEngine.XR.Interaction.Toolkit.TeleportationProvider[] providers =
                FindAll<UnityEngine.XR.Interaction.Toolkit.TeleportationProvider>();
            UnityEngine.XR.Interaction.Toolkit.TeleportationArea[] areas =
                UnityEngine.Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.TeleportationArea>(
                    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
            if (providers.Length < 1 || areas.Length < 1)
            {
                Debug.LogError("[SmokeProbe] FAIL-J: 传送装配缺失 (providers=" + providers.Length
                    + " areas=" + areas.Length + ")");
                return false;
            }
            Debug.Log("[SmokeProbe] J 传送装配 OK（TeleportationProvider + 地面 TeleportationArea）");
            return true;
        }

        // ---------- L ----------
        private IEnumerator TestUguiPanel()
        {
            sectionPass = true;
            MemoryPanel panel = FindFirst<MemoryPanel>();
            if (panel == null)
            {
                Debug.LogError("[SmokeProbe] FAIL-L: 缺 MemoryPanel");
                sectionPass = false;
                yield break;
            }

            AppData data = SaveDataService.LoadAppData();
            MemoryFragment frag = FindFragmentById(data, "frag_01");
            if (frag == null)
            {
                Debug.LogError("[SmokeProbe] FAIL-L: frag_01 缺失");
                sectionPass = false;
                yield break;
            }

            panel.Show(frag);
            yield return null; // 等 Canvas 构建/激活
            if (!panel.IsShowing || panel.PhotoTexture == null || panel.TitleString != "缝纫机")
            {
                Debug.LogError("[SmokeProbe] FAIL-L: 面板显示异常 (showing=" + panel.IsShowing
                    + " photo=" + (panel.PhotoTexture != null) + " title=" + panel.TitleString + ")");
                sectionPass = false;
                yield break;
            }
            panel.Hide();
            yield return null;
            if (panel.IsShowing)
            {
                Debug.LogError("[SmokeProbe] FAIL-L: Hide 后仍显示");
                sectionPass = false;
                yield break;
            }
            Debug.Log("[SmokeProbe] L UGUI 磨砂面板 OK（照片+标题+显隐）");
        }

        // ---------- M ----------
        private IEnumerator TestStardustField()
        {
            sectionPass = true;
            StardustField dust = FindFirst<StardustField>();
            if (dust == null)
            {
                Debug.LogError("[SmokeProbe] FAIL-M: 缺 StardustField");
                sectionPass = false;
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.5f);
            if (!dust.IsEmitting || dust.MaxParticles != 200)
            {
                Debug.LogError("[SmokeProbe] FAIL-M: 星尘未发射 (emitting=" + (dust != null && dust.IsEmitting)
                    + " max=" + (dust != null ? dust.MaxParticles : -1) + ")");
                sectionPass = false;
                yield break;
            }
            Debug.Log("[SmokeProbe] M 星尘粒子场 OK（200 上限，暖琥珀，跟随相机）");
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
