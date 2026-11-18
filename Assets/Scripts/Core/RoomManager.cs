using System;
using System.Collections;
using System.Collections.Generic;
using MemoryCorridor.Interaction;
using MemoryCorridor.Media;
using UnityEngine;

namespace MemoryCorridor.Core
{
    /// <summary>
    /// 房间管理（TECH_DESIGN_V2 §5.10）：全景穹顶贴图 + 环境音 + 光点随房间装载/卸载。
    /// 转场淡入淡出由 W4 全屏暗幕承担；本层负责数据与对象生命周期。
    /// 坑-025：桥接器/走读可能在 Start 顺序上晚于本组件，光点命名固定 LP_{fragmentId}。
    /// </summary>
    public class RoomManager : MonoBehaviour
    {
        [SerializeField] private GameObject lightPointPrefab;
        [SerializeField] private FadeController fadeController;
        [SerializeField] private AudioSource ambientSource;
        [SerializeField] private AudioClip[] ambientClips; // 与房间索引一一对应，可缺
        [SerializeField] private SceneFader fader;

        public int CurrentRoomIndex { get; private set; } = -1;
        public List<RoomData> Rooms { get; private set; }
        public int SpawnedAnchorCount { get; private set; }

        /// <summary>R17-1：运行时主题 id（用户选择优先于房间 theme；default/rain）。</summary>
        public string ActiveThemeId { get; private set; } = "default";

        /// <summary>房间切换完成事件（RoutePlayer 订阅停止走读）。</summary>
        public event Action<int> OnRoomSwitched;

        private readonly List<GameObject> spawnedAnchors = new List<GameObject>();

        /// <summary>编辑器搭建注入引用。</summary>
        public void Configure(GameObject lightPointPrefabRef, FadeController fadeRef,
            AudioSource ambientSourceRef, AudioClip[] ambientClipsRef, SceneFader faderRef)
        {
            lightPointPrefab = lightPointPrefabRef;
            fadeController = fadeRef;
            ambientSource = ambientSourceRef;
            ambientClips = ambientClipsRef;
            fader = faderRef;
        }

        private void Start()
        {
            AppData data = SaveDataService.LoadAppData();
            Rooms = (data != null && data.rooms != null) ? data.rooms : new List<RoomData>();
            if (data != null && data.settings != null && !string.IsNullOrEmpty(data.settings.activeThemeId))
            {
                ActiveThemeId = data.settings.activeThemeId; // R17-1：恢复上次选择
            }
            SwitchToRoom(0);
        }

        /// <summary>切换房间：卸载当前光点、换穹顶与环境音、装载目标房间光点。</summary>
        public bool SwitchToRoom(int index)
        {
            if (Rooms == null || index < 0 || index >= Rooms.Count) return false;

            UnloadCurrentAnchors();
            CurrentRoomIndex = index;
            RoomData room = Rooms[index];
            SpawnRoomAnchors(room);
            ApplyDome(room);
            ApplyAmbience(index);
            ApplyTheme(room);

            if (OnRoomSwitched != null) OnRoomSwitched(index);
            Debug.Log("[RoomManager] 房间切换 → " + room.title + "（" + SpawnedAnchorCount + " 光点）");
            return true;
        }

        /// <summary>R16-3：主题色调落地——环境光+雾色+指数雾密度按运行时选择（用户选择优先于房间固有 theme）。</summary>
        private void ApplyTheme(RoomData room)
        {
            ThemeColors.ThemeSpec spec = ThemeColors.For(ActiveThemeId);
            RenderSettings.ambientLight = spec.Ambient;
            RenderSettings.fog = true;
            RenderSettings.fogColor = spec.Fog;
            RenderSettings.fogMode = FogMode.Exponential; // R17：密度雾（主题间雾密度差异可见）
            RenderSettings.fogDensity = spec.FogDensity;
        }

        /// <summary>R17-1：直接设置主题（探针/引导用；与 CycleTheme 同样即时生效+持久化）。</summary>
        public void SetTheme(string themeId, bool withFade)
        {
            if (withFade && fader != null) StartCoroutine(CycleThemeWithFade(themeId));
            else SetThemeImmediate(themeId);
        }

        /// <summary>
        /// R17-1：循环切换主题（default→rain→default），即时生效并持久化；
        /// withFade=true 时包 0.3s 全屏淡入淡出避免闪跳（设置面板入口用）。
        /// </summary>
        public string CycleTheme(bool withFade)
        {
            string next = ThemeColors.NextOf(ActiveThemeId);
            if (withFade && fader != null)
            {
                StartCoroutine(CycleThemeWithFade(next));
            }
            else
            {
                SetThemeImmediate(next);
            }
            return ActiveThemeId;
        }

        private System.Collections.IEnumerator CycleThemeWithFade(string next)
        {
            yield return fader.FadeOut(0.15f);
            SetThemeImmediate(next);
            yield return fader.FadeIn(0.15f); // 合计 0.3s：避免主题参数硬跳
        }

        private void SetThemeImmediate(string themeId)
        {
            ActiveThemeId = themeId;
            ThemeColors.ThemeSpec spec = ThemeColors.For(themeId);
            RenderSettings.ambientLight = spec.Ambient;
            RenderSettings.fogColor = spec.Fog;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = spec.FogDensity;

            AppData data = SaveDataService.LoadAppData();
            if (data != null && data.settings != null)
            {
                data.settings.activeThemeId = themeId;
                SaveDataService.SaveAppData(data); // 持久化（切房/重启保持）
            }
            Debug.Log("[RoomManager] 主题切换 → " + themeId);
        }

        public RoomData CurrentRoom()
        {
            if (Rooms == null || CurrentRoomIndex < 0 || CurrentRoomIndex >= Rooms.Count) return null;
            return Rooms[CurrentRoomIndex];
        }

        /// <summary>带淡入淡出的房间切换（§5.3 零硬切；无 fader 时退化为直切）。</summary>
        /// <summary>带淡入淡出的房间切换（§5.3 零硬切；无 fader 时退化为直切）。
        /// R12 迷失防护：淡入完成后 0.5s 观感稳定期，期间玩家确认方位（期间协程未返回=交互未开放）。</summary>
        public IEnumerator SwitchToRoomWithFade(int index)
        {
            if (fader != null) yield return fader.FadeOut(0.3f);
            SwitchToRoom(index);
            if (fader != null) yield return fader.FadeIn(0.3f);
            yield return new WaitForSeconds(0.5f); // R12 空间迷失防护：观感稳定期
        }

        private void UnloadCurrentAnchors()
        {
            for (int i = 0; i < spawnedAnchors.Count; i++)
            {
                if (spawnedAnchors[i] != null) Destroy(spawnedAnchors[i]);
            }
            spawnedAnchors.Clear();
            SpawnedAnchorCount = 0;
        }

        private void SpawnRoomAnchors(RoomData room)
        {
            if (lightPointPrefab == null || room.fragments == null) return;

            for (int i = 0; i < room.fragments.Count; i++)
            {
                MemoryFragment fragment = room.fragments[i];
                GameObject instance = Instantiate(lightPointPrefab, fragment.anchor.GetPosition(),
                    fragment.anchor.GetRotation());
                instance.name = "LP_" + fragment.id; // 坑-025：稳定命名供测试/调试定位
                LightPointAnchor anchor = instance.GetComponent<LightPointAnchor>();
                if (anchor != null)
                {
                    anchor.BindFragment(fragment.id, fragment.visibility == "private", fragment.type);
                    if (fadeController != null) fadeController.Apply(fragment, anchor);
                }
                spawnedAnchors.Add(instance);
            }
            SpawnedAnchorCount = spawnedAnchors.Count;
            SettingsService.ApplyAll(); // R5：spawn 后应用持久化显隐设置（光点标记）
        }

        private void ApplyDome(RoomData room)
        {
            PanoramaDome dome = FindFirstLive<PanoramaDome>();
            if (dome == null || string.IsNullOrEmpty(room.panoramaPath)) return;

            string resolved = MediaResolver.ResolveExisting(room.panoramaPath);
            if (resolved == null)
            {
                Debug.LogWarning("[RoomManager] 全景缺失: " + MediaResolver.DescribeMiss(room.panoramaPath));
                return;
            }
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (texture.LoadImage(System.IO.File.ReadAllBytes(resolved)))
            {
                dome.SetPanorama(texture);
            }
        }

        private void ApplyAmbience(int index)
        {
            if (ambientSource == null || ambientClips == null) return;
            if (index < 0 || index >= ambientClips.Length || ambientClips[index] == null) return;
            ambientSource.clip = ambientClips[index];
            if (!ambientSource.isPlaying) ambientSource.Play();
        }

        private static T FindFirstLive<T>() where T : UnityEngine.Object
        {
            T[] found = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return found.Length > 0 ? found[0] : null;
        }
    }
}
