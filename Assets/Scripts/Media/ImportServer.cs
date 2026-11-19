using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using MemoryCorridor.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 局域网素材导入服务（TECH_DESIGN_V2 §5.2）：手机浏览器上传图片/音频、提交日记，
    /// 落盘 PersistentDataPath/Media/&lt;fragmentId&gt;/。
    /// 端口 8899；Windows 上 "http://+:8899/" 需管理员或 URLACL，失败自动回退本机回环（坑-016）。
    /// 线程模型：监听与文件 IO 在后台线程，状态经 ConcurrentQueue 回主线程打印。
    /// 注意：Android/IL2CPP 兼容性在 W2 第 1 天用 Emulator 冒烟（方案 §6 修订 6）。
    /// </summary>
    public class ImportServer : MonoBehaviour
    {
        [SerializeField] private int port = 8899;

        private HttpListener listener;
        private Thread listenerThread;
        private volatile bool running;
        private readonly ConcurrentQueue<string> statusQueue = new ConcurrentQueue<string>();

        private void Start()
        {
            SaveDataService.WarmCache(); // 坑-024：主线程预热路径缓存，后台线程只读缓存
            running = true;
            listenerThread = new Thread(ListenLoop);
            listenerThread.IsBackground = true;
            listenerThread.Start();
        }

        private void OnDestroy()
        {
            running = false;
            try
            {
                if (listener != null) listener.Stop();
            }
            catch (Exception)
            {
                // 关闭时 listener 可能已死，忽略
            }
        }

        private void Update()
        {
            string status;
            while (statusQueue.TryDequeue(out status))
            {
                Debug.Log("[ImportServer] " + status);
            }
        }

        private void ListenLoop()
        {
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add("http://+:" + port + "/");
                try
                {
                    listener.Start();
                    statusQueue.Enqueue("监听中(全接口) http://0.0.0.0:" + port + "/");
                }
                catch (HttpListenerException)
                {
                    listener = new HttpListener();
                    listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
                    listener.Start();
                    statusQueue.Enqueue("无 + 前缀权限，回退本机回环 http://127.0.0.1:" + port + "/");
                }

                while (running)
                {
                    HttpListenerContext ctx = listener.GetContext();
                    ThreadPool.QueueUserWorkItem(delegate { HandleContext(ctx); });
                }
            }
            catch (Exception e)
            {
                if (running) statusQueue.Enqueue("监听线程退出: " + e.Message);
            }
        }

        private void HandleContext(HttpListenerContext ctx)
        {
            try
            {
                string path = ctx.Request.Url.AbsolutePath;
                if (ctx.Request.HttpMethod == "GET" && (path == "/" || path == "/index.html"))
                {
                    WriteResponse(ctx, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(BuildIndexPage()));
                    return;
                }
                if (ctx.Request.HttpMethod == "POST" && path == "/upload")
                {
                    HandleUpload(ctx);
                    return;
                }
                if (ctx.Request.HttpMethod == "POST" && path == "/diary")
                {
                    HandleDiary(ctx);
                    return;
                }
                WriteResponse(ctx, 404, "text/plain", Encoding.UTF8.GetBytes("not found"));
            }
            catch (Exception e)
            {
                statusQueue.Enqueue("处理 " + ctx.Request.HttpMethod + " " + ctx.Request.Url.AbsolutePath
                    + " 失败: " + e.GetType().Name + ": " + e.Message
                    + (e.InnerException != null ? " | inner: " + e.InnerException.Message : ""));
                try { WriteResponse(ctx, 500, "text/plain", Encoding.UTF8.GetBytes("error: " + e.Message)); }
                catch (Exception) { /* 响应已断开 */ }
            }
        }

        private void HandleUpload(HttpListenerContext ctx)
        {
            string fragmentId = ctx.Request.QueryString["fragmentId"];
            string fileName = ctx.Request.QueryString["name"];
            if (string.IsNullOrEmpty(fragmentId) || string.IsNullOrEmpty(fileName))
            {
                WriteResponse(ctx, 400, "text/plain", Encoding.UTF8.GetBytes("missing fragmentId/name"));
                return;
            }

            string safeName = Path.GetFileName(fileName);
            string dir = Path.Combine(SaveDataService.MediaRoot, fragmentId);
            Directory.CreateDirectory(dir);
            string fullPath = Path.Combine(dir, safeName);

            using (Stream input = ctx.Request.InputStream)
            using (FileStream output = new FileStream(fullPath, FileMode.Create))
            {
                byte[] buffer = new byte[8192];
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                }
            }

            statusQueue.Enqueue($"收到 {fragmentId}/{safeName} ({new FileInfo(fullPath).Length} bytes)");
            WriteResponse(ctx, 200, "text/plain", Encoding.UTF8.GetBytes("ok"));
        }

        private void HandleDiary(HttpListenerContext ctx)
        {
            string body;
            using (StreamReader reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
            {
                body = reader.ReadToEnd();
            }

            DiaryPayload payload = JsonConvert.DeserializeObject<DiaryPayload>(body);
            if (payload == null || string.IsNullOrEmpty(payload.fragmentId))
            {
                WriteResponse(ctx, 400, "text/plain", Encoding.UTF8.GetBytes("bad diary payload"));
                return;
            }

            AppData data = SaveDataService.LoadAppData();
            MemoryFragment target = FindFragment(data, payload.fragmentId);
            if (target == null)
            {
                WriteResponse(ctx, 404, "text/plain", Encoding.UTF8.GetBytes("fragment not found: " + payload.fragmentId));
                return;
            }

            // R21 P2：统一走 MemoryCardEditService（校验/空标题兜底/事件刷新面板）
            var edit = MemoryCardEditService.Apply(data, payload.fragmentId,
                payload.title, payload.date ?? DateTime.Now.ToString("yyyy-MM-dd"), payload.text ?? "");
            if (!edit.Ok)
            {
                WriteResponse(ctx, 400, "text/plain", Encoding.UTF8.GetBytes("edit rejected: " + edit.Message));
                return;
            }
            statusQueue.Enqueue($"日记已并入 {payload.fragmentId}（{edit.AppliedTitle}）");
            WriteResponse(ctx, 200, "text/plain", Encoding.UTF8.GetBytes("ok"));
        }

        private static MemoryFragment FindFragment(AppData data, string fragmentId)
        {
            if (data == null || data.rooms == null) return null;
            for (int r = 0; r < data.rooms.Count; r++)
            {
                RoomData room = data.rooms[r];
                if (room.fragments == null) continue;
                for (int f = 0; f < room.fragments.Count; f++)
                {
                    if (room.fragments[f].id == fragmentId) return room.fragments[f];
                }
            }
            return null;
        }

        private static void WriteResponse(HttpListenerContext ctx, int code, string contentType, byte[] bytes)
        {
            ctx.Response.StatusCode = code;
            ctx.Response.ContentType = contentType;
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.OutputStream.Close();
        }

        private static string BuildIndexPage()
        {
            // 极简单页：fetch 原始字节上传，规避 multipart 解析
            return "<!doctype html><html><head><meta charset='utf-8'>"
                + "<meta name='viewport' content='width=device-width,initial-scale=1'>"
                + "<title>回响长廊 · 导入</title></head>"
                + "<body style='font-family:sans-serif;background:#2B2436;color:#E8A24B;padding:1em'>"
                + "<h2>回响长廊 · 素材导入</h2>"
                + "<p>fragmentId: <input id='fid' value='frag_01'></p>"
                + "<p>图片 <input type='file' id='img' accept='image/*'></p>"
                + "<p>录音 <input type='file' id='aud' accept='audio/*'></p>"
                + "<p>日记 <input id='dd' placeholder='日期 2026-06-18'><br>"
                + "<textarea id='dt' rows='4' style='width:100%'></textarea></p>"
                + "<button onclick='up()'>上传文件</button> <button onclick='diary()'>提交日记</button>"
                + "<pre id='log'></pre>"
                + "<script>"
                + "function log(m){document.getElementById('log').textContent+=m+'\\n'}"
                + "async function sendFile(el,kind){var f=el.files[0];if(!f){log('未选文件');return}"
                + "var fid=document.getElementById('fid').value;"
                + "var r=await fetch('/upload?fragmentId='+encodeURIComponent(fid)+'&name='+encodeURIComponent(f.name),{method:'POST',body:f});"
                + "log(kind+' '+f.name+': '+await r.text())}"
                + "function up(){sendFile(document.getElementById('img'),'图片');sendFile(document.getElementById('aud'),'录音')}"
                + "async function diary(){var fid=document.getElementById('fid').value;"
                + "var r=await fetch('/diary',{method:'POST',body:JSON.stringify({fragmentId:fid,"
                + "date:document.getElementById('dd').value,text:document.getElementById('dt').value})});"
                + "log('日记: '+await r.text())}"
                + "</script></body></html>";
        }

        [Serializable]
        private class DiaryPayload
        {
            public string fragmentId;
            public string title; // R21
            public string date;
            public string text;
        }
    }
}
