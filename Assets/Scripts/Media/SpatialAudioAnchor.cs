using UnityEngine;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 空间音频锚点（TECH_DESIGN_V2 §5.1 "先闻其声"）：
    /// 进入 narrRadius 内以低音量隐约响起并随靠近线性增强，超过 1.5 倍半径暂停；
    /// SpatialBlend=1 走 3D 衰减，音量分级由 AudioMixer/设置层叠加。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SpatialAudioAnchor : MonoBehaviour
    {
        [SerializeField] private float narrRadius = 3f;
        [SerializeField] private float farVolume = 0.2f;

        private AudioSource audioSource;
        private Transform listenerTransform;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.loop = true;
        }

        private void Update()
        {
            if (audioSource == null || audioSource.clip == null) return;

            if (listenerTransform == null)
            {
                Camera cam = Camera.main;
                if (cam != null) listenerTransform = cam.transform;
                return;
            }

            float dist = Vector3.Distance(listenerTransform.position, transform.position);
            float maxDist = narrRadius * 1.5f;
            if (dist > maxDist)
            {
                if (audioSource.isPlaying) audioSource.Pause();
                return;
            }

            if (!audioSource.isPlaying) audioSource.Play();
            float t = Mathf.Clamp01(1f - dist / maxDist);
            audioSource.volume = Mathf.Lerp(farVolume * 0.5f, 1f, t);
        }

        /// <summary>绑定音频片段（回访旁白/环境录音）。</summary>
        public void SetClip(AudioClip clip)
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            audioSource.clip = clip;
        }
    }
}
