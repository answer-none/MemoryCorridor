using System;
using System.Collections;
using MemoryCorridor.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 回忆重演四拍时序（TECH_DESIGN_V2 §3.2）：①现实退后（环境光压暗）
    /// → ②回忆绽放（分层伪 3D 展开）→ ③重返当时（旁白/日记，音频素材缺位时记日志）
    /// → ④温柔收回（复原）。SkipToEnd 直跳第④拍；不做相机接管（防眩晕 §5.3）。
    /// 坑-018：批处理/真机都按真实时间推进；W4 用全屏暗幕替换环境光压暗。
    /// </summary>
    public class ReplaySequencer : MonoBehaviour
    {
        public enum ReplayState { Idle, Beat1_Dim, Beat2_Bloom, Beat3_Return, Beat4_Settle, Done }

        /// <summary>四拍时长（秒，可调；冒烟用短拍）。public 供测试/调参。</summary>
        public float[] beatDurations = { 0.5f, 1.5f, 2.5f, 1.5f };
        [SerializeField] private float dimFloor = 0.3f;

        public ReplayState State { get; private set; }

        /// <summary>最近一次重演的发起方标签（"bridge"=光点主路径 / "wall"=星河墙），用于延迟出面板分流。</summary>
        public string LastPlaySource { get; private set; } = "legacy";

        public event Action<ReplayState> OnBeat;

        private Color ambientBefore;
        private bool dimming;
        private GameObject layerRoot;
        private bool skipRequested;

        /// <summary>开始重演（重复调用在运行期间被忽略）。</summary>
        public void Play(MemoryFragment fragment, Vector3 anchorPosition)
        {
            Play(fragment, anchorPosition, "legacy");
        }

        /// <summary>带来源标签的重演（bridge=光点主路径延迟出面板；wall=星河墙立即出面板）。</summary>
        public void Play(MemoryFragment fragment, Vector3 anchorPosition, string sourceId)
        {
            if (fragment == null) return;
            if (State != ReplayState.Idle && State != ReplayState.Done) return;

            LastPlaySource = sourceId ?? "legacy";
            skipRequested = false;
            StartCoroutine(SequenceRoutine(fragment, anchorPosition));
        }

        /// <summary>跳到第④拍（随时可按扳机跳过——方案 §3.2）。</summary>
        public void SkipToEnd()
        {
            skipRequested = true;
        }

        private IEnumerator SequenceRoutine(MemoryFragment fragment, Vector3 anchorPosition)
        {
            // ① 现实退后
            State = ReplayState.Beat1_Dim;
            RaiseBeat();
            ambientBefore = RenderSettings.ambientLight;
            dimming = true;
            RenderSettings.ambientLight = ambientBefore * dimFloor;
            yield return BeatWait(0);

            // ② 回忆绽放
            State = ReplayState.Beat2_Bloom;
            RaiseBeat();
            BuildLayeredScene(fragment, anchorPosition);
            yield return BeatWait(1);

            // ③ 重返当时
            State = ReplayState.Beat3_Return;
            RaiseBeat();
            if (fragment.diary != null && !string.IsNullOrEmpty(fragment.diary.text))
            {
                Debug.Log("[ReplaySequencer] ③ 重返当时：" + fragment.diary.date);
            }
            yield return BeatWait(2);

            // ④ 温柔收回
            State = ReplayState.Beat4_Settle;
            RaiseBeat();
            TeardownLayeredScene();
            RenderSettings.ambientLight = ambientBefore;
            dimming = false;
            yield return BeatWait(3);

            State = ReplayState.Done;
            RaiseBeat();
        }

        /// <summary>等待第 beatIndex 拍时长；skipRequested 时立即结束等待。</summary>
        private IEnumerator BeatWait(int beatIndex)
        {
            float duration = (beatDurations != null && beatIndex < beatDurations.Length)
                ? Mathf.Max(0.01f, beatDurations[beatIndex])
                : 0.5f;
            float deadline = Time.realtimeSinceStartup + duration;
            while (Time.realtimeSinceStartup < deadline && !skipRequested)
            {
                yield return null;
            }
        }

        private void BuildLayeredScene(MemoryFragment fragment, Vector3 anchorPosition)
        {
            TeardownLayeredScene();
            string imagePath = FindFirstImagePath(fragment);
            if (imagePath == null)
            {
                Debug.Log("[ReplaySequencer] ② 无分层素材，跳过展开");
                return;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(System.IO.File.ReadAllBytes(imagePath)))
            {
                Debug.LogWarning("[ReplaySequencer] ② 分层素材解码失败: " + imagePath);
                return;
            }

            layerRoot = new GameObject("ReplayLayeredPhoto");
            layerRoot.transform.position = anchorPosition + Vector3.up * 0.4f;
            Camera cam = Camera.main;
            if (cam != null)
            {
                layerRoot.transform.rotation = Quaternion.LookRotation(
                    layerRoot.transform.position - cam.transform.position);
            }
            LayeredPhoto layered = layerRoot.AddComponent<LayeredPhoto>();
            // V1 预案：无三层拆分素材时，单图三层 + 视差（退化模式，W2 后接 AI 抠图分层）
            layered.Build(texture, texture, texture);
        }

        private void TeardownLayeredScene()
        {
            if (layerRoot != null)
            {
                Destroy(layerRoot);
                layerRoot = null;
            }
        }

        private static string FindFirstImagePath(MemoryFragment fragment)
        {
            if (fragment.mediaItems == null) return null;
            for (int i = 0; i < fragment.mediaItems.Count; i++)
            {
                MediaItem item = fragment.mediaItems[i];
                if (item.kind != "image" || string.IsNullOrEmpty(item.path)) continue;
                string resolved = MediaResolver.ResolveExisting(item.path);
                if (resolved != null) return resolved;
            }
            return null;
        }

        private void RaiseBeat()
        {
            if (OnBeat != null) OnBeat(State);
        }

        private void OnDisable()
        {
            // 兜底复原（场景卸载/组件禁用时不能把环境光留在压暗态）
            if (dimming)
            {
                RenderSettings.ambientLight = ambientBefore;
                dimming = false;
            }
            TeardownLayeredScene();
        }
    }
}
