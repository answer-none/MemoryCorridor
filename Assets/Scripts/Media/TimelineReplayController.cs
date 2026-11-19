using System.Collections.Generic;
using MemoryCorridor.Core;
using UnityEngine;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 时间轴回放（R19 P2 落地）：按记忆时间（diary.date 升序）自动串联重演当前房间碎片——
    /// 每片调 ReplaySequencer.Play（四拍时序），单片停留 6s 后推进，走完自动收。
    /// 接入 UI：SettingsPanel"时间轴回放"行点击启停。HUD 显示进度（N/M + 当前碎片标题）。
    /// 探针 TestHook：StartReplay/Advance/StopReplay 可手动推进（不等计时）。
    /// </summary>
    public class TimelineReplayController : MonoBehaviour
    {
        [SerializeField] private RoomManager roomManager;
        [SerializeField] private ReplaySequencer sequencer;
        [SerializeField] private float secondsPerFragment = 6f;

        private readonly List<MemoryFragment> queue = new List<MemoryFragment>();
        private int index = -1;
        private float holdTimer;
        private GameObject hudRoot;
        private TextMesh hudText;
        private float hudTimer;

        // —— 探针/断言读数 ——
        public bool IsReplaying { get; private set; }
        public int CurrentIndex => index;
        public int QueueCount => queue.Count;
        public string CurrentTitle { get; private set; }
        public static int SequencerCalls { get; private set; } // R19 断言：sequencer 被调计数

        /// <summary>编辑器搭建注入。</summary>
        public void Configure(RoomManager rm, ReplaySequencer seq)
        {
            roomManager = rm;
            sequencer = seq;
        }

        /// <summary>启动：当前房间碎片按 diary.date 升序排队，从最老开始重演。</summary>
        public bool StartReplay()
        {
            RoomData room = roomManager != null ? roomManager.CurrentRoom() : null;
            if (room == null || room.fragments == null || room.fragments.Count == 0) return false;

            queue.Clear();
            queue.AddRange(room.fragments);
            queue.Sort((x, y) => string.CompareOrdinal(
                x.diary != null ? x.diary.date : "",
                y.diary != null ? y.diary.date : ""));
            index = 0;
            IsReplaying = true;
            PlayCurrent();
            EnsureHud();
            if (hudRoot != null) hudRoot.SetActive(true);
            return true;
        }

        /// <summary>推进到下一站（自动计时/探针手动共用）。</summary>
        public void Advance()
        {
            if (!IsReplaying) return;
            index++;
            if (index >= queue.Count) { StopReplay(); return; }
            PlayCurrent();
        }

        /// <summary>停止（探针/切房/走完共用）。</summary>
        public void StopReplay()
        {
            IsReplaying = false;
            index = -1;
            holdTimer = 0f;
            if (hudRoot != null) hudRoot.SetActive(false);
        }

        private void PlayCurrent()
        {
            MemoryFragment f = queue[index];
            if (sequencer != null)
            {
                sequencer.Play(f, f.anchor.GetPosition(), "timeline");
                SequencerCalls++;
            }
            CurrentTitle = f.title;
            holdTimer = 0f;
        }

        private void Update()
        {
            if (!IsReplaying) return;
            holdTimer += Time.unscaledDeltaTime;
            if (holdTimer >= secondsPerFragment) Advance();

            hudTimer += Time.unscaledDeltaTime;
            if (hudTimer >= 0.2f && hudRoot != null && hudRoot.activeSelf)
            {
                hudTimer = 0f;
                Camera cam = Camera.main;
                if (cam != null)
                {
                    hudRoot.transform.position = cam.transform.position + cam.transform.forward * 1.0f
                        + cam.transform.up * 0.20f;
                    hudRoot.transform.rotation = Quaternion.LookRotation(
                        hudRoot.transform.position - cam.transform.position, cam.transform.up); // 正面朝玩家（R17 HUD 同修）
                }
                if (hudText != null)
                {
                    hudText.text = "时间轴回放 " + (index + 1) + "/" + queue.Count + " · " + CurrentTitle;
                }
            }
        }

        /// <summary>HUD：LabelBg 条 + 进度/标题行（三段式简化版，全令牌）。</summary>
        private void EnsureHud()
        {
            if (hudRoot != null) return;
            Shader spriteShader = Shader.Find("MemoryCorridor/Sprite3D");
            if (spriteShader == null) return;
            hudRoot = new GameObject("TimelineReplayHud");
            Material bm = new Material(spriteShader);
            bm.mainTexture = RoundedRectTexture.RoundedRect(560, 96, (int)UiTokens.BadgeRadius, UiTokens.LabelBg, UiTokens.LineSoft, 2);
            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Collider c = board.GetComponent<Collider>();
            if (c != null) Destroy(c); // 坑-043
            board.transform.SetParent(hudRoot.transform, false);
            board.transform.localScale = new Vector3(0.56f, 0.096f, 1);
            board.GetComponent<MeshRenderer>().sharedMaterial = bm;
            hudText = UiTextFactory.CreateTextMesh(hudRoot.transform, "HudText",
                new Vector3(0, 0, -0.01f), 26, 0.010f, TextAnchor.MiddleCenter, TextAlignment.Center,
                UiTokens.LabelInk, "时间轴回放");
            hudRoot.SetActive(false);
        }
    }
}
