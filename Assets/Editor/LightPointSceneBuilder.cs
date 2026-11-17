using System;
using System.Collections.Generic;
using System.IO;
using MemoryCorridor.Core;
using MemoryCorridor.Interaction;
using MemoryCorridor.Media;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace MemoryCorridor.EditorTools
{
    /// <summary>
    /// W2 场景搭建器 v4：两房间演示（奶奶的客厅/老院子）+ XRI 装配 + 星尘信标 +
    /// RoomManager/RoutePlayer/ReplaySequencer/记忆面板/导入服务，幂等可重跑。
    /// 用法：Unity.exe -batchmode -projectPath … -executeMethod …BuildAndSmoke -logFile …（不传 -quit）。
    /// </summary>
    public static class LightPointSceneBuilder
    {
        private static readonly Color AmberGlow = new Color(0.91f, 0.64f, 0.29f);
        private const string RigPrefabPath = "Assets/Samples/XR Interaction Toolkit/2.5.4/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        private const string SimulatorPrefabPath = "Assets/Samples/XR Interaction Toolkit/2.5.4/XR Device Simulator/XR Device Simulator.prefab";

        [MenuItem("MemoryCorridor/W2 搭建演示场景")]
        public static void BuildAndSmoke()
        {
            BuildPrefabAndScene();
            VerifySaveRoundtrip();
            Debug.Log("[SceneBuilder] 搭建完成，进入 Play 冒烟（SmokeProbe 断言后自动退出）…");
            EditorApplication.isPlaying = true;
        }

        private static void BuildPrefabAndScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials"); // 坑-023

            // 坑-037：TMP Essentials 自动导入（根治 GUI 首次 Play 的模态弹窗卡死）
            try
            {
                if (!AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
                {
                    string tmpPkgRoot = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                        "Library", "PackageCache");
                    string[] candidates = Directory.GetFiles(tmpPkgRoot,
                        "TMP Essential Resources.unitypackage", SearchOption.AllDirectories);
                    if (candidates.Length > 0)
                    {
                        AssetDatabase.ImportPackage(candidates[0], false); // 静默导入
                        Debug.Log("[SceneBuilder] TMP Essentials 已静默导入");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SceneBuilder] TMP Essentials 导入跳过: " + e.Message);
            }

            // 1) 光点辉光材质 + 预制体（球体+光晕外球+点光+音源+低通+锚点+XRI可交互体）
            Material glowMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            glowMat.EnableKeyword("_EMISSION");
            glowMat.SetColor("_BaseColor", AmberGlow * 0.4f);
            glowMat.SetColor("_EmissionColor", AmberGlow * 2f);
            AssetDatabase.CreateAsset(glowMat, "Assets/Prefabs/LightPointGlow.mat");
            Shader glowShellShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/Sprite3D.shader");

            GameObject proto = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            proto.name = "LightPoint";
            MeshRenderer protoRenderer = proto.GetComponent<MeshRenderer>();
            protoRenderer.sharedMaterial = glowMat;
            Light protoLight = proto.AddComponent<Light>();
            protoLight.type = LightType.Point;
            protoLight.range = 3f;
            protoLight.color = AmberGlow;
            protoLight.intensity = 2f;
            proto.AddComponent<AudioSource>();
            proto.AddComponent<AudioLowPassFilter>();
            LightPointAnchor protoAnchor = proto.AddComponent<LightPointAnchor>();
            protoAnchor.Configure(protoRenderer, protoLight);
            protoAnchor.SetSelectChime(AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Audio/sfx_lightpoint_chime.wav")); // R1 E5：预制体级注入，实例继承

            if (glowShellShader != null)
            {
                // R3 美学：光晕外球（半透明壳，呼吸/褪色由锚点驱动——萤火意象）
                GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                shell.name = "GlowShell";
                shell.transform.SetParent(proto.transform, false);
                shell.transform.localScale = Vector3.one * 1.6f;
                Collider shellCollider = shell.GetComponent<Collider>();
                if (shellCollider != null) UnityEngine.Object.DestroyImmediate(shellCollider);
                MeshRenderer shellRenderer = shell.GetComponent<MeshRenderer>();
                Material shellMat = new Material(glowShellShader);
                shellMat.SetColor("_Color", new Color(0.91f, 0.64f, 0.29f, 0.18f));
                shellRenderer.sharedMaterial = shellMat;
                protoAnchor.SetGlowShell(shellRenderer);
            }

            // R15 ①：符号浮标（MoL image 要素——billboard 类型小牌，实例绑定碎片时换贴图）
            Shader symShader = glowShellShader != null ? glowShellShader : Shader.Find("MemoryCorridor/Sprite3D");
            if (symShader != null)
            {
                GameObject symGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                symGo.name = "Symbol";
                symGo.transform.SetParent(proto.transform, false);
                symGo.transform.localPosition = Vector3.up * 0.48f; // 光晕壳外（壳半径 0.36m），不挡视线
                symGo.transform.localScale = Vector3.one * 0.10f;
                Collider symCollider = symGo.GetComponent<Collider>();
                if (symCollider != null) UnityEngine.Object.DestroyImmediate(symCollider);
                MeshRenderer symRenderer = symGo.GetComponent<MeshRenderer>();
                Material symMat = new Material(symShader);
                symRenderer.sharedMaterial = symMat;
                LightPointSymbol sym = symGo.AddComponent<LightPointSymbol>();
                sym.Configure(symRenderer);
                protoAnchor.SetSymbol(sym);
            }

            proto.AddComponent<XRSimpleInteractable>();
            GameObject lightPrefab = PrefabUtility.SaveAsPrefabAsset(proto, "Assets/Prefabs/LightPoint.prefab");
            UnityEngine.Object.DestroyImmediate(proto);
            Debug.Log("[SceneBuilder] LightPoint.prefab OK（含 XRSimpleInteractable）");

            // 2) 演示数据（两房间五碎片，写 appdata.json）
            AppData demo = BuildDemoAppData();
            SaveDataService.SaveAppData(demo);
            Debug.Log("[SceneBuilder] appdata.json 已写（2 房间 8 碎片）: " + SaveDataService.AppDataFilePath);

            // 3) 布置场景（幂等）
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/EditorScene.unity");
            DestroyAll<LightPointAnchor>("光点");
            DestroyAll<FadeController>("褪色控制器");
            DestroyAll<XROrigin>("XR Origin");
            DestroyAll<XRInteractionManager>("交互管理器");
            DestroyAll<XRDeviceSimulator>("设备模拟器");
            DestroyAll<XRIAnchorBridge>("XRI桥");
            DestroyAll<Camera>("旧相机");
            DestroyAll<ImportServer>("导入服务");
            DestroyAll<AmbienceController>("环境音");
            DestroyAll<MemoryPanel>("记忆面板");
            DestroyAll<StardustField>("星尘");
            DestroyAll<HoverPreviewCard>("预览卡");
            DestroyAll<WelcomeBoard>("欢迎牌");
            DestroyAll<RoomDoor>("房间门");
            DestroyAll<TutorialGuide>("引导条");
            DestroyAll<Media.SettingsPanel>("设置面板");
            DestroyAll<PanoramaDome>("全景穹顶");
            DestroyAll<RoomManager>("房间管理");
            DestroyAll<RoutePlayer>("走读");
            DestroyAll<ReplaySequencer>("重演");
            DestroyAll<StardustBeacon>("信标");
            DestroyAll<ConstellationWall>("星河墙");
            DestroyAll<StarNode>("星点");
            DestroyAll<SceneFader>("转场");
            DestroyAll<TeleportationArea>("传送区");

            // 4) 全景穹顶 + 环境光（坑-022）
            Shader domeShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/PanoramaDome.shader");
            if (domeShader == null) throw new Exception("PanoramaDome.shader 未找到");
            GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "PanoramaDome";
            dome.transform.position = Vector3.zero;
            dome.transform.localScale = Vector3.one * 60f;
            Collider domeCollider = dome.GetComponent<Collider>();
            if (domeCollider != null) UnityEngine.Object.DestroyImmediate(domeCollider);
            MeshRenderer domeRenderer = dome.GetComponent<MeshRenderer>();
            Material domeMat = new Material(domeShader);
            // 编辑期直接贴客厅全景（资产引用随场景序列化）——用户打开场景即可见"奶奶的客厅"；
            // Play 时 PanoramaDome.Start 再按 appdata 室内/持久化目录优先级重载（双保险）
            Texture2D editTimePano = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Art/Panoramas/living_room_warm.jpg");
            if (editTimePano != null) domeMat.mainTexture = editTimePano;
            AssetDatabase.CreateAsset(domeMat, "Assets/Materials/PanoramaDome.mat");
            domeRenderer.sharedMaterial = domeMat;
            dome.AddComponent<PanoramaDome>();
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.19f, 0.21f);

            // 5) XR 装配
            // R3 美学：Bloom 后处理（视觉规范 §4.2——光点/星尘真正发光；坑-039）
            VolumeProfile volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            UnityEngine.Rendering.Universal.Bloom bloom = volumeProfile.Add<UnityEngine.Rendering.Universal.Bloom>();
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.6f);
            bloom.tint.Override(AmberGlow);
            // R11 b：舒适暗角（强度由 SettingsService.ApplyVignette 运行时按 settings 调）
            UnityEngine.Rendering.Universal.Vignette vignette = volumeProfile.Add<UnityEngine.Rendering.Universal.Vignette>();
            vignette.intensity.Override(0f);
            vignette.smoothness.Override(0.4f);
            AssetDatabase.CreateAsset(volumeProfile, "Assets/Settings/GlobalVolume.asset");
            GameObject volumeGo = new GameObject("GlobalVolume");
            Volume volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = volumeProfile;

            GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
            if (rigPrefab == null) throw new Exception("XR Origin (XR Rig).prefab 未找到");
            GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rig.transform.position = Vector3.zero;

            // 坑-039：主相机开启后处理（Bloom 才会生效）
            Unity.XR.CoreUtils.XROrigin origin = rig.GetComponent<Unity.XR.CoreUtils.XROrigin>();
            if (origin != null && origin.Camera != null)
            {
                var camData = origin.Camera.GetComponent<
                    UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (camData != null) camData.renderPostProcessing = true;
            }
            // R4 F1：Kinematic Rigidbody——OnTriggerEnter 需要一方有 RB（门框触发切房）
            Rigidbody originRb = rig.AddComponent<Rigidbody>();
            originRb.isKinematic = true;
            originRb.useGravity = false;

            GameObject simPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPrefabPath);
            if (simPrefab == null) throw new Exception("XR Device Simulator.prefab 未找到");
            GameObject sim = (GameObject)PrefabUtility.InstantiatePrefab(simPrefab);
            sim.transform.position = Vector3.zero;

            // 6) 记忆面板（W4 UGUI 磨砂版：Awake/Show 自建 Canvas，无需编辑期装配）
            GameObject panelGo = new GameObject("MemoryPanel");
            MemoryPanel panel = panelGo.AddComponent<MemoryPanel>();
            panelGo.SetActive(false); // 选中光点时才弹出

            // 7) 环境 / 服务 / 控制器
            AudioClip ambEvening = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/amb_evening_roomtone.wav");
            AudioClip ambCourtyard = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/amb_courtyard_dusk.wav");
            GameObject ambience = new GameObject("Ambience");
            AudioSource ambSource = ambience.AddComponent<AudioSource>();
            ambSource.clip = ambEvening;
            ambience.AddComponent<AmbienceController>();

            new GameObject("StardustField").AddComponent<StardustField>();
            new GameObject("ImportServer").AddComponent<ImportServer>();

            // R1 E2：悬停预览小卡（初始隐藏，桥接器驱动）
            GameObject previewGo = new GameObject("HoverPreviewCard");
            previewGo.AddComponent<MeshRenderer>();
            HoverPreviewCard previewCard = previewGo.AddComponent<HoverPreviewCard>();
            previewGo.SetActive(false); // EnsureBuilt 惰性构建；Hide 即此态

            FadeController fade = new GameObject("FadeController").AddComponent<FadeController>();

            // 地面 + 传送区域（XRI Teleportation；走读定点传送 W4 换 TeleportationAnchor）
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(4f, 1f, 4f);
            floor.AddComponent<TeleportationArea>();

            SceneFader fader = new GameObject("SceneFader").AddComponent<SceneFader>();

            GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "StardustBeacon";
            beacon.transform.localScale = Vector3.one * 0.18f;
            Collider beaconCollider = beacon.GetComponent<Collider>();
            if (beaconCollider != null) UnityEngine.Object.DestroyImmediate(beaconCollider);
            beacon.GetComponent<MeshRenderer>().sharedMaterial = glowMat;
            beacon.AddComponent<StardustBeacon>();
            beacon.AddComponent<Media.PulseEmissive>(); // R12 path：呼吸式微高亮（单焦点）

            ReplaySequencer sequencer = new GameObject("ReplaySequencer").AddComponent<ReplaySequencer>();

            ConstellationWall wall = new GameObject("ConstellationWall").AddComponent<ConstellationWall>();
            wall.Configure(glowMat, panel, sequencer);

            RoomManager roomManager = new GameObject("RoomManager").AddComponent<RoomManager>();
            roomManager.Configure(lightPrefab, fade, ambSource, new AudioClip[] { ambEvening, ambCourtyard }, fader);

            RoutePlayer routePlayer = new GameObject("RoutePlayer").AddComponent<RoutePlayer>();
            routePlayer.Configure(beacon, roomManager, fade, wall);

            XRIAnchorBridge bridge = new GameObject("XRIAnchorBridge").AddComponent<XRIAnchorBridge>();
            AudioClip chime = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/sfx_lightpoint_chime.wav");

            // R2 E1：欢迎告示牌——出生点视线正前方（R4 修正：原在侧面不可见）
            GameObject welcomeGo = new GameObject("WelcomeBoard");
            welcomeGo.AddComponent<MeshRenderer>();
            welcomeGo.AddComponent<WelcomeBoard>();
            welcomeGo.transform.position = new Vector3(0f, 1.75f, 1.8f); // R7：抬高 0.25m，不遮挡 1.2m 高的光点

            // R4：新手引导条（HUD 跟随相机，事件+计时双驱动）
            GameObject guideGo = new GameObject("TutorialGuide");
            guideGo.AddComponent<MeshRenderer>();
            TutorialGuide tutorialGuide = guideGo.AddComponent<TutorialGuide>();

            // R5/R11：设置面板（5 行开关：距离/暗角/移动/光点/标签）
            GameObject settingsGo = new GameObject("SettingsPanel");
            settingsGo.AddComponent<MeshRenderer>();
            Media.SettingsPanel settingsPanel = settingsGo.AddComponent<Media.SettingsPanel>();
            settingsGo.transform.position = new Vector3(0.9f, 1.5f, 2.2f); // R12 差异表：水平角 54°→22°

            // 坑-026：全部跨对象引用走 Configure 序列化字段（编辑期属性赋值不持久化）
            bridge.Configure(panel, routePlayer, sequencer, previewCard, chime, wall,
                welcomeGo.GetComponent<WelcomeBoard>(), tutorialGuide, settingsPanel);

            // R4 F1/F12：房间入口门（发光门框+牌匾+走近自动切房，双向对称）
            BuildRoomDoor(new Vector3(0f, 0f, 3.9f), 1, "老院子", roomManager, glowMat);
            Debug.Log("[SceneBuilder] 房间门 OK（走近自动切房，带 3s 冷却）");

            if (GameObject.Find("SmokeProbe") == null)
            {
                GameObject probeGo = new GameObject("SmokeProbe");
                probeGo.AddComponent<SmokeProbe>();
            }

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/EditorScene.unity");
            Debug.Log("[SceneBuilder] EditorScene 布置完成（穹顶 + XR Rig + 两房间管理 + 走读 + 重演 + 面板 + 导入服务 + 探针）");
        }

        private static TextMesh BuildTextMesh(Transform parent, string name, Vector3 localPos,
            Font font, int fontSize, float charSize, TextAnchor anchor, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.font = font;
            renderer.sharedMaterial = font.material;
            tm.fontSize = fontSize;
            tm.characterSize = charSize;
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            return tm;
        }

        /// <summary>R4 F1/F12：房间入口门——两柱+横梁（发光）+ 圆角牌匾 + 走近触发切房。</summary>
        private static void BuildRoomDoor(Vector3 position, int targetIndex, string targetTitle,
            RoomManager roomManager, Material glowMat)
        {
            GameObject door = new GameObject("RoomDoor_" + targetIndex);
            door.transform.position = position;

            Shader spriteShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/Sprite3D.shader");

            // 两柱 + 横梁（发光 amber）
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = "Post" + (i > 0 ? "R" : "L");
                post.transform.SetParent(door.transform, false);
                post.transform.localPosition = new Vector3(i * 0.7f, 1.1f, 0f);
                post.transform.localScale = new Vector3(0.09f, 2.2f, 0.09f);
                MeshRenderer postRenderer = post.GetComponent<MeshRenderer>();
                Material postMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                postMat.EnableKeyword("_EMISSION");
                postMat.SetColor("_BaseColor", AmberGlow * 0.6f);
                postMat.SetColor("_EmissionColor", AmberGlow * 1.2f);
                postRenderer.material = postMat;
                post.AddComponent<Media.PulseEmissive>(); // R12 path：门柱呼吸式微高亮
            }
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "Beam";
            beam.transform.SetParent(door.transform, false);
            beam.transform.localPosition = new Vector3(0f, 2.24f, 0f);
            beam.transform.localScale = new Vector3(1.49f, 0.09f, 0.09f);
            MeshRenderer beamRenderer = beam.GetComponent<MeshRenderer>();
            Material beamMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            beamMat.EnableKeyword("_EMISSION");
            beamMat.SetColor("_BaseColor", AmberGlow * 0.6f);
            beamMat.SetColor("_EmissionColor", AmberGlow * 1.2f);
            beamRenderer.material = beamMat;

            // 牌匾：圆角底板 + 目标房间名
            GameObject plaque = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plaque.name = "Plaque";
            UnityEngine.Object.DestroyImmediate(plaque.GetComponent<Collider>());
            plaque.transform.SetParent(door.transform, false);
            plaque.transform.localPosition = new Vector3(0f, 1.85f, -0.01f);
            plaque.transform.localScale = new Vector3(0.52f, 0.13f, 1f);
            MeshRenderer plaqueRenderer = plaque.GetComponent<MeshRenderer>();
            Material plaqueMat = new Material(spriteShader);
            plaqueMat.mainTexture = RoundedRectTexture.RoundedRect(
                520, 130, 14, new Color(0.12f, 0.10f, 0.15f, 0.92f),
                new Color(0.91f, 0.64f, 0.29f), 2);
            plaqueMat.SetColor("_Color", Color.white);
            plaqueRenderer.material = plaqueMat;
            GameObject plaqueTextGo = new GameObject("PlaqueText");
            plaqueTextGo.transform.SetParent(plaque.transform, false);
            plaqueTextGo.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            MeshRenderer textRenderer = plaqueTextGo.AddComponent<MeshRenderer>();
            TextMesh plaqueTm = plaqueTextGo.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            plaqueTm.font = font;
            textRenderer.sharedMaterial = font.material;
            plaqueTm.text = "前往 " + targetTitle;
            plaqueTm.fontSize = 42;
            plaqueTm.characterSize = 0.011f;
            plaqueTm.anchor = TextAnchor.MiddleCenter;
            plaqueTm.alignment = TextAlignment.Center;
            plaqueTm.color = new Color(0.92f, 0.88f, 0.82f, 1f);

            // 触发区：玩家（XR Origin）走近自动切房
            BoxCollider trigger = door.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.4f, 2.4f, 0.8f);
            trigger.center = new Vector3(0f, 1.2f, 0f);
            RoomDoor roomDoor = door.AddComponent<RoomDoor>();
            roomDoor.Configure(targetIndex, targetTitle, roomManager);
        }

        private static void DestroyAll<T>(string label) where T : Component
        {
            // 坑-050：排除 inactive 的查找会让 inactive 面板/引导牌跨轮堆积——cleanup 必须含 inactive
            T[] stale = UnityEngine.Object.FindObjectsByType<T>(
                UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
            for (int i = 0; i < stale.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(stale[i].gameObject);
            }
            if (stale.Length > 0) Debug.Log($"[SceneBuilder] 清理旧{label} ×{stale.Length}");
        }

        private static AppData BuildDemoAppData()
        {
            AppData data = new AppData();
            data.currentMemberId = "mem_01";
            data.members.Add(new MemberData { id = "mem_01", name = "我", role = "owner" });

            // —— 房间一：奶奶的客厅（3 碎片）——
            RoomData room1 = new RoomData
            {
                id = "room_01",
                title = "奶奶的客厅",
                panoramaPath = "Panoramas/room_01.jpg",
                theme = "amber",
                ambientPath = "Audio/amb_evening_roomtone.wav",
                visibility = "public",
            };
            room1.fragments.Add(new MemoryFragment
            {
                id = "frag_01",
                title = "缝纫机",
                type = "image",
                ownerMemberId = "mem_01",
                roomId = "room_01",
                order = 0,
                visibility = "public",
                createdAt = "2026-11-12T09:00:00+08:00",
                lastVisitedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                diary = new DiaryEntry { date = "2012-06-18", text = "她的蝴蝶牌缝纫机，夏天总在窗边响。" },
                mediaItems = new List<MediaItem>
                {
                    new MediaItem { kind = "image", path = "Media/frag_01/sewing.jpg", caption = "窗边的缝纫机" },
                },
                anchor = new FragmentAnchor { position = new float[] { -0.9f, 1.2f, 2.5f } },
            });
            room1.fragments.Add(new MemoryFragment
            {
                id = "frag_02",
                title = "老收音机",
                type = "audio",
                ownerMemberId = "mem_01",
                roomId = "room_01",
                order = 1,
                visibility = "private",
                createdAt = "2026-11-12T09:10:00+08:00",
                lastVisitedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                diary = new DiaryEntry { date = "2015-10-01", text = "戏曲频道永远开到最大声，她跟着哼，跑调也不许我们笑。" },
                mediaItems = new List<MediaItem>
                {
                    new MediaItem { kind = "image", path = "Media/frag_02/radio.jpg", caption = "五斗柜上的红灯牌收音机" },
                    new MediaItem { kind = "audio", path = "Media/frag_02/radio.wav", caption = "收音机里的一段戏" },
                },
                anchor = new FragmentAnchor { position = new float[] { 0f, 1.2f, 2.5f } },
            });
            room1.fragments.Add(new MemoryFragment
            {
                id = "frag_03",
                title = "童年自行车",
                type = "mixed",
                ownerMemberId = "mem_01",
                roomId = "room_01",
                order = 2,
                visibility = "public",
                createdAt = "2026-11-12T09:20:00+08:00",
                lastVisitedAt = "2026-01-15T10:00:00Z", // ≈300 天前 → 褪色样例
                diary = new DiaryEntry { date = "2008-07-20", text = "后座上驮着整个夏天。" },
                mediaItems = new List<MediaItem>
                {
                    new MediaItem { kind = "image", path = "Media/frag_03/bike.jpg", caption = "院子里学车" },
                    new MediaItem { kind = "audio", path = "Media/frag_03/bell.wav", caption = "车铃声" },
                    new MediaItem { kind = "video", path = "Media/frag_03/memories.mp4", caption = "重演回放（R8 视频）" },
                },
                anchor = new FragmentAnchor { position = new float[] { 0.9f, 1.2f, 2.5f } },
            });
            room1.fragments.Add(new MemoryFragment
            {
                id = "frag_06",
                title = "窗台的花瓶",
                type = "image",
                ownerMemberId = "mem_01",
                roomId = "room_01",
                order = 3,
                visibility = "public",
                createdAt = "2026-11-12T09:50:00+08:00",
                lastVisitedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                diary = new DiaryEntry { date = "2018-04-05", text = "清明前后她总在窗台插一束新折的栀子，满屋都是香的。" },
                mediaItems = new List<MediaItem>
                {
                    new MediaItem { kind = "image", path = "Media/frag_06/vase.jpg", caption = "窗台上的木框与花瓶" },
                },
                anchor = new FragmentAnchor { position = new float[] { -1.6f, 1.2f, 1.6f } },
            });
            data.rooms.Add(room1);

            // —— 房间二：老院子（2 碎片）——
            RoomData room2 = new RoomData
            {
                id = "room_02",
                title = "老院子",
                panoramaPath = "Panoramas/room_02.jpg",
                theme = "dusk",
                ambientPath = "Audio/amb_courtyard_dusk.wav",
                visibility = "public",
            };
            room2.fragments.Add(new MemoryFragment
            {
                id = "frag_04",
                title = "村口的路",
                type = "image",
                ownerMemberId = "mem_01",
                roomId = "room_02",
                order = 0,
                visibility = "public",
                createdAt = "2026-11-12T09:30:00+08:00",
                lastVisitedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                diary = new DiaryEntry { date = "2009-05-01", text = "放学回家的路，土路晴天也带泥。" },
                mediaItems = new List<MediaItem>
                {
                    new MediaItem { kind = "image", path = "Media/frag_04/road.jpg", caption = "乡间的路" },
                },
                anchor = new FragmentAnchor { position = new float[] { -0.5f, 1.2f, 2.0f } },
            });
            room2.fragments.Add(new MemoryFragment
            {
                id = "frag_05",
                title = "摇椅日记",
                type = "diary",
                ownerMemberId = "mem_01",
                roomId = "room_02",
                order = 1,
                visibility = "public",
                createdAt = "2026-11-12T09:40:00+08:00",
                lastVisitedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                diary = new DiaryEntry { date = "2020-08-15", text = "傍晚坐在摇椅上，蒲扇摇啊摇，槐花落了一地。" },
                anchor = new FragmentAnchor { position = new float[] { 0.5f, 1.2f, 2.0f } },
            });
            room2.fragments.Add(new MemoryFragment
            {
                id = "frag_07",
                title = "屋前的自行车",
                type = "image",
                ownerMemberId = "mem_01",
                roomId = "room_02",
                order = 2,
                visibility = "public",
                createdAt = "2026-11-12T09:45:00+08:00",
                lastVisitedAt = "2026-05-15T10:00:00Z", // ≈180 天前 → 房间二褪色对照
                diary = new DiaryEntry { date = "2010-06-01", text = "二八大杠停在枣树下，车把上挂着一网兜汽水瓶。" },
                mediaItems = new List<MediaItem>
                {
                    new MediaItem { kind = "image", path = "Media/frag_07/frontyard.jpg", caption = "屋前骑车" },
                },
                anchor = new FragmentAnchor { position = new float[] { -1.6f, 1.2f, 1.2f } },
            });
            room2.fragments.Add(new MemoryFragment
            {
                id = "frag_08",
                title = "妹妹学车",
                type = "image",
                ownerMemberId = "mem_01",
                roomId = "room_02",
                order = 3,
                visibility = "public",
                createdAt = "2026-11-12T09:50:00+08:00",
                lastVisitedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                diary = new DiaryEntry { date = "2011-09-12", text = "她扶着车把不敢蹬，我在后面扶了整个暑假，撒手那天比她还高兴。" },
                mediaItems = new List<MediaItem>
                {
                    new MediaItem { kind = "image", path = "Media/frag_08/learner.jpg", caption = "扶着车的妹妹" },
                },
                anchor = new FragmentAnchor { position = new float[] { 1.6f, 1.2f, 1.2f } },
            });
            data.rooms.Add(room2);

            return data;
        }

        /// <summary>appdata.json 落盘回读校验（Newtonsoft 路径回归）。</summary>
        private static void VerifySaveRoundtrip()
        {
            AppData loaded = SaveDataService.LoadAppData();
            bool ok = loaded.rooms.Count == 2
                && loaded.rooms[0].fragments.Count == 4
                && loaded.rooms[1].fragments.Count == 4
                && loaded.settings.fadeStartDays == 30
                && loaded.rooms[0].fragments[2].lastVisitedAt == "2026-01-15T10:00:00Z"
                && loaded.rooms[1].fragments[2].lastVisitedAt == "2026-05-15T10:00:00Z"
                && loaded.rooms[0].fragments[1].visibility == "private"
                && loaded.rooms[0].fragments[1].mediaItems.Count == 2
                && loaded.rooms[1].fragments[1].type == "diary";

            if (!ok)
            {
                Debug.LogError("[SceneBuilder] FAIL: appdata.json 回读校验不通过");
                throw new Exception("appdata.json 回读校验失败");
            }
            Debug.Log("[SceneBuilder] appdata.json 回读校验 OK（2 房间/8 碎片/双褪色样例/私密样例/组合媒体/日记类型）");
        }
    }
}
