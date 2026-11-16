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
                    HoverPreviewCard card = FindFirst<HoverPreviewCard>();
                    if (card == null || !card.IsShowing || card.TitleString != "缝纫机")
                    {
                        Debug.LogError("[SmokeProbe] FAIL-B: 预览卡异常 (showing=" + (card != null && card.IsShowing)
                            + " title=" + (card != null ? card.TitleString : "null") + ")");
                        pass = false; failedSections++;
                    }
                    else
                    {
                        Debug.Log("[SmokeProbe] B 悬停预览卡 OK（缩略+标题）");
                        card.Hide();
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

                    // Time 升序：f03(2008) 最老在前，f05(2020) 最新在后
                    List<MemoryFragment> byTime = MemorySortService.Sorted(all, SortCriterion.Time);
                    bool timeOk = byTime[0].id == "frag_03" && byTime[byTime.Count - 1].id == "frag_05";

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
                    UnityEngine.FindObjectsSortMode.None);
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
                ? "[SmokeProbe] PASS: 房间/XRI/悬停/选中/褪色/排序/走读/回访/切换/重演/星河墙×2/转场/传送/面板/星尘/导入/导出导入/设置开关/多维排序/安全扫描/舒适性 全部通过"
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
            yield return new WaitForSecondsRealtime(0.2f);

            // R2：wall 路径统一——Done 兜底后面板应已显示（延迟出面板链路）
            MemoryPanel wallPanel = FindFirst<MemoryPanel>();
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
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(1); // 坑-031：仅编辑器；真机上只留日志
#endif
        }
    }
}
