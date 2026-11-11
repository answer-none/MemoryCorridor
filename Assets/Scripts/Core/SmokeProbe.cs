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

            // --- ImportServer HTTP 冒烟（W2 第 1 天硬节点，批处理替代 Emulator GUI） ---
            if (pass)
            {
                yield return new WaitForSecondsRealtime(0.6f); // 等监听线程就绪
                // 同步 HTTP 无 yield，可安全包 try/catch（坑-017）
                try
                {
                    TestImportServer();
                    Debug.Log("[SmokeProbe] ImportServer HTTP 冒烟 OK（GET 首页/POST 上传落盘/POST 日记入档）");
                }
            catch (System.Exception e)
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
                Debug.LogError("[SmokeProbe] FAIL: ImportServer HTTP 冒烟异常 " + detail);
                pass = false;
            }
            }

            Debug.Log(pass ? "[SmokeProbe] PASS: XRI 装配/悬停预览/选中/褪色/导入服务 全部通过" : "[SmokeProbe] 结果: FAIL");

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

        /// <summary>ImportServer 三连测：GET 首页、POST 上传落盘、POST 日记写档。失败抛异常。</summary>
        private static void TestImportServer()
        {
            const string baseUrl = "http://127.0.0.1:8899";

            // 1) GET 首页
            System.Net.HttpWebResponse home = Request("GET", baseUrl + "/", null, null);
            if (home.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new System.Exception("首页状态码 " + (int)home.StatusCode);
            }
            home.Close();

            // 2) POST 上传（伪 JPEG 字节流；服务端不做格式校验，落盘即验）
            byte[] fakeJpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
            System.Net.HttpWebResponse up = Request("POST",
                baseUrl + "/upload?fragmentId=frag_01&name=smoke_test.jpg",
                "application/octet-stream", fakeJpeg);
            if (up.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new System.Exception("上传状态码 " + (int)up.StatusCode);
            }
            up.Close();
            string saved = System.IO.Path.Combine(Application.persistentDataPath,
                System.IO.Path.Combine("Media", System.IO.Path.Combine("frag_01", "smoke_test.jpg")));
            if (!System.IO.File.Exists(saved))
            {
                throw new System.Exception("上传文件未落盘: " + saved);
            }

            // 3) POST 日记 → appdata.json 更新
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
