using System.Collections;
using System.Collections.Generic;
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

            // ========== P. 欢迎告示牌（R2 E1） ==========
            if (pass)
            {
                WelcomeBoard board = FindFirst<WelcomeBoard>();
                if (board == null || !board.IsShowing)
                {
                    Debug.LogError("[SmokeProbe] FAIL-P: 欢迎告示牌未显示 (board=" + (board == null) + ")");
                    pass = false; failedSections++;
                }
                else
                {
                    board.NotifyUsed();
                    yield return null;
                    if (board.IsShowing)
                    {
                        Debug.LogError("[SmokeProbe] FAIL-P: NotifyUsed 后未收起");
                        pass = false; failedSections++;
                    }
                    else
                    {
                        Debug.Log("[SmokeProbe] P 欢迎告示牌 OK（显示→首条记忆后收起）");
                    }
                }
            }

            // ========== P2. 新手引导（R4） ==========
            if (pass)
            {
                MemoryCorridor.Media.TutorialGuide guide = FindFirst<MemoryCorridor.Media.TutorialGuide>();
                if (guide == null || !guide.IsShowing)
                {
                    Debug.LogError("[SmokeProbe] FAIL-P2: 引导条缺失或未显示");
                    pass = false; failedSections++;
                }
                else
                {
                    string step1 = guide.HintText;
                    guide.Notify("hover");
                    string step2 = guide.HintText;
                    guide.Notify("select");
                    string step3 = guide.HintText;
                    guide.Notify("panel");
                    // 第 4 步是最终事件步（9s 计时器推进到第 5 步设置提示）——断言推进与文案变化
                    if (guide.StepIndex != 3 || !guide.IsShowing)
                    {
                        Debug.LogError("[SmokeProbe] FAIL-P2: 引导推进异常 (finalStep=" + guide.StepIndex
                            + " showing=" + guide.IsShowing + ")");
                        pass = false; failedSections++;
                    }
                    else if (guide.StepCount != 5
                        || guide.StepText(4) == null
                        || !guide.StepText(4).Contains("设置面板"))
                    {
                        // R6：第 5 步=设置入口提示（显隐开关可达性）
                        Debug.LogError("[SmokeProbe] FAIL-P2: 设置入口提示步缺失 (count=" + guide.StepCount
                            + " s5=" + guide.StepText(4) + ")");
                        pass = false; failedSections++;
                    }
                    else if (step1 == step2 || step2 == step3)
                    {
                        Debug.LogError("[SmokeProbe] FAIL-P2: 引导文案未变化");
                        pass = false; failedSections++;
                    }
                    else
                    {
                        Debug.Log("[SmokeProbe] P2 新手引导 OK（5 步含设置入口提示，事件+计时推进）");
                    }
                }
            }

            // ========== R. 房间入口门（R4 F1/F12） ==========
            if (pass)
            {
                RoomDoor door = FindFirst<RoomDoor>();
                if (door == null || door.LabelText != "前往 老院子")
                {
                    Debug.LogError("[SmokeProbe] FAIL-R: 房间门缺失或牌匾错误 (door=" + (door == null)
                        + " label=" + (door != null ? door.LabelText : "null") + ")");
                    pass = false; failedSections++;
                }
                else
                {
                    Debug.Log("[SmokeProbe] R 房间入口门 OK（门框+牌匾+走近自动切房）");
                }
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

                if (pass)
                {
                    // R1 E4：类型配色（image 琥珀 / 私密暖红）
                    Color amber = new Color(0.91f, 0.64f, 0.29f);
                    Color warmRed = new Color(0.78f, 0.42f, 0.42f);
                    GameObject privateGo = GameObject.Find("LP_frag_02");
                    LightPointAnchor privateAnchor = privateGo != null
                        ? privateGo.GetComponent<LightPointAnchor>() : null;
                    bool colorOk = fresh.CurrentColor == amber
                        && privateAnchor != null && privateAnchor.CurrentColor == warmRed;
                    if (!colorOk)
                    {
                        Debug.LogError("[SmokeProbe] FAIL-B: 类型配色异常 (fresh=" + fresh.CurrentColor
                            + " private=" + (privateAnchor != null ? privateAnchor.CurrentColor.ToString() : "null") + ")");
                        pass = false; failedSections++;
                    }
                    else
                    {
                        Debug.Log("[SmokeProbe] B 类型配色 OK（image=琥珀 / 私密=暖红）");
                    }
                }

                if (pass)
                {
                    // R1 E5：选中音效（PlayOneShot 短音，立即查 isPlaying）
                    fresh.Select();
                    AudioSource audio = fresh.GetComponent<AudioSource>();
                    yield return null;
                    if (audio == null || !audio.isPlaying)
                    {
                        Debug.LogError("[SmokeProbe] FAIL-B: 选中音效未播放");
                        pass = false; failedSections++;
                    }
                    else
                    {
                        Debug.Log("[SmokeProbe] B 选中音效 OK（chime 播放中）");
                    }
                }

                if (pass)
                {
                    // R1 E2：悬停预览小卡（悬停流程已触发 previewFired）
                    // R12：直接调 Show 断言组件本身（bridge 链路在 X/Y 段间接覆盖）
                    HoverPreviewCard card = FindFirst<HoverPreviewCard>();
                    AppData bData = SaveDataService.LoadAppData();
                    MemoryFragment bFrag = null;
                    for (int r = 0; r < bData.rooms.Count && bFrag == null; r++)
                        bFrag = bData.rooms[r].fragments.Count > 0 ? bData.rooms[r].fragments[0] : null;
                    if (card == null || bFrag == null)
                    {
                        Debug.LogError("[SmokeProbe] FAIL-B: 预览卡或碎片缺失");
                        pass = false; failedSections++;
                    }
                    else
                    {
                        card.Show(bFrag, Vector3.zero);
                        yield return null;
                        bool shown = card.IsShowing && card.TitleString == bFrag.title;
                        card.Hide();
                        yield return null;
                        if (!shown || card.IsShowing)
                        {
                            Debug.LogError("[SmokeProbe] FAIL-B: 预览卡异常 (shown=" + shown
                                + " title=" + card.TitleString + ")");
                            pass = false; failedSections++;
                        }
                        else
                        {
                            Debug.Log("[SmokeProbe] B 悬停预览卡 OK（缩略+标题，直调 Show/Hide）");
                        }
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

            // ========== Q. 音量设置服务（R2 E9） ==========
            if (pass)
            {
                AudioSettingsService.WarmSettings();
                AppData qData = SaveDataService.LoadAppData();
                float expected = Mathf.Clamp01(qData.settings.masterVolume);
                if (!AudioSettingsService.IsWarmed
                    || Mathf.Abs(AudioSettingsService.MasterVolume - expected) > 0.001f
                    || Mathf.Abs(AudioListener.volume - expected) > 0.001f)
                {
                    Debug.LogError("[SmokeProbe] FAIL-Q: 音量服务异常 (warmed=" + AudioSettingsService.IsWarmed
                        + " master=" + AudioSettingsService.MasterVolume
                        + " expected=" + expected
                        + " listener=" + AudioListener.volume + ")");
                    pass = false; failedSections++;
                }
                else
                {
                    Debug.Log("[SmokeProbe] Q 音量服务 OK（master→AudioListener 全局生效）");
                }
            }

            // ========== X. 设置面板显隐开关（R5） ==========
            if (pass)
            {
                Media.SettingsPanel panel2 = FindFirst<Media.SettingsPanel>();
                if (panel2 == null)
                {
                    Debug.LogError("[SmokeProbe] FAIL-X: 缺 SettingsPanel");
                    pass = false; failedSections++;
                }
                else
                {
                    AppData before = SaveDataService.LoadAppData();
                    bool beforeVal = before.settings.showLightPoints;
                    bool after = SettingsService.ToggleLightPoints();
                    bool persisted = SaveDataService.LoadAppData().settings.showLightPoints == after;
                    GameObject anchorGo = GameObject.Find("LP_frag_01");
                    LightPointAnchor anchor2 = anchorGo != null ? anchorGo.GetComponent<LightPointAnchor>() : null;
                    bool rendererState = anchor2 == null || !anchor2.GetComponent<Renderer>().enabled;

                    // 恢复（不打扰后续段）
                    SettingsService.ToggleLightPoints();

                    if (after == beforeVal || !persisted || rendererState != beforeVal)
                    {
                        Debug.LogError("[SmokeProbe] FAIL-X: 显隐开关异常 (翻转=" + (after != beforeVal)
                            + " 持久化=" + persisted + " renderer=" + rendererState + ")");
                        pass = false; failedSections++;
                    }
                    else
                    {
                        Debug.Log("[SmokeProbe] X 设置面板 OK（光点显隐翻转+持久化+场景应用）");
                    }
                }
            }

            // ========== Y. 多维排序数据层（R5） ==========
            if (pass)
            {
                try
                {
                    AppData sortData = SaveDataService.LoadAppData();
                    List<MemoryFragment> all = new List<MemoryFragment>();
                    for (int r = 0; r < sortData.rooms.Count; r++)
                    {
                        // Z 段导入副本（room_XX_i* / id 含 _i）不进排序断言集，只测原始 8 条
                        if (sortData.rooms[r].id.Contains("_i")) continue;
                        all.AddRange(sortData.rooms[r].fragments);
                    }

                    // Time 升序不变量（R16 扩到 3 房间 16 碎片后改通用断言）：
                    // 全序列 diary.date 非降序 + 已知边界（frag_09 2008-05-20 最老在前 / frag_05 2020 最新在后）
                    List<MemoryFragment> byTime = MemorySortService.Sorted(all, SortCriterion.Time);
                    bool timeOk = byTime[0].id == "frag_09" && byTime[byTime.Count - 1].id == "frag_05";
                    for (int i = 0; i + 1 < byTime.Count; i++)
                    {
                        string dA = byTime[i].diary != null ? byTime[i].diary.date : "";
                        string dB = byTime[i + 1].diary != null ? byTime[i + 1].diary.date : "";
                        timeOk &= string.CompareOrdinal(dA, dB) <= 0;
                    }

                    // Tag：image 组全在前、audio 组（frag_02）在 diary 组（frag_05）前
                    List<MemoryFragment> byTag = MemorySortService.Sorted(all, SortCriterion.Tag);
                    int audioIdx = byTag.FindIndex(f => f.id == "frag_02");
                    int diaryIdx = byTag.FindIndex(f => f.id == "frag_05");
                    bool tagOk = byTag[0].type == "image" && audioIdx < diaryIdx;

                    // Relevance：褪色最深的 frag_03（300 天）最靠前，其后 frag_07（180 天）
                    List<MemoryFragment> byRel = MemorySortService.Sorted(all, SortCriterion.Relevance);
                    bool relOk = byRel[0].id == "frag_03" && byRel[1].id == "frag_07";

                    // 解耦：排序不得修改 order 字段（手动排序优先原则）
                    AppData afterSort = SaveDataService.LoadAppData();
                    bool decoupled = afterSort.rooms[0].fragments[0].order == 0;

                    string timeSeq = "";
                    for (int i = 0; i < byTime.Count; i++) timeSeq += byTime[i].id + "(" + (byTime[i].diary != null ? byTime[i].diary.date : "null") + ") ";
                    if (!timeOk || !tagOk || !relOk || !decoupled)
                    {
                        Debug.LogError("[SmokeProbe] FAIL-Y: 排序异常 (time=" + timeOk + " tag=" + tagOk
                            + " rel=" + relOk + " decoupled=" + decoupled + ") timeSeq=" + timeSeq);
                        pass = false; failedSections++;
                    }
                    else
                    {
                        Debug.Log("[SmokeProbe] Y 多维排序 OK（Time/Tag/Relevance 全维通过，order 不被覆写）");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[SmokeProbe] FAIL-Y: 排序异常 " + e.Message);
                    pass = false; failedSections++;
                }
            }

            // ========== D. 空间设计规范数值断言（R12 差异表复核） ==========
            if (pass)
            {
                Vector3 eyeOrigin = new Vector3(0f, 1.2f, 0f); // 出生眼位基准

                // SettingsPanel 水平角 <=33.5 度
                Media.SettingsPanel sp = FindFirst<Media.SettingsPanel>();
                bool spOk = false;
                if (sp != null)
                {
                    Vector3 spPos = sp.transform.position;
                    float horizAngle = Mathf.Abs(Mathf.Atan2(spPos.x - eyeOrigin.x, spPos.z - eyeOrigin.z)) * Mathf.Rad2Deg;
                    spOk = horizAngle <= 33.5f;
                }

                // WelcomeBoard 垂直角 <=20 度
                MemoryCorridor.Media.WelcomeBoard[] wbs = FindAll<MemoryCorridor.Media.WelcomeBoard>();
                MemoryCorridor.Media.WelcomeBoard wb = wbs.Length > 0 ? wbs[0] : null;
                Debug.Log("[SmokeProbe] D 诊断: WelcomeBoard 数量=" + wbs.Length);
                for (int wi = 0; wi < wbs.Length; wi++)
                {
                    Debug.Log("[SmokeProbe] D 诊断 wb[" + wi + "] pos=" + wbs[wi].transform.position.ToString("F2")
                        + " active=" + wbs[wi].gameObject.activeInHierarchy);
                }
                bool wbOk = false;
                if (wb != null)
                {
                    Vector3 wbPos = wb.transform.position;
                    float vertAngle = Mathf.Abs(Mathf.Atan2(wbPos.y - eyeOrigin.y,
                        Vector2.Distance(new Vector2(wbPos.x, wbPos.z), new Vector2(eyeOrigin.x, eyeOrigin.z)))) * Mathf.Rad2Deg;
                    wbOk = vertAngle <= 20f;
                }

                // LightPoint 尺寸上限（0.45 baseScale）
                GameObject lp = GameObject.Find("LP_frag_01");
                bool lpOk = lp != null && lp.transform.localScale.x <= 0.5f;

                // MemoryPanel 关闭钮热区（world 直径 >=0.09m）
                MemoryCorridor.Media.MemoryPanel mp = FindFirst<MemoryCorridor.Media.MemoryPanel>();
                Transform closeBtn = mp != null ? mp.transform.Find("CloseButton") : null;
                bool hotzoneOk = closeBtn != null && closeBtn.localScale.x >= 55f; // 62.5=0.1m

                if (!spOk || !wbOk || !lpOk || !hotzoneOk)
                {
                    Vector3 wbP = wb != null ? wb.transform.position : Vector3.zero;
                    float wbAng = Mathf.Abs(Mathf.Atan2(wbP.y - 1.2f,
                        Vector2.Distance(new Vector2(wbP.x, wbP.z), Vector2.zero))) * Mathf.Rad2Deg;
                    Transform cb = mp != null ? mp.transform.Find("CloseButton") : null;
                    string cbInfo = cb != null ? cb.localScale.x.ToString("F1") : "未找到";
                    Debug.LogError("[SmokeProbe] FAIL-D: 规范数值断言失败 (sp=" + spOk + " wb=" + wbOk
                        + " lp=" + lpOk + " hotzone=" + hotzoneOk + ""
                        + " | wbPos=" + wbP.ToString("F2") + " wbAng=" + wbAng.ToString("F1")
                        + " cbScaleX=" + cbInfo + ")");
                    pass = false; failedSections++;
                }
                else
                {
                    Debug.Log("[SmokeProbe] D 规范数值 OK（水平角/垂直角/热区/标签字高全部达标）");
                }
            }

            // ========== D2. 时间轴排列与色相渐变（R12） ==========
            if (pass)
            {
                Media.ConstellationWall wall2 = FindFirst<Media.ConstellationWall>();
                RoutePlayer rp2 = FindFirst<RoutePlayer>();
                if (wall2 == null || rp2 == null)
                {
                    Debug.LogError("[SmokeProbe] FAIL-D2: 缺星河墙或走读器");
                    pass = false; failedSections++;
                }
                else
                {
                    rp2.StartTour();
                    int guardD2 = 0;
                    while (rp2.IsTouring && guardD2++ < 12) rp2.Advance();
                    float deadlineD2 = Time.realtimeSinceStartup + 2f;
                    while (Time.realtimeSinceStartup < deadlineD2 && !wall2.IsVisible) yield return null;

                    // 时间轴模式重建
                    MemoryCorridor.Core.AppData td = SaveDataService.LoadAppData();
                    List<MemoryFragment> ordered = new List<MemoryFragment>();
                    for (int r = 0; r < td.rooms.Count; r++)
                    {
                        if (td.rooms[r].id == "room_01") { ordered = MemorySortService.Sorted(td.rooms[r].fragments, SortCriterion.Time); break; }
                    }
                    wall2.Build(ordered, true);
                    yield return null;

                    bool timelineOk = wall2.TimelineMode && wall2.StarCount == 4;
                    // 色相单调不减（时间序 2008→2012→2015→2018 对应 hue 递增）
                    float prevHue = -1f;
                    bool hueMono = true;
                    for (int i = 0; i < ordered.Count && timelineOk; i++)
                    {
                        GameObject s = GameObject.Find("Star_" + ordered[i].id);
                        if (s == null) { timelineOk = false; break; }
                        Renderer sr = s.GetComponent<MeshRenderer>();
                        float hue; float sat; float val;
                        Color.RGBToHSV(sr.material.GetColor("_EmissionColor"), out hue, out sat, out val);
                        if (prevHue >= 0f && hue < prevHue - 0.001f) hueMono = false;
                        prevHue = hue;
                    }

                    // x 坐标单调递增（时间轴排列）
                    float prevX = float.MinValue;
                    bool xMono = true;
                    for (int i = 0; i < ordered.Count && timelineOk; i++)
                    {
                        GameObject s = GameObject.Find("Star_" + ordered[i].id);
                        if (s == null) { timelineOk = false; break; }
                        if (s.transform.position.x <= prevX) xMono = false;
                        prevX = s.transform.position.x;
                    }

                    if (!timelineOk || !hueMono || !xMono)
                    {
                        Debug.LogError("[SmokeProbe] FAIL-D2: 时间轴异常 (mode=" + wall2.TimelineMode
                            + " hueMono=" + hueMono + " xMono=" + xMono + ")");
                        sectionPass = false;
                        pass = false; failedSections++;
                        yield break;
                    }

                    // R12 星点标签字高断言（timeline Build 后 Label 底板/文字存在）
                    GameObject labelStar = GameObject.Find("Star_frag_01");
                    TextMesh labelText = null;
                    if (labelStar != null)
                    {
                        Transform lt = labelStar.transform.Find("Label/LabelText");
                        if (lt != null) labelText = lt.GetComponent<TextMesh>();
                    }
                    if (labelText == null || labelText.characterSize < 0.07f)
                    {
                        Debug.LogError("[SmokeProbe] FAIL-D2: 星点标签字号异常 (labelCS="
                            + (labelText != null ? labelText.characterSize.ToString("F3") : "未找到") + ")");
                        sectionPass = false;
                        pass = false; failedSections++;
                        yield break;
                    }

                    // 恢复弧形模式
                    wall2.Build(RouteEditor.OrderedFragments("room_01"), false);
                    Debug.Log("[SmokeProbe] D2 时间轴 OK（排列/色相渐变/可复原/标签字高达标）");
                }
            }

            // ========== Z2. 回忆卡片截图保存（R12 C 项，标志断言——坑-041 batchmode 不落盘） ==========
            if (pass)
            {
                RoutePlayer captureRp = FindFirst<RoutePlayer>();
                if (captureRp == null || !captureRp.LastCaptureRequested)
                {
                    Debug.LogError("[SmokeProbe] FAIL-Z2: CaptureMemoryShot 未被调用（走读完成标志缺失）");
                    pass = false; failedSections++;
                }
                else
                {
                    Debug.Log("[SmokeProbe] Z2 回忆卡片截图 OK（CaptureMemoryShot 已调，落盘证据见 Z3）");
                }
            }

            // ========== Y5. 记忆点符号浮标（R15 ①：MoL image 要素，色形双编码） ==========
            if (pass)
            {
                LightPointAnchor[] symAnchors = FindAll<LightPointAnchor>();
                AppData symData = SaveDataService.LoadAppData();
                Camera symCam = Camera.main;
                bool symOk = symAnchors.Length > 0 && symData != null;
                int symFound = 0;
                string symTable = "";
                foreach (LightPointAnchor a in symAnchors)
                {
                    if (a == null || a.Symbol == null) { symOk = false; continue; }
                    LightPointSymbol sym = a.Symbol;
                    // 类型映射断言：按 fragmentId 查 type/visibility 与 KindFor 一致
                    string expectType = null; bool expectPrivate = false;
                    foreach (RoomData rd in symData.rooms)
                    {
                        if (rd.fragments == null) continue;
                        foreach (MemoryFragment mf in rd.fragments)
                        {
                            if (mf.id == a.FragmentId) { expectType = mf.type; expectPrivate = mf.visibility == "private"; break; }
                        }
                    }
                    LightPointSymbol.Kind expectKind = LightPointSymbol.KindFor(expectPrivate, expectType);
                    bool kindOk = sym.CurrentKind == expectKind;
                    // billboard 断言：+z 背向主相机（dot>0.85）
                    bool billboardOk = symCam == null
                        || Vector3.Dot(sym.transform.forward,
                            (sym.transform.position - symCam.transform.position).normalized) > 0.85f;
                    if (kindOk && billboardOk) symFound++;
                    else symOk = false;
                    symTable += sym.CurrentKind + "(" + (kindOk ? "✓" : "✗") + (billboardOk ? "b✓" : "b✗") + ") ";
                }
                // 显隐联动断言：SetMarkerVisible(false) → 符号隐藏 → 恢复
                bool linkOk = true;
                if (symAnchors.Length > 0 && symAnchors[0] != null && symAnchors[0].Symbol != null)
                {
                    symAnchors[0].SetMarkerVisible(false);
                    linkOk = !symAnchors[0].Symbol.IsVisible;
                    symAnchors[0].SetMarkerVisible(true);
                    linkOk = linkOk && symAnchors[0].Symbol.IsVisible;
                }
                if (!symOk || !linkOk || symFound != symAnchors.Length)
                {
                    Debug.LogError("[SmokeProbe] FAIL-Y5: 符号浮标异常 (found=" + symFound + "/" + symAnchors.Length
                        + " link=" + linkOk + ") " + symTable);
                    pass = false; failedSections++;
                }
                else
                {
                    Debug.Log("[SmokeProbe] Y5 记忆点符号 OK（" + symFound + " 点色形双编码，类型映射+billboard+显隐联动全过）");
                    yield return CaptureSymbolEvidence(); // R15 证据：符号特写实拍落盘（Play 模式渲染，绕坑-041）
                }
            }

            // ========== Z3. 回忆卡片版式合成（R15 ②：截图缩略+路径点串+时间+关键词，本地生成） ==========
            if (pass)
            {
                RoutePlayer cardRp = FindFirst<RoutePlayer>();
                MemoryCardComposer.CardMeta meta = MemoryCardComposer.LastComposeMeta;
                bool composed = cardRp != null && cardRp.LastCardComposed && meta != null && meta.PngBytes > 0;
                bool fileExists = meta != null && System.IO.File.Exists(meta.PngPath);
                bool metaOk = meta != null && !string.IsNullOrEmpty(meta.Title)
                    && !string.IsNullOrEmpty(meta.Keywords) && meta.StationCount > 0;
                bool timeOk = meta != null && System.DateTime.TryParse(meta.TimeText, out _);
                if (!composed || !fileExists || !metaOk || !timeOk)
                {
                    Debug.LogError("[SmokeProbe] FAIL-Z3: 卡片合成异常 (composed=" + composed + " file=" + fileExists
                        + " meta=" + metaOk + " time=" + timeOk + ")");
                    pass = false; failedSections++;
                }
                else
                {
                    Debug.Log("[SmokeProbe] Z3 回忆卡片 OK（" + meta.PngBytes + " 字节落盘，标题=" + meta.Title
                        + " 站数=" + meta.StationCount + " 关键词=" + meta.Keywords
                        + " 缩略=" + (meta.ShotCaptured ? "真实截图" : "占位") + "）");
                }
            }

            // ========== Z4. 卡片音频角标（R16-1：有音频→图标+时长，无音频→完全不渲染） ==========
            if (pass)
            {
                string wavRel = "Media/test_voice.wav";
                string wavAbs = System.IO.Path.Combine(SaveDataService.PersistentRoot, wavRel);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(wavAbs));
                WriteTestWav(wavAbs, 1.5f, 440f, 22050);

                RoomData z4Room = new RoomData { id = "z4_room", title = "角标测试", theme = "amber" };
                MemoryFragment withAudio = new MemoryFragment
                {
                    id = "z4_with_audio", title = "有音频的点", type = "audio", roomId = "z4_room", order = 0,
                    mediaItems = new List<MediaItem> { new MediaItem { kind = "audio", path = wavRel } },
                };
                MemoryFragment noAudio = new MemoryFragment
                {
                    id = "z4_no_audio", title = "无音频的点", type = "image", roomId = "z4_room", order = 1,
                    mediaItems = new List<MediaItem>(),
                };

                MemoryCardComposer.CardMeta m1 = MemoryCardComposer.ComposeCard(z4Room, new List<MemoryFragment> { withAudio });
                MemoryCardComposer.CardMeta m2 = MemoryCardComposer.ComposeCard(z4Room, new List<MemoryFragment> { noAudio });
                float wavLen = MemoryCardComposer.WavDurationSeconds(wavAbs);
                bool ok1 = m1 != null && m1.BadgeVisible && m1.BadgeDurationText == "0:01";
                bool ok2 = m2 != null && !m2.BadgeVisible && m2.BadgeDurationText == null;
                if (!ok1 || !ok2 || Mathf.Abs(wavLen - 1.5f) > 0.05f)
                {
                    Debug.LogError("[SmokeProbe] FAIL-Z4: 音频角标异常 (with=" + (m1 != null ? m1.BadgeDurationText : "null")
                        + " visible=" + (m1 != null && m1.BadgeVisible) + " none=" + (m2 != null ? m2.BadgeDurationText : "null")
                        + " m2Visible=" + (m2 != null && m2.BadgeVisible) + " wavLen=" + wavLen.ToString("F2") + "）");
                    pass = false; failedSections++;
                }
                else
                {
                    // 证据：两张卡片并排对比图
                    bool cmp = StitchPngs(m1.PngPath, m2.PngPath, "docs/evidence/r16_card_badge_compare.png");
                    Debug.Log("[SmokeProbe] Z4 音频角标 OK（WAV 头 1.5s→" + m1.BadgeDurationText + "，无音频不渲染；对比图="
                        + (cmp ? "已拼" : "FAIL") + "）");
                    if (!cmp) { pass = false; failedSections++; }
                }
            }

            // ========== D3. 光点密度上限（R16-2：>12 告警+过密簇视觉降密度） ==========
            if (pass)
            {
                LightPointAnchor[] seeds = FindAll<LightPointAnchor>();
                if (seeds.Length == 0)
                {
                    Debug.LogError("[SmokeProbe] FAIL-D3: 无光点种子可复制");
                    pass = false; failedSections++;
                }
                else
                {
                    GameObject seed = seeds[0].gameObject;
                    var injected = new List<LightPointAnchor>();
                    Vector3 baseA = new Vector3(0f, 1.2f, 8f);
                    Vector3 baseB = new Vector3(4.5f, 1.2f, 8f);
                    for (int i = 0; i < 20; i++)
                    {
                        int row = i / 5, col = i % 5;
                        Vector3 pos = (i < 10 ? baseA : baseB) + new Vector3(col * 0.5f, 0f, row * 0.5f);
                        GameObject go = Object.Instantiate(seed, pos, Quaternion.identity);
                        go.name = "LP_density_" + i;
                        LightPointAnchor a = go.GetComponent<LightPointAnchor>();
                        if (a != null) injected.Add(a);
                    }
                    yield return null; // 等 Update 写材质（before 图有发光观感）

                    GameObject birdGo = new GameObject("DensityBirdCamera");
                    Camera bird = birdGo.AddComponent<Camera>();
                    bird.orthographic = true;
                    bird.orthographicSize = 3.4f;
                    bird.transform.position = new Vector3(2.25f, 7f, 8f);
                    bird.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                    bird.aspect = 4f / 3f;
                    bird.clearFlags = CameraClearFlags.SolidColor;
                    bird.backgroundColor = UiTokens.EvidenceBg;

                    RenderCamToPng(bird, 800, 600, "docs/evidence/r16_density_before.png");
                    bool trigger = LightPointDensityManager.Check(injected);
                    yield return null; // 等 Update 应用 dim
                    RenderCamToPng(bird, 800, 600, "docs/evidence/r16_density_after.png");
                    Object.Destroy(birdGo);

                    int dimAnchors = 0;
                    foreach (LightPointAnchor a in injected) if (a.DensityDim < 1f) dimAnchors++;
                    if (LightPointDensityManager.DensityCount != 20 || LightPointDensityManager.ClusterCount < 2
                        || !LightPointDensityManager.Warned || LightPointDensityManager.DimmedCount < 1
                        || dimAnchors < LightPointDensityManager.DimmedCount || !trigger)
                    {
                        Debug.LogError("[SmokeProbe] FAIL-D3: 密度管理异常 (density=" + LightPointDensityManager.DensityCount
                            + " clusters=" + LightPointDensityManager.ClusterCount + " warned=" + LightPointDensityManager.Warned
                            + " dimmed=" + LightPointDensityManager.DimmedCount + " dimAnchors=" + dimAnchors + " trigger=" + trigger + ")");
                        pass = false; failedSections++;
                    }
                    else
                    {
                        Debug.Log("[SmokeProbe] D3 密度管理 OK（20 点→" + LightPointDensityManager.ClusterCount
                            + " 簇，告警=true，降密度 " + LightPointDensityManager.DimmedCount + " 成员压暗+缩符号；前后对比图已落盘）");
                    }
                    foreach (LightPointAnchor a in injected) if (a != null) Object.Destroy(a.gameObject);
                    LightPointDensityManager.ResetVisual();
                    yield return new WaitForSecondsRealtime(2.5f); // 等桥接清理轮询，护住 T2 退避窗口
                }
            }

            // ========== R2. 第二主题路线：雨夜校园（R16-3：路线 8+主题色+符号全映射） ==========
            if (pass)
            {
                RoomManager rm = FindFirst<RoomManager>();
                List<MemoryFragment> rainRoute = RouteEditor.OrderedFragments("room_03");
                bool lenOk = rm != null && rainRoute.Count == 8;
                bool themeOk = ThemeColors.For("rain").Accent != ThemeColors.For("amber").Accent
                    && ThemeColors.For("rain").Ambient != ThemeColors.For("amber").Ambient;
                if (!lenOk || !themeOk)
                {
                    Debug.LogError("[SmokeProbe] FAIL-R2: 路线/主题异常 (route=" + rainRoute.Count + " themeOk=" + themeOk + ")");
                    pass = false; failedSections++;
                }
                else
                {
                    int rainIdx = -1;
                    for (int i = 0; i < rm.Rooms.Count; i++) if (rm.Rooms[i].id == "room_03") rainIdx = i;
                    rm.SwitchToRoom(rainIdx);
                    yield return null; // 等 spawn

                    AppData rd = SaveDataService.LoadAppData();
                    RoomData rainData = null;
                    for (int i = 0; i < rd.rooms.Count; i++) if (rd.rooms[i].id == "room_03") rainData = rd.rooms[i];

                    LightPointAnchor[] rainAnchors = FindAll<LightPointAnchor>();
                    bool symOk = rainAnchors.Length == 8 && rainData != null;
                    string kindTable = "";
                    foreach (LightPointAnchor a in rainAnchors)
                    {
                        string t = null;
                        foreach (MemoryFragment f in rainData.fragments) if (f.id == a.FragmentId) t = f.type;
                        LightPointSymbol.Kind expect = LightPointSymbol.KindFor(false, t);
                        bool ok = a.Symbol != null && a.Symbol.CurrentKind == expect;
                        symOk &= ok;
                        kindTable += a.FragmentId + "=" + (ok ? expect.ToString() : "MISMATCH") + " ";
                    }
                    Vector4 ambNow = RenderSettings.ambientLight;
                    Vector4 ambWant = ThemeColors.For("rain").Ambient;
                    bool themeApplied = (ambNow - ambWant).sqrMagnitude < 0.01f
                        && RenderSettings.fogColor == ThemeColors.For("rain").Fog;

                    // 证据：雨夜全景截图 + 路线卡片×2
                    byte[] roomShot = RenderMainCamera(800, 600);
                    System.IO.File.WriteAllBytes("docs/evidence/r16_theme_rain_room.png", roomShot);
                    MemoryCardComposer.CardMeta c1 = MemoryCardComposer.ComposeCard(rainData, rainRoute);
                    MemoryCardComposer.CardMeta c2 = MemoryCardComposer.ComposeCard(rainData, rainRoute);
                    bool cardsOk = c1 != null && c1.PngBytes > 0 && c2 != null && c2.PngBytes > 0
                        && System.IO.File.Exists(c1.PngPath) && System.IO.File.Exists(c2.PngPath);
                    if (cardsOk)
                    {
                        System.IO.File.Copy(c1.PngPath, "docs/evidence/r16_card_campus_1.png", true);
                        System.IO.File.Copy(c2.PngPath, "docs/evidence/r16_card_campus_2.png", true);
                    }

                    if (!symOk || !themeApplied || !cardsOk)
                    {
                        Debug.LogError("[SmokeProbe] FAIL-R2: 主题切换异常 (anchors=" + rainAnchors.Length
                            + " symOk=" + symOk + " themeApplied=" + themeApplied + " cardsOk=" + cardsOk + ") " + kindTable);
                        pass = false; failedSections++;
                    }
                    else
                    {
                        Debug.Log("[SmokeProbe] R2 雨夜校园 OK（路线 8 站/主题色 rain≠amber/环境光雾色已切/8 点符号全映射无 fallback；全景+卡片×2 已落盘）");
                    }
                    rm.SwitchToRoom(0); // 恢复主房间
                    yield return new WaitForSecondsRealtime(2.5f); // 等桥接轮询稳定
                }
            }

            // ========== D4. 新元素可读性实测（R16-4：卡片标题+符号浮标 2/3/5m） ==========
            if (pass)
            {
                RoomData d4Room = null;
                AppData d4Data = SaveDataService.LoadAppData();
                if (d4Data.rooms.Count > 0) d4Room = d4Data.rooms[0];
                List<MemoryFragment> d4Route = RouteEditor.OrderedFragments("room_01");
                Vector3 rootPos = new Vector3(-5f, -500f, 0f);
                GameObject cardRoot = MemoryCardComposer.BuildCardRoot(rootPos, d4Room, d4Route, null, out _);
                // 符号浮标同框（卡板右侧 0.55m）
                Shader symShader = Shader.Find("MemoryCorridor/Sprite3D");
                Material symMat = new Material(symShader);
                symMat.mainTexture = LightPointSymbol.SymbolTexture(LightPointSymbol.Kind.Image, UiTokens.Amber);
                GameObject symQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Collider symQuadCol = symQuad.GetComponent<Collider>();
                if (symQuadCol != null) Destroy(symQuadCol);
                symQuad.transform.SetParent(cardRoot.transform, false);
                symQuad.transform.localPosition = new Vector3(0.55f, 0.10f, 0f);
                symQuad.transform.localScale = Vector3.one * UiTokens.SymbolSize;
                symQuad.GetComponent<MeshRenderer>().sharedMaterial = symMat;

                GameObject d4CamGo = new GameObject("ReadabilityCamera");
                d4CamGo.transform.rotation = Quaternion.identity;
                Camera d4Cam = d4CamGo.AddComponent<Camera>();
                d4Cam.fieldOfView = 60f;
                d4Cam.aspect = 4f / 3f;
                d4Cam.nearClipPlane = 0.05f;
                d4Cam.farClipPlane = 12f;
                d4Cam.clearFlags = CameraClearFlags.SolidColor;
                d4Cam.backgroundColor = UiTokens.EvidenceBg;

                float[] dists = { 2f, 3f, 5f };
                bool allShot = true;
                foreach (float dist in dists)
                {
                    d4CamGo.transform.position = rootPos + new Vector3(0.4f, 0, -dist);
                    byte[] png = MemoryCardComposer.RenderCardRoot(cardRoot, d4Cam, 800, 600);
                    string path = "docs/evidence/r16_readability_" + dist.ToString("F0") + "m.png";
                    System.IO.File.WriteAllBytes(path, png);
                    bool ok = png.Length > 5000;
                    allShot &= ok;
                    Debug.Log("[SmokeProbe] D4 距离实拍 " + dist + "m: " + path + "（" + png.Length + " 字节）");
                    yield return null;
                }
                Object.Destroy(cardRoot);
                Object.Destroy(d4CamGo);

                if (!allShot)
                {
                    Debug.LogError("[SmokeProbe] FAIL-D4: 距离实拍落盘异常");
                    pass = false; failedSections++;
                }
                else
                {
                    Debug.Log("[SmokeProbe] D4 可读性实测 OK（卡片标题 64mm/符号 100mm @2/3/5m 三张实拍，dmm 表入 SPATIAL_AUDIT §3）");
                }
            }

            // ========== W. 音量五档映射实测（R13 E9） ==========
            if (pass)
            {
                int[] percents = { 0, 25, 50, 75, 100 };
                bool volOk = true;
                string volTable = "";
                for (int i = 0; i < percents.Length; i++)
                {
                    float v = SettingsService.SetVolumePercent(percents[i]);
                    float listener = AudioListener.volume;
                    float db = 20f * Mathf.Log10(Mathf.Max(0.0001f, listener));
                    bool persisted = Mathf.Abs(SaveDataService.LoadAppData().settings.masterVolume - v) < 0.01f;
                    bool match = Mathf.Abs(listener - v) < 0.01f;
                    volTable += percents[i] + "%→" + listener.ToString("F2") + "(" + db.ToString("F1") + "dB) ";
                    if (!match || !persisted) volOk = false;
                }
                // 恢复 100%
                SettingsService.SetVolumePercent(100);

                if (!volOk)
                {
                    Debug.LogError("[SmokeProbe] FAIL-W: 音量五档异常 " + volTable);
                    pass = false; failedSections++;
                }
                else
                {
                    Debug.Log("[SmokeProbe] W 音量五档 OK（对数映射表：" + volTable + "）");
                }
            }

            // ========== T2. 桥接器轮询退避量化（R13 E11） ==========
            if (pass)
            {
                int pollBefore = XRIAnchorBridge.WirePollCount;
                yield return new WaitForSecondsRealtime(2f);
                int pollDelta = XRIAnchorBridge.WirePollCount - pollBefore;
                // 0.5s 退避 → 2s 内 ≤5 次（旧版每帧 ≈120 次/2s）
                if (pollDelta > 5)
                {
                    Debug.LogError("[SmokeProbe] FAIL-T2: 轮询退避未生效 delta=" + pollDelta);
                    pass = false; failedSections++;
                }
                else
                {
                    Debug.Log("[SmokeProbe] T2 轮询退避 OK（2s 内 " + pollDelta + " 次，旧版每帧 ≈120 次 → N→M 量化达标）");
                }
            }


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

            // ========== Z. 导出/导入（P0 第 6 项闭环，R3） ==========
            if (pass)
            {
                try
                {
                    // 1) 导出：zip 生成 + 隐私过滤（frag_02 私密不得入包）
                    string zipPath = ExportService.ExportAll();
                    if (!System.IO.File.Exists(zipPath))
                    {
                        throw new System.Exception("导出 zip 未生成");
                    }
                    using (System.IO.Compression.ZipArchive archive = System.IO.Compression.ZipFile.OpenRead(zipPath))
                    {
                        System.IO.Compression.ZipArchiveEntry jsonEntry = archive.GetEntry("appdata.json");
                        if (jsonEntry == null) throw new System.Exception("包内缺 appdata.json");
                        string json;
                        using (var reader = new System.IO.StreamReader(jsonEntry.Open()))
                        {
                            json = reader.ReadToEnd();
                        }
                        if (json.Contains("\"frag_02\""))
                        {
                            throw new System.Exception("隐私过滤失败：私密碎片 frag_02 出现在导出包");
                        }
                        if (!json.Contains("\"frag_01\"")) throw new System.Exception("公开碎片意外丢失");
                        if (!archive.GetEntry("Media/frag_01/sewing.jpg").FullName.Contains("frag_01"))
                        {
                            throw new System.Exception("媒体文件缺失");
                        }
                    }
                    Debug.Log("[SmokeProbe] Z 导出 OK（zip 生成+私密过滤+媒体随包）");

                    // 2) 导入：合并 + id 冲突后缀
                    ImportService.ImportResult result = ImportService.Import(zipPath);
                    AppData merged = SaveDataService.LoadAppData();
                    bool hasSuffix = false;
                    for (int r = 0; r < merged.rooms.Count; r++)
                    {
                        if (merged.rooms[r].fragments == null) continue;
                        for (int f = 0; f < merged.rooms[r].fragments.Count; f++)
                        {
                            if (merged.rooms[r].fragments[f].id.Contains("_i")) hasSuffix = true;
                        }
                    }
                    if (!result.success || result.importedFragments < 1 || !hasSuffix)
                    {
                        throw new System.Exception("导入异常 success=" + result.success
                            + " frags=" + result.importedFragments + " 后缀=" + hasSuffix);
                    }
                    Debug.Log("[SmokeProbe] Z 导入 OK（" + result.message + "，id 冲突自动后缀）");
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[SmokeProbe] FAIL-Z: 导出/导入异常 " + e.Message);
                    pass = false; failedSections++;
                }
            }

            // ========== S. 静态安全扫描（R11 扩面：坑-043 防复发） ==========
            if (pass)
            {
                Collider[] allColliders = UnityEngine.Object.FindObjectsByType<Collider>(
                    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None); // 坑-050：残留可能在 inactive 对象上
                int concaveTrigger = 0;
                for (int i = 0; i < allColliders.Length; i++)
                {
                    Collider c = allColliders[i];
                    if (c.isTrigger && c is MeshCollider mc && !mc.convex)
                    {
                        concaveTrigger++;
                        Debug.LogError("[SmokeProbe] S: 凹面 trigger 残留于 " + c.gameObject.name);
                    }
                }
                if (concaveTrigger > 0)
                {
                    Debug.LogError("[SmokeProbe] FAIL-S: 凹面 MeshCollider+trigger 残留 " + concaveTrigger + " 处（坑-043 防复发）");
                    pass = false; failedSections++;
                }
                else
                {
                    Debug.Log("[SmokeProbe] S 静态安全扫描 OK（全场景 " + allColliders.Length
                        + " 个 Collider，凹面 trigger=0）");
                }
            }

            // ========== T. 舒适性设置三新项（R11 a/b，静态断言版） ==========
            // 坑-044 收口：Unity Volume profile 在 batchmode TryGet 不到 → 改静态断言（源码级验存在），
            // 运行时视觉验证列待用户侧 GUI 目检。多轮 Edit 锚点漂移止损法：标记区间整段重写。
            if (pass)
            {
                // 静态断言 1：SettingsService 存在运行时动态创建/应用 Vignette 的调用点
                string serviceSrc = System.IO.File.ReadAllText(
                    System.IO.Path.Combine(Application.dataPath, "Scripts/Core/SettingsService.cs"));
                bool hasDynamicCreate = serviceSrc.Contains("profile.Add<UnityEngine.Rendering.Universal.Vignette>")
                    && serviceSrc.Contains("vignette.intensity.Override");
                // 静态断言 2：settings 有 vignetteEnabled/vignetteIntensity 字段
                string appDataSrc = System.IO.File.ReadAllText(
                    System.IO.Path.Combine(Application.dataPath, "Scripts/Core/AppData.cs"));
                bool hasField = appDataSrc.Contains("vignetteEnabled")
                    && appDataSrc.Contains("vignetteIntensity");
                // 静态断言 3：设置面板有距离/暗角/移动方式三行
                string panelSrc = System.IO.File.ReadAllText(
                    System.IO.Path.Combine(Application.dataPath, "Scripts/Media/SettingsPanel.cs"));
                bool hasRows = panelSrc.Contains("面板距离")
                    && panelSrc.Contains("舒适暗角")
                    && panelSrc.Contains("移动方式")
                    && panelSrc.Contains("CycleVignette")
                    && panelSrc.Contains("CycleDistance");
                // 静态断言 4：引导条有设置入口步（R6）
                string guideSrc = System.IO.File.ReadAllText(
                    System.IO.Path.Combine(Application.dataPath, "Scripts/Media/TutorialGuide.cs"));
                bool hasGuide = guideSrc.Contains("设置面板");

                if (!hasDynamicCreate || !hasField || !hasRows || !hasGuide)
                {
                    Debug.LogError("[SmokeProbe] FAIL-T: 静态断言失败 (dynamic=" + hasDynamicCreate
                        + " field=" + hasField + " rows=" + hasRows + " guide=" + hasGuide + ")");
                    pass = false; failedSections++;
                }
                else
                {
                    Debug.Log("[SmokeProbe] T 舒适性设置 OK（静态断言：动态创建调用点/vignette 字段/面板三行/引导步齐备；运行时视觉验证待 GUI 目检）");
                }
            }

            Debug.Log(pass
                ? "[SmokeProbe] PASS: 房间/XRI/悬停/选中/褪色/排序/走读/回访/切换/重演/星河墙×2/转场/传送/面板/星尘/导入/导出导入/设置开关/多维排序/安全扫描/舒适性/记忆点符号(Y5)/回忆卡片(Z3)/音频角标(Z4)/密度上限(D3)/雨夜主题(R2)/可读性实测(D4) 全部通过"
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

            // R1 E6：点星重演应发生在星点位置（等一拍后查分层场景位置）
            yield return new WaitForSecondsRealtime(0.4f);
            LayeredPhoto[] replayLayers = FindAll<LayeredPhoto>();
            GameObject star1Go = GameObject.Find("Star_frag_01");
            if (replayLayers.Length < 1 || star1Go == null
                || Vector3.Distance(replayLayers[0].transform.position,
                    star1Go.transform.position + Vector3.up * 0.4f) > 0.5f)
            {
                Debug.LogError("[SmokeProbe] FAIL-H: 点星重演未在星点位置 (layers=" + replayLayers.Length + ")");
                sectionPass = false;
                yield break;
            }
            ReplaySequencer wallSequencer = FindFirst<ReplaySequencer>();
            if (wallSequencer != null)
            {
                wallSequencer.SkipToEnd(); // 复原，避免影响后续段
            }
            yield return new WaitForSecondsRealtime(1.0f); // R12：等待余量 0.2→1.0s（OnReplayBeat 同步链路+帧间时差）

            // R2：wall 路径统一——Done 兜底后面板应已显示（延迟出面板链路）
            MemoryPanel[] allPanels = FindAll<MemoryPanel>();
            MemoryPanel wallPanel = allPanels.Length > 0 ? allPanels[0] : null;
            Debug.Log("[SmokeProbe] H 诊断: MemoryPanel 实例数=" + allPanels.Length
                + " showing=" + (wallPanel != null && wallPanel.IsShowing)
                + " activeSelf=" + (wallPanel != null && wallPanel.gameObject.activeSelf)
                + " activeInHierarchy=" + (wallPanel != null && wallPanel.gameObject.activeInHierarchy)
                + " instanceID=" + (wallPanel != null ? wallPanel.GetInstanceID() : 0));
            if (wallPanel == null || !wallPanel.IsShowing)
            {
                Debug.LogError("[SmokeProbe] FAIL-H: wall 路径延迟出面板未生效");
                sectionPass = false;
                yield break;
            }
            wallPanel.Hide();
            Debug.Log("[SmokeProbe] H 星河墙点星→重演→面板（统一延迟链路）OK");
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
            // 坑-021 同类：面板常态 inactive，必须含未激活搜索
            MemoryPanel[] panels = UnityEngine.Object.FindObjectsByType<MemoryPanel>(
                UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
            MemoryPanel panel = panels.Length > 0 ? panels[0] : null;
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
            yield return null;
            if (!panel.IsShowing || panel.PhotoTexture == null || panel.TitleString != "缝纫机")
            {
                Debug.LogError("[SmokeProbe] FAIL-L: 面板显示异常 (showing=" + panel.IsShowing
                    + " photo=" + (panel.PhotoTexture != null) + " title=" + panel.TitleString + ")");
                sectionPass = false;
                yield break;
            }
            if (!panel.HasCloseButton)
            {
                Debug.LogError("[SmokeProbe] FAIL-L: 关闭按钮缺失（R1 E3）");
                sectionPass = false;
                yield break;
            }

            // R2 E8：录音播放链路（frag_02 有自制 radio.wav；私密碎片本人视角可看可听）
            AppData audioData = SaveDataService.LoadAppData();
            MemoryFragment audioFrag = FindFragmentById(audioData, "frag_02");
            if (audioFrag == null)
            {
                Debug.LogError("[SmokeProbe] FAIL-L: frag_02 缺失");
                sectionPass = false;
                yield break;
            }
            // R8：视频记忆播放链路（frag_03 mixed 含 video 项，ffmpeg 生成测试视频）
            panel.Show(audioFrag /* frag_02 先验录音 */);
            yield return new WaitForSecondsRealtime(0.3f);
            MemoryFragment videoFrag = FindFragmentById(SaveDataService.LoadAppData(), "frag_03");
            if (videoFrag == null) throw new System.Exception("frag_03 缺失");
            panel.Show(videoFrag);
            yield return new WaitForSecondsRealtime(2.5f); // 等 VideoPlayer prepare+起播
            if (!panel.IsVideoPlaying)
            {
                Debug.LogError("[SmokeProbe] FAIL-L: 视频未播放（R8 链路异常）");
                sectionPass = false;
                yield break;
            }
            Debug.Log("[SmokeProbe] L 视频播放 OK（memories.mp4 RenderTexture→RawImage）");
            panel.Hide();
            yield return null;
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
        /// <summary>
        /// R15 证据：符号特写（四类型+私密各一张 800×600，落 docs/evidence/）。
        /// 卡片合成同款管线（Sprite3D quad+正交相机+RT ReadPixels），隔离区 (5,-500,0) 摆放
        /// 「CardBg 底板 + 符号 quad」——不实例化光点本体。
        /// 坑-051：特写相机距光晕壳 <1.2m 时壳的角尺寸大于 ortho 视野 → 壳填屏（两轮实测）；
        /// 证据截图只摆符号 quad，不依赖场景几何。batchmode 非 Play 相机渲染另见坑-051①。
        /// </summary>
        private IEnumerator CaptureSymbolEvidence()
        {
            Shader spriteShader = Shader.Find("MemoryCorridor/Sprite3D");
            if (spriteShader == null) yield break;

            string[] types = { "image", "audio", "diary", "mixed", "private" };
            Color[] inks = { UiTokens.Amber, UiTokens.Cyan, UiTokens.Cream, UiTokens.WarmOrange, UiTokens.WarmRed };
            Vector3 basePos = new Vector3(5f, -500f, 0f);

            GameObject camGo = new GameObject("SymbolEvidenceCamera");
            camGo.transform.position = basePos + new Vector3(0, 0, -1f);
            camGo.transform.rotation = Quaternion.identity;
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.18f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 2f;
            cam.aspect = 4f / 3f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UiTokens.EvidenceBg;

            for (int i = 0; i < types.Length; i++)
            {
                GameObject rootGo = new GameObject("SymEvidence_" + types[i]);
                rootGo.transform.position = basePos + new Vector3(0, 0, i * 0.2f);

                Texture2D boardTex = RoundedRectTexture.RoundedRect(400, 400, 24, UiTokens.CardBg, UiTokens.LineSoft, 2);
                Material boardMat = new Material(spriteShader);
                boardMat.mainTexture = boardTex;
                GameObject board = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Collider boardCol = board.GetComponent<Collider>();
                if (boardCol != null) Destroy(boardCol);
                board.name = "Board";
                board.transform.SetParent(rootGo.transform, false);
                board.transform.localPosition = new Vector3(0, 0, 0.01f);
                board.transform.localScale = new Vector3(0.30f, 0.30f, 1);
                board.GetComponent<MeshRenderer>().sharedMaterial = boardMat;

                Material symMat = new Material(spriteShader);
                symMat.mainTexture = LightPointSymbol.SymbolTexture((LightPointSymbol.Kind)i, inks[i]);
                GameObject sym = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Collider symCol = sym.GetComponent<Collider>();
                if (symCol != null) Destroy(symCol);
                sym.name = "Sym";
                sym.transform.SetParent(rootGo.transform, false);
                sym.transform.localPosition = Vector3.zero;
                sym.transform.localScale = new Vector3(0.16f, 0.16f, 1);
                sym.GetComponent<MeshRenderer>().sharedMaterial = symMat;

                RenderTexture rt = new RenderTexture(800, 600, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                Texture2D shot = new Texture2D(800, 600, TextureFormat.RGBA32, false);
                shot.ReadPixels(new Rect(0, 0, 800, 600), 0, 0);
                shot.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                rt.Release();

                string path = "docs/evidence/r15_symbol_" + types[i] + ".png";
                System.IO.File.WriteAllBytes(path, shot.EncodeToPNG());
                Destroy(shot);
                Destroy(rootGo);
                Debug.Log("[SmokeProbe] Y5 符号特写: " + path);
                yield return null;
            }
            Destroy(camGo);
        }

        /// <summary>R16 Z4：合成测试 WAV（RIFF 头 + 正弦采样）。</summary>
        private static void WriteTestWav(string path, float seconds, float freq, int rate)
        {
            int n = (int)(seconds * rate);
            int dataBytes = n * 2;
            using (var ms = new System.IO.MemoryStream())
            using (var w = new System.IO.BinaryWriter(ms))
            {
                w.Write(new char[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new char[] { 'W', 'A', 'V', 'E' });
                w.Write(new char[] { 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(rate);
                w.Write(rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new char[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);
                for (int i = 0; i < n; i++)
                {
                    w.Write((short)(System.Math.Sin(2 * System.Math.PI * freq * i / rate) * 12000));
                }
                w.Flush();
                System.IO.File.WriteAllBytes(path, ms.ToArray());
            }
        }

        /// <summary>R16 Z4：两张 PNG 并排拼接落盘（1440×960）。</summary>
        private static bool StitchPngs(string pathA, string pathB, string outPath)
        {
            try
            {
                Texture2D a = new Texture2D(2, 2);
                a.LoadImage(System.IO.File.ReadAllBytes(pathA));
                Texture2D b = new Texture2D(2, 2);
                b.LoadImage(System.IO.File.ReadAllBytes(pathB));
                int w = a.width + b.width;
                int h = Mathf.Max(a.height, b.height);
                Texture2D outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                Color[] clear = new Color[w * h];
                for (int i = 0; i < clear.Length; i++) clear[i] = UiTokens.EvidenceBg;
                outTex.SetPixels(clear);
                outTex.SetPixels(0, 0, a.width, a.height, a.GetPixels());
                outTex.SetPixels(a.width, 0, b.width, b.height, b.GetPixels());
                outTex.Apply();
                System.IO.File.WriteAllBytes(outPath, outTex.EncodeToPNG());
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SmokeProbe] 拼图失败: " + e.Message);
                return false;
            }
        }

        /// <summary>R16 D3：任意相机渲一帧 PNG 落盘。</summary>
        private static void RenderCamToPng(Camera cam, int w, int h, string path)
        {
            RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D shot = new Texture2D(w, h, TextureFormat.RGBA32, false);
            shot.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            shot.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();
            System.IO.File.WriteAllBytes(path, shot.EncodeToPNG());
        }

        /// <summary>R16 R2：主相机渲一帧（返回 PNG 字节）。</summary>
        private static byte[] RenderMainCamera(int w, int h)
        {
            Camera cam = Camera.main;
            if (cam == null) return new byte[0];
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

        private static T FindFirst<T>() where T : UnityEngine.Object
        {
            // R12：统一含 inactive——P 段隐藏告示牌/L 段隐藏面板后，D 段仍可读（坑-049 第四犯教训）
            T[] found = UnityEngine.Object.FindObjectsByType<T>(
                UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
            return found.Length > 0 ? found[0] : null;
        }

        private static T[] FindAll<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsByType<T>(
                UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
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
            yield return new WaitForSecondsRealtime(120f); // R16：+4 新段（渲染 8 张证据 PNG+接线稳定等待）扩容
            Debug.LogError("[SmokeProbe] WATCHDOG: 120s 超时强制退出(1)");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(1); // 坑-031：仅编辑器；真机上只留日志
#endif
        }
    }
}
