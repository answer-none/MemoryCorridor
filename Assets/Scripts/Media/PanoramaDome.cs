using MemoryCorridor.Core;
using UnityEngine;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 全景穹顶：倒置球面 + 等距柱状投影贴图（MemoryCorridor/PanoramaDome shader）。
    /// Start 从 appdata.json 房间 panoramaPath 经 MediaResolver 加载（真实数据流）。
    /// 房间切换时由 RoomManager 调 SetPanorama（W3）。
    /// 坑-022：URP 下 Skybox/Panorama 内置 shader 不可靠（Shader.Find 为 null），弃用。
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class PanoramaDome : MonoBehaviour
    {
        private void Start()
        {
            AppData data = SaveDataService.LoadAppData();
            string relative = (data != null && data.rooms != null && data.rooms.Count > 0)
                ? data.rooms[0].panoramaPath
                : null;

            string resolved = MediaResolver.ResolveExisting(relative);
            if (resolved == null)
            {
                Debug.LogWarning("[PanoramaDome] 全景图缺失: " + MediaResolver.DescribeMiss(relative));
                return;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(System.IO.File.ReadAllBytes(resolved)))
            {
                Debug.LogWarning("[PanoramaDome] 全景解码失败: " + resolved);
                return;
            }

            GetComponent<MeshRenderer>().material.mainTexture = texture;
            Debug.Log("[PanoramaDome] 已加载全景: " + resolved);
        }

        /// <summary>运行时切换房间全景（W3 房间切换用）。</summary>
        public void SetPanorama(Texture2D texture)
        {
            GetComponent<MeshRenderer>().material.mainTexture = texture;
        }
    }
}
