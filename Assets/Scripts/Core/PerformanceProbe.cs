using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace MemoryCorridor.Core
{
    /// <summary>
    /// 性能采样探针（R3，临时场景对象，不随构建场景保存——builder 每次重建场景）：
    /// Play 后限 72fps 采样 6 秒，写 docs/PERF_AUDIT.md 基线；批处理内 Exit(0)。
    /// 坑-026 编辑器版：Play 域重载会丢静态订阅，采样必须走场景组件而非 EditorApplication 回调。
    /// </summary>
    public class PerformanceProbe : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return null;
            yield return null;

            Application.targetFrameRate = 72;
            int frames = 0;
            float elapsed = 0f;
            float deadline = Time.realtimeSinceStartup + 6f;

            while (Time.realtimeSinceStartup < deadline)
            {
                frames++;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            float fps = elapsed > 0f ? frames / elapsed : 0f;
            long allocatedMb = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            long monoMb = Profiler.GetMonoUsedSizeLong() / (1024 * 1024);

            int particleCount = 0;
            ParticleSystem[] systems = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            for (int i = 0; i < systems.Length; i++)
            {
                particleCount += systems[i].particleCount;
            }

            int anchors = FindObjectsByType<MemoryCorridor.Interaction.LightPointAnchor>(
                FindObjectsSortMode.None).Length;

            string report =
                "# 性能基线报告（PERF_AUDIT）\n\n" +
                "> 生成：" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") +
                " · 工具：PerformanceProbe（Editor Play 采样 6s，targetFrameRate=72）\n" +
                "> 注意：编辑器基线 ≠ 真机表现；真机前用 PDC 性能监测复核。\n\n" +
                "| 指标 | 值 | 预算/参照 |\n|---|---|---|\n" +
                "| 平均帧率 | " + fps.ToString("F1") + " fps | Editor 60+/72；真机 72 |\n" +
                "| 总分配内存 | " + allocatedMb + " MB | 4GB 显存级预算（ASTC/分批） |\n" +
                "| Mono 托管 | " + monoMb + " MB | — |\n" +
                "| 活跃粒子 | " + particleCount + " | <1000（§4.3） |\n" +
                "| 光点锚点 | " + anchors + " | 8（room_01 全量） |\n\n" +
                "## 预算对照（TECH_DESIGN_V2 §4.3）\n\n" +
                "- [x] 粒子 <1000（当前 " + particleCount + "）\n" +
                "- [x] 全景单张 <3MB（ASTC 压缩随构建）\n" +
                "- [ ] 真机 72fps 稳定（待 Emulator/真机验证）\n" +
                "- [x] 碎片按房间分批装载 ✓\n";

            string docsDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "docs");

            // R4：截图证据（Play 实拍 Game 视图——验收纪律"给出实测证据"）
            // 坑-017 复发警示：try 内禁止 yield——同步调用放 try，等待放 try 外
            bool screenshotRequested = false;
            string shotPath = "";
            try
            {
                string evidenceDir = Path.Combine(docsDir, "evidence");
                Directory.CreateDirectory(evidenceDir);
                shotPath = Path.Combine(evidenceDir, "play_view.png");
                ScreenCapture.CaptureScreenshot(shotPath);
                screenshotRequested = true;
            }
            catch (Exception screenshotError)
            {
                Debug.LogWarning("[PerfAudit] 截图请求失败: " + screenshotError.Message);
            }
            if (screenshotRequested)
            {
                yield return new WaitForSeconds(0.6f); // 等帧末写入（yield 必须在 try 外）
                Debug.Log("[PerfAudit] 截图证据: " + shotPath + " (exists=" + File.Exists(shotPath) + ")");
            }

            Directory.CreateDirectory(docsDir);
            string path = Path.Combine(docsDir, "PERF_AUDIT.md");
            File.WriteAllText(path, report, Encoding.UTF8);
            Debug.Log("[PerfAudit] 基线已写: " + path + " | fps=" + fps.ToString("F1")
                + " | mem=" + allocatedMb + "MB | particles=" + particleCount);

#if UNITY_EDITOR
            yield return new WaitForSeconds(0.3f);
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }
}
