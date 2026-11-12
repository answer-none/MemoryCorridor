using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MemoryCorridor.EditorTools
{
    /// <summary>
    /// 性能基线审计（R3）：Editor Play 由场景内 PerformanceProbe 采样 6 秒，
    /// 产出 docs/PERF_AUDIT.md；probe 临时创建不保存（不进构建场景）。
    /// 坑-026 编辑器版：Play 域重载丢静态订阅——采样必须走场景组件。
    /// 用法：Unity.exe -batchmode -projectPath … -executeMethod …PerformanceAudit.Run -logFile …
    /// </summary>
    public static class PerformanceAudit
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EditorScene.unity");
            GameObject probeGo = new GameObject("PerformanceProbe");
            probeGo.AddComponent<MemoryCorridor.Core.PerformanceProbe>();
            // 不 SaveScene：probe 不落盘，构建场景保持干净
            EditorApplication.isPlaying = true;
        }
    }
}
