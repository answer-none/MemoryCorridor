using System.Collections;
using MemoryCorridor.Core;
using UnityEngine;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 音频讲解自动播放控制（R17-2，延续 R16-1 音频角标）：
    /// 进入房间自动播放首个带可解析音频的记忆点讲解；
    /// 视线离开（夹角 >30°）或走远（>2m）自动暂停，回来续播（不重头，AudioSource.Pause/UnPause 保留 time）。
    /// 播放中 HUD 显示标题 + 进度条（AudioNarrationHud 同物体构建）。
    /// 探针 TestHook：ForcePlayProbe / AutoPlayed / PausedOnLeave / ResumeFromOffset。
    /// </summary>
    public class AutoNarrationController : MonoBehaviour
    {
        [SerializeField] private RoomManager roomManager;

        private AudioSource source;
        private Transform trackTarget;
        private Coroutine loadRoutine;
        private string currentTitle;
        private bool pausedByLeave;
        private float monitorTimer;
        private float hudTimer;

        // —— 探针/文档断言读数 ——
        public bool AutoPlayed { get; private set; }
        public bool PausedOnLeave { get; private set; }
        public float ResumeFromOffset { get; private set; }
        public bool IsPlaying => source != null && source.isPlaying;
        public string CurrentTitle => currentTitle;
        public string CurrentSubtitle { get; private set; } // R20：字幕文本（diary 摘要，探针断言）
        public float Progress01 => (source != null && source.clip != null && source.clip.length > 0f)
            ? Mathf.Clamp01(source.time / source.clip.length) : 0f;

        // —— HUD 元素 ——
        private GameObject hudRoot;
        private TextMesh hudTitle;
        private TextMesh hudTime;      // R17：当前/总时长（"0:01 / 0:03"）
        private TextMesh hudSubtitle;  // R20：讲解字幕（diary 摘要）
        private Transform hudProgressFill;
        private const float HudDist = 1.0f;

        /// <summary>编辑器搭建注入（builder）。</summary>
        public void Configure(RoomManager rm) { roomManager = rm; }

        private void Start()
        {
            if (roomManager == null)
            {
                RoomManager[] found = FindObjectsByType<RoomManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (found.Length > 0) roomManager = found[0];
            }
            if (roomManager != null) roomManager.OnRoomSwitched += OnRoomSwitched;
            BuildHud();
            PlayForCurrentRoom();
        }

        private void OnDestroy()
        {
            if (roomManager != null) roomManager.OnRoomSwitched -= OnRoomSwitched;
        }

        private void OnRoomSwitched(int roomIndex)
        {
            PlayForCurrentRoom();
        }

        /// <summary>当前房间首个带可解析音频的碎片 → 自动播放。</summary>
        public void PlayForCurrentRoom()
        {
            RoomData room = roomManager != null ? roomManager.CurrentRoom() : null;
            if (room == null || room.fragments == null) return;
            for (int i = 0; i < room.fragments.Count; i++)
            {
                MemoryFragment f = room.fragments[i];
                if (f.mediaItems == null) continue;
                for (int m = 0; m < f.mediaItems.Count; m++)
                {
                    if (f.mediaItems[m].kind != "audio") continue;
                    string resolved = MediaResolver.ResolveExisting(f.mediaItems[m].path);
                    if (resolved == null) continue; // 缺失静默降级
                    Vector3 origin = Vector3.zero;
                    GameObject lp = GameObject.Find("LP_" + f.id); // 光点对象为判定基准（坑-049：LP_ 命名稳定且 active）
                    if (lp != null) origin = lp.transform.position;
                    LoadAndPlay(resolved, f.id, f.title, origin, f);
                    return;
                }
            }
            // 无音频碎片：静默不播（不报错，AutoPlayed 保持 false）
        }

        /// <summary>探针 TestHook：直接指定音频与判定基准点（隔离验证控制逻辑）。</summary>
        public void ForcePlayProbe(string resolvedPath, string fragmentId, string title, Vector3 origin)
        {
            LoadAndPlay(resolvedPath, fragmentId, title, origin);
        }

        /// <summary>R20 探针 TestHook 重载：带碎片（字幕来源 diary.text）。</summary>
        public void ForcePlayProbe(string resolvedPath, string fragmentId, string title, Vector3 origin, MemoryFragment fragment)
        {
            LoadAndPlay(resolvedPath, fragmentId, title, origin, fragment);
        }

        /// <summary>
        /// 探针 TestHook：按碎片播放（无音频→静默返回且 AutoPlayed=false，不报错不误播）。
        /// </summary>
        public void PlayFragmentProbe(MemoryFragment fragment, Vector3 origin)
        {
            if (fragment == null || fragment.mediaItems == null || fragment.mediaItems.Count == 0)
            {
                AutoPlayed = false; // 静音点：最后一次尝试未开播
                return;
            }
            for (int m = 0; m < fragment.mediaItems.Count; m++)
            {
                if (fragment.mediaItems[m].kind != "audio") continue;
                string resolved = MediaResolver.ResolveExisting(fragment.mediaItems[m].path);
                if (resolved == null) continue;
                LoadAndPlay(resolved, fragment.id, fragment.title, origin, fragment);
                return;
            }
            AutoPlayed = false; // 有 mediaItems 但无音频项
        }

        private MemoryFragment lastFragment; // R20：字幕来源

        private void LoadAndPlay(string resolvedPath, string fragmentId, string title, Vector3 origin, MemoryFragment fragment = null)
        {
            lastFragment = fragment;
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f; // HUD 讲解走 2D（跟随玩家）
            }
            if (loadRoutine != null) StopCoroutine(loadRoutine);
            pausedByLeave = false;
            currentTitle = title;
            narrationOrigin = new GameObject("NarrationOrigin_" + fragmentId).transform;
            narrationOrigin.position = origin;
            // R20：字幕=碎片 diary 摘要（前 24 字）
            string sub = "";
            if (lastFragment != null && lastFragment.diary != null && !string.IsNullOrEmpty(lastFragment.diary.text))
            {
                sub = lastFragment.diary.text;
                if (sub.Length > 24) sub = sub.Substring(0, 24) + "…";
            }
            CurrentSubtitle = sub;
            if (hudSubtitle != null) hudSubtitle.text = sub;
            loadRoutine = StartCoroutine(LoadRoutine(resolvedPath, fragmentId, title));
        }

        private IEnumerator LoadRoutine(string resolvedPath, string fragmentId, string title)
        {
            string url = "file:///" + resolvedPath.Replace('\\', '/');
            AudioType type = resolvedPath.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase)
                ? AudioType.MPEG : AudioType.WAV;
            using (UnityEngine.Networking.UnityWebRequest request =
                UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(url, type))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[AutoNarration] 加载失败（静默降级）: " + request.error);
                    yield break;
                }
                AudioClip clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(request);
                if (clip == null || source == null) yield break;

                AudioSettingsService.WarmSettings();
                source.clip = clip;
                source.time = 0f;
                source.volume = AudioSettingsService.NarrationVolume;
                source.Play();
                AutoPlayed = true;
                SetHudVisible(true);
                Debug.Log("[AutoNarration] 讲解自动播放: " + title + "（" + fragmentId + "，" + clip.length.ToString("F1") + "s）");
            }
        }

        private void Update()
        {
            if (source == null || source.clip == null) return;

            // 0.5s 周期监控：视线夹角（>30° 离开）+ 距离（>2m 走远）→ 暂停；恢复→续播
            monitorTimer += Time.unscaledDeltaTime;
            if (monitorTimer >= 0.5f)
            {
                monitorTimer = 0f;
                if (trackTarget == null)
                {
                    Camera cam = Camera.main;
                    if (cam != null) trackTarget = cam.transform;
                }
                if (trackTarget != null && narrationOrigin != null)
                {
                    Vector3 to = narrationOrigin.position - trackTarget.position;
                    float dist = to.magnitude;
                    float dot = Vector3.Dot(trackTarget.forward, to.normalized);
                    bool shouldPlay = dist <= 2f && dot > 0.866f; // 2m 内 + 视锥 30° 半角
                    if (shouldPlay && pausedByLeave)
                    {
                        source.UnPause(); // 续播（time 保留，不重头）
                        pausedByLeave = false;
                        ResumeFromOffset = source.time;
                        Debug.Log("[AutoNarration] 续播 @" + source.time.ToString("F1") + "s");
                    }
                    else if (!shouldPlay && source.isPlaying)
                    {
                        source.Pause();
                        pausedByLeave = true;
                        PausedOnLeave = true;
                        SetHudVisible(false);
                    }
                }
            }

            // HUD 跟随 + 进度刷新（0.1s 节流）
            hudTimer += Time.unscaledDeltaTime;
            if (hudTimer >= 0.1f && hudRoot != null && hudRoot.activeSelf)
            {
                hudTimer = 0f;
                Camera cam = Camera.main;
                if (cam != null)
                {
                    hudRoot.transform.position = cam.transform.position + cam.transform.forward * HudDist
                        + cam.transform.up * -0.18f;
                    // quad 正面法线 -z：+z 背向相机=正面朝玩家（避免镜像）
                    hudRoot.transform.rotation = Quaternion.LookRotation(
                        hudRoot.transform.position - cam.transform.position, cam.transform.up);
                }
                if (hudProgressFill != null)
                {
                    float p = Progress01;
                    // 左锚填充：位置随进度右移，宽度按进度缩放
                    hudProgressFill.localPosition = new Vector3(-0.22f + 0.44f * p * 0.5f, -0.024f, -0.02f);
                    hudProgressFill.localScale = new Vector3(Mathf.Max(0.001f, 0.44f * p), 0.012f, 1f);
                }
                if (hudTime != null && source != null && source.clip != null)
                {
                    hudTime.text = FormatTime(source.time) + " / " + FormatTime(source.clip.length);
                }
                if (source != null) source.volume = AudioSettingsService.NarrationVolume; // R20：音量即时跟随档位
            }
        }

        private Transform narrationOrigin; // 当前讲解光点位置（视线/距离判定基准）

        /// <summary>HUD：LabelBg 底条 + 标题 + 进度条（背景槽 + 填充条，UiTokens 尺寸）。</summary>
        private void BuildHud()
        {
            Shader spriteShader = Shader.Find("MemoryCorridor/Sprite3D");
            if (spriteShader == null) return;
            hudRoot = new GameObject("NarrationHud");
            hudRoot.SetActive(false);

            Material boardMat = new Material(spriteShader);
            boardMat.mainTexture = RoundedRectTexture.RoundedRect(560, 120, (int)UiTokens.BadgeRadius, UiTokens.LabelBg, UiTokens.LineSoft, 2);
            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Collider col = board.GetComponent<Collider>();
            if (col != null) Destroy(col); // 坑-043
            board.transform.SetParent(hudRoot.transform, false);
            board.transform.localScale = new Vector3(0.56f, 0.12f, 1);
            board.GetComponent<MeshRenderer>().sharedMaterial = boardMat;

            // 三段式布局：标题（左上）/ 时长（右上）/ 进度条（下行）。字符高 = fontSize×cs/10（无二级缩放）。
            // 标题 24×0.010/10=0.024m@1m=24dmm ✓（13 字≈0.31m 板内）
            hudTitle = UiTextFactory.CreateTextMesh(hudRoot.transform, "HudTitle",
                new Vector3(-0.24f, 0.030f, -0.01f), 24, 0.010f, TextAnchor.MiddleLeft, TextAlignment.Left,
                UiTokens.LabelInk, "");
            hudTime = UiTextFactory.CreateTextMesh(hudRoot.transform, "HudTime",
                new Vector3(0.26f, 0.030f, -0.01f), 22, 0.010f, TextAnchor.MiddleRight, TextAlignment.Right,
                UiTokens.CaptionInk, "0:00 / 0:00");

            // R20：字幕行（diary 摘要，板下方独立小条）
            Material subMat = new Material(spriteShader);
            subMat.mainTexture = RoundedRectTexture.RoundedRect(560, 72, (int)UiTokens.BadgeRadius, UiTokens.LabelBg, UiTokens.LineSoft, 2);
            GameObject subBoard = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Collider subCol = subBoard.GetComponent<Collider>();
            if (subCol != null) Destroy(subCol);
            subBoard.transform.SetParent(hudRoot.transform, false);
            subBoard.transform.localPosition = new Vector3(0, -0.105f, 0);
            subBoard.transform.localScale = new Vector3(0.56f, 0.072f, 1);
            subBoard.GetComponent<MeshRenderer>().sharedMaterial = subMat;
            // 字符高 = 20×0.010/10 = 0.020m @1m = 20 dmm ≥16 ✓（无二级缩放）
            hudSubtitle = UiTextFactory.CreateTextMesh(hudRoot.transform, "HudSubtitle",
                new Vector3(0, -0.105f, -0.01f), 20, 0.010f, TextAnchor.MiddleCenter, TextAlignment.Center,
                UiTokens.LabelInk, "");

            // 进度条：槽 0.44×0.012 下行居中 + 左锚填充（Update 按 Progress01 更新）
            Material slotMat = new Material(spriteShader);
            slotMat.mainTexture = RoundedRectTexture.RoundedRect(240, 12, 4, UiTokens.ThumbBg, UiTokens.LineSoft, 1);
            GameObject slot = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Collider slotCol = slot.GetComponent<Collider>();
            if (slotCol != null) Destroy(slotCol);
            slot.transform.SetParent(hudRoot.transform, false);
            slot.transform.localPosition = new Vector3(0, -0.024f, -0.01f);
            slot.transform.localScale = new Vector3(0.44f, 0.012f, 1);
            slot.GetComponent<MeshRenderer>().sharedMaterial = slotMat;

            Material fillMat = new Material(spriteShader);
            Texture2D fillTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color[] fillPx = { UiTokens.Amber, UiTokens.Amber, UiTokens.Amber, UiTokens.Amber };
            fillTex.SetPixels(fillPx);
            fillTex.Apply();
            fillMat.mainTexture = fillTex;
            GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Collider fillCol = fill.GetComponent<Collider>();
            if (fillCol != null) Destroy(fillCol);
            fill.transform.SetParent(hudRoot.transform, false);
            fill.transform.localPosition = new Vector3(-0.22f, -0.024f, -0.02f); // 左锚起点
            fill.transform.localScale = new Vector3(0.001f, 0.012f, 1);
            fill.GetComponent<MeshRenderer>().sharedMaterial = fillMat;
            hudProgressFill = fill.transform;
        }

        /// <summary>探针控制 HUD 显隐（截图证据用；游戏内随播放状态自动开合）。</summary>
        public void SetHudVisible(bool v, string titleOverride = null)
        {
            if (hudRoot == null) BuildHud();
            if (titleOverride != null && hudTitle != null) hudTitle.text = titleOverride;
            else if (hudTitle != null) hudTitle.text = currentTitle ?? "";
            if (hudTime != null && source != null && source.clip != null)
            {
                hudTime.text = FormatTime(source.time) + " / " + FormatTime(source.clip.length);
            }
            if (hudRoot != null) hudRoot.SetActive(v);
        }

        /// <summary>m:ss 文案（与卡片角标同口径）。</summary>
        private static string FormatTime(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return (s / 60) + ":" + (s % 60).ToString("00");
        }
    }
}
