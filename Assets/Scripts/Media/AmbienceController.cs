using MemoryCorridor.Core;
using UnityEngine;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 房间环境音控制器：2D 循环底噪，音量取自 appdata.json 的 settings.ambientVolume。
    /// 每房间一条（房间切换时由 RoomManager 换 clip，W3）。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AmbienceController : MonoBehaviour
    {
        private void Start()
        {
            AudioSource source = GetComponent<AudioSource>();
            source.loop = true;
            source.spatialBlend = 0f; // 房间底噪不空间化
            source.playOnAwake = true;
            source.Play();

            AppData data = SaveDataService.LoadAppData();
            if (data != null && data.settings != null)
            {
                source.volume = Mathf.Clamp01(data.settings.ambientVolume);
            }
        }
    }
}
