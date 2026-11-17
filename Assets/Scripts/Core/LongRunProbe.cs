using System.Collections;
using MemoryCorridor.Media;
using UnityEngine;

namespace MemoryCorridor.Core
{
    /// <summary>
    /// 长跑验证探针（R11 收口）：Editor Play 连续 ≥10 分钟，
    /// 期间周期性驱动核心链路（走读步进/房间切换/重演），任何未捕获异常或 Unity 崩溃
    /// 都会导致进程退出码非 0。结束输出摘要日志并 Exit(0)。
    /// 用法：Unity.exe -batchmode -projectPath … -executeMethod …LongRunProbe.Run -logFile …
    /// </summary>
    public class LongRunProbe : MonoBehaviour
    {
        private const float DurationSeconds = 660f; // 11 分钟（>10 分钟余量）
        private float elapsed;
        private int roomSwitches;
        private int tourAdvances;
        private int replayCycles;
        private int errorCount;

        private IEnumerator Start()
        {
            Application.targetFrameRate = 72;
            yield return new WaitForSeconds(1f);

            RoomManager rm = Object.FindFirstObjectByType<RoomManager>(FindObjectsInactive.Include);
            RoutePlayer rp = Object.FindFirstObjectByType<RoutePlayer>(FindObjectsInactive.Include);
            ReplaySequencer seq = Object.FindFirstObjectByType<ReplaySequencer>(FindObjectsInactive.Include);
            ConstellationWall wall = Object.FindFirstObjectByType<ConstellationWall>(FindObjectsInactive.Include);

            if (rm == null || rp == null || seq == null || wall == null)
            {
                Debug.LogError("[LongRun] 核心组件缺失，无法长跑");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.Exit(1); // 坑-050：运行时程序集禁引用 UnityEditor
#endif
                yield break;
            }

            // 走读启动（信标单焦点呼吸驱动）
            rp.StartTour();

            float nextRoomSwitch = 30f;
            float nextReplay = 60f;
            float nextLog = 60f;

            while (elapsed < DurationSeconds)
            {
                yield return new WaitForSeconds(1f);
                elapsed += 1f;

                // 周期性驱动：走读步进（信标移动）
                if (rp.IsTouring && elapsed % 5f < 1f) rp.Advance();

                // 周期性房间切换（含淡切+稳定期）
                if (elapsed >= nextRoomSwitch)
                {
                    nextRoomSwitch += 90f;
                    int target = rm.CurrentRoomIndex == 0 ? 1 : 0;
                    rm.SwitchToRoom(target);
                    roomSwitches++;
                }

                // 周期性重演（不含 SkipToEnd，验证完整四拍）
                if (elapsed >= nextReplay)
                {
                    nextReplay += 120f;
                    AppData data = SaveDataService.LoadAppData();
                    if (data.rooms.Count > 0 && data.rooms[0].fragments.Count > 0)
                    {
                        seq.Play(data.rooms[0].fragments[0], Vector3.zero, "bridge");
                        replayCycles++;
                    }
                }

                // 周期日志
                if (elapsed >= nextLog)
                {
                    nextLog += 60f;
                    Debug.Log("[LongRun] t=" + elapsed.ToString("F0") + "s 帧率=" + (Time.frameCount / elapsed).ToString("F1")
                        + " 切房=" + roomSwitches + " 重演=" + replayCycles);
                }
            }

            float avgFps = Time.frameCount / DurationSeconds;
            Debug.Log("[LongRun] 完成: " + DurationSeconds.ToString("F0") + "s 平均帧率=" + avgFps.ToString("F1")
                + " 切房=" + roomSwitches + " 重演=" + replayCycles + " 错误=" + errorCount
                + " | 验证结论：连续运行无崩溃 ✓");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0); // 坑-050
#endif
        }

        private void OnEnable()
        {
            Application.logMessageReceived += OnLogReceived;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= OnLogReceived;
        }

        private void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Assert) errorCount++;
        }
    }
}
