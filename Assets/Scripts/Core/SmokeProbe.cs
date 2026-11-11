using System.Collections;
using MemoryCorridor.Interaction;
using UnityEngine;

namespace MemoryCorridor.Core
{
    /// <summary>
    /// W1 自动冒烟探针（仅 Editor Play 使用）：验证
    /// ① 悬停 0.3s 触发 OnHoverPreview；② Select() 触发 OnSelected；
    /// ③ 300 天样例光点褪色系数低于新鲜光点（FadeController）。
    /// batchmode 下 PASS→Exit(0)，FAIL→Exit(1)；60s 看门狗防挂死。
    /// 坑-017：协程 yield 不得位于 try/catch 内（CS1626），断言代码不包 try。
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
            yield return null; // 等全部 Start 完成（含 FadeController/XRIAnchorBridge 从盘装配）

            LightPointAnchor fresh = FindAnchor("LP_fresh_1");
            LightPointAnchor faded = FindAnchor("LP_faded_3");
            bool pass = true;

            if (fresh == null || faded == null)
            {
                Debug.LogError("[SmokeProbe] FAIL: 演示锚点缺失 (fresh=" + (fresh == null) + " faded=" + (faded == null) + ")");
                pass = false;
            }

            // --- XRI 装配断言（W1 追加） ---
            if (pass)
            {
                Unity.XR.CoreUtils.XROrigin origin = FindFirst<Unity.XR.CoreUtils.XROrigin>();
                // 坑-021：无 XR 设备时 XRInputModalityManager 会禁用控制器层级——
                // 装配断言必须含未激活对象（交互功能验证属 GUI/模拟器步骤）
                UnityEngine.XR.Interaction.Toolkit.XRRayInteractor[] rays =
                    UnityEngine.Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.XRRayInteractor>(
                        UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
                UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable[] interactables =
                    FindAll<UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable>();
                MemoryCorridor.Interaction.XRIAnchorBridge bridge =
                    FindFirst<MemoryCorridor.Interaction.XRIAnchorBridge>();

                if (origin == null)
                {
                    Debug.LogError("[SmokeProbe] FAIL: 缺少 XR Origin");
                    pass = false;
                }
                if (rays.Length < 1)
                {
                    Debug.LogError("[SmokeProbe] FAIL: 缺少 XRRayInteractor");
                    pass = false;
                }
                if (interactables.Length < 3 || bridge == null)
                {
                    Debug.LogError("[SmokeProbe] FAIL: 光点 XRSimpleInteractable<3 或缺桥接器 (interactables="
                        + interactables.Length + " bridge=" + (bridge == null) + ")");
                    pass = false;
                }
                if (pass)
                {
                    Debug.Log("[SmokeProbe] XRI 装配 OK: origin/ray×" + rays.Length
                        + "/interactable×" + interactables.Length + "/bridge（rays 含未激活：无设备批处理下控制器由 ModalityManager 禁用，预期）");
                }
            }

            if (pass)
            {
                string previewId = "";
                string selectedId = "";
                fresh.OnHoverPreview += delegate (string id) { previewFired = true; previewId = id; };
                fresh.OnSelected += delegate (string id) { selectedFired = true; selectedId = id; };

                fresh.SetHighlight(true);
                // 坑-018：batchmode 帧率不设限，按真实时间预算等待而非帧数
                float deadline = Time.realtimeSinceStartup + 2f;
                while (!previewFired && Time.realtimeSinceStartup < deadline)
                {
                    fresh.TickHover();
                    yield return null;
                }
                if (!previewFired)
                {
                    Debug.LogError("[SmokeProbe] FAIL: 悬停预览 0.3s 未触发");
                    pass = false;
                }
                else
                {
                    Debug.Log("[SmokeProbe] 悬停预览 OK (fragment=" + previewId + ")");
                }

                fresh.Select();
                if (!selectedFired)
                {
                    Debug.LogError("[SmokeProbe] FAIL: Select() 未触发 OnSelected");
                    pass = false;
                }
                else
                {
                    Debug.Log("[SmokeProbe] 选中 OK (fragment=" + selectedId + ")");
                }

                float fadeFresh = fresh.CurrentFade;
                float fadeFaded = faded.CurrentFade;
                if (fadeFaded >= fadeFresh)
                {
                    Debug.LogError("[SmokeProbe] FAIL: 褪色差异无效 fresh=" + fadeFresh + " faded=" + fadeFaded);
                    pass = false;
                }
                else
                {
                    Debug.Log("[SmokeProbe] 褪色差异 OK: fresh=" + fadeFresh.ToString("F2")
                        + " faded=" + fadeFaded.ToString("F2"));
                }
            }

            Debug.Log(pass ? "[SmokeProbe] PASS: XRI 装配/悬停预览/选中/褪色 全部通过" : "[SmokeProbe] 结果: FAIL");

#if UNITY_EDITOR
            yield return new WaitForSeconds(0.3f);
            UnityEditor.EditorApplication.Exit(pass ? 0 : 1);
#endif
        }

        private static T FindFirst<T>() where T : UnityEngine.Object
        {
            T[] found = UnityEngine.Object.FindObjectsByType<T>(UnityEngine.FindObjectsSortMode.None);
            return found.Length > 0 ? found[0] : null;
        }

        private static T[] FindAll<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsByType<T>(UnityEngine.FindObjectsSortMode.None);
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(60f);
            Debug.LogError("[SmokeProbe] WATCHDOG: 60s 超时强制退出(1)");
            UnityEditor.EditorApplication.Exit(1);
        }

        private static LightPointAnchor FindAnchor(string objectName)
        {
            GameObject go = GameObject.Find(objectName);
            if (go == null) return null;
            return go.GetComponent<LightPointAnchor>();
        }
    }
}
