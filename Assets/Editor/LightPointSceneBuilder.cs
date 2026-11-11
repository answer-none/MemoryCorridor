using System;
using System.Collections.Generic;
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
    /// W1 场景搭建器（XRI 正式版）：光点预制体（含 XRSimpleInteractable）+ EditorScene
    /// 三个演示光点（新鲜/私密/褪色 300 天）+ XRI XR Origin 装配 + XR Device Simulator
    /// + XRIAnchorBridge + appdata.json 落盘回读校验 + 进入 Play 由 SmokeProbe 自动断言并退出。
    /// 用法：Unity.exe -batchmode -projectPath … -executeMethod MemoryCorridor.EditorTools.LightPointSceneBuilder.BuildAndSmoke -logFile …
    /// 注意：不传 -quit（Play 内由探针 EditorApplication.Exit 结束进程）。
    /// 幂等：重跑前按类型清空旧光点/控制器/相机/XR 装配（坑-019）。
    /// </summary>
    public static class LightPointSceneBuilder
    {
        private static readonly Color AmberGlow = new Color(0.91f, 0.64f, 0.29f);
        private const string RigPrefabPath = "Assets/Samples/XR Interaction Toolkit/2.5.4/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        private const string SimulatorPrefabPath = "Assets/Samples/XR Interaction Toolkit/2.5.4/XR Device Simulator/XR Device Simulator.prefab";

        [MenuItem("MemoryCorridor/W1 搭建演示场景")]
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

            // 1) 光点辉光材质资产（URP Lit + 自发光）
            Material glowMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            glowMat.EnableKeyword("_EMISSION");
            glowMat.SetColor("_BaseColor", AmberGlow * 0.4f);
            glowMat.SetColor("_EmissionColor", AmberGlow * 2f);
            AssetDatabase.CreateAsset(glowMat, "Assets/Prefabs/LightPointGlow.mat");

            // 2) 光点预制体：球体 + 点光源 + 音源(带低通) + 锚点 + XRI 可交互体
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
            proto.AddComponent<XRSimpleInteractable>();
            PrefabUtility.SaveAsPrefabAsset(proto, "Assets/Prefabs/LightPoint.prefab");
            UnityEngine.Object.DestroyImmediate(proto);
            Debug.Log("[SceneBuilder] LightPoint.prefab OK（含 XRSimpleInteractable）");

            // 3) 演示碎片数据（同时写 appdata.json）
            List<MemoryFragment> demo = BuildDemoFragments();
            SaveDemoAppData(demo);

            // 4) 布置 EditorScene（幂等：先清旧实例再布置——坑-019）
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

            // 4.5) 暮色氛围：全景穹顶 + 环境光（坑-022：URP 下内置 Skybox/Panorama 不可用，改自研穹顶）
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials"); // 坑-023：CreateAsset 不会自动建父目录
            DestroyAll<PanoramaDome>("全景穹顶");
            Shader domeShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/PanoramaDome.shader");
            if (domeShader == null) throw new Exception("PanoramaDome.shader 未找到（检查 Assets/Shaders）");
            GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "PanoramaDome";
            dome.transform.position = Vector3.zero;
            dome.transform.localScale = Vector3.one * 60f;
            Collider domeCollider = dome.GetComponent<Collider>();
            if (domeCollider != null) UnityEngine.Object.DestroyImmediate(domeCollider);
            MeshRenderer domeRenderer = dome.GetComponent<MeshRenderer>();
            Material domeMat = new Material(domeShader);
            AssetDatabase.CreateAsset(domeMat, "Assets/Materials/PanoramaDome.mat");
            domeRenderer.sharedMaterial = domeMat;
            dome.AddComponent<PanoramaDome>();
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.19f, 0.21f); // 暮色暖基调

            GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
            if (rigPrefab == null) throw new Exception("XR Origin (XR Rig).prefab 未找到: " + RigPrefabPath);
            GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rig.transform.position = Vector3.zero;

            GameObject simPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPrefabPath);
            if (simPrefab == null) throw new Exception("XR Device Simulator.prefab 未找到: " + SimulatorPrefabPath);
            GameObject sim = (GameObject)PrefabUtility.InstantiatePrefab(simPrefab);
            sim.transform.position = Vector3.zero;

            // 4.7) 记忆面板（W2 版：Quad + TextMesh，W4 换 UGUI）
            GameObject panelRoot = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panelRoot.name = "MemoryPanel";
            Collider panelCollider = panelRoot.GetComponent<Collider>();
            if (panelCollider != null) UnityEngine.Object.DestroyImmediate(panelCollider);
            panelRoot.transform.position = new Vector3(0f, 1.4f, 1.5f);
            panelRoot.transform.localScale = new Vector3(1.1f, 0.85f, 1f);
            MeshRenderer panelRenderer = panelRoot.GetComponent<MeshRenderer>();
            Material panelMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(panelMat, "Assets/Materials/MemoryPanelImage.mat");
            panelRenderer.sharedMaterial = panelMat;

            Font builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panelRoot.transform, false);
            titleGo.transform.localPosition = new Vector3(0f, 0.42f, -0.01f);
            MeshRenderer titleRenderer = titleGo.AddComponent<MeshRenderer>();
            TextMesh titleTm = titleGo.AddComponent<TextMesh>();
            titleTm.font = builtinFont;
            titleRenderer.sharedMaterial = builtinFont.material;
            titleTm.fontSize = 64;
            titleTm.characterSize = 0.09f;
            titleTm.anchor = TextAnchor.MiddleCenter;
            titleTm.alignment = TextAlignment.Center;
            titleTm.color = new Color(0.91f, 0.64f, 0.29f);

            GameObject bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(panelRoot.transform, false);
            bodyGo.transform.localPosition = new Vector3(0f, -0.12f, -0.01f);
            MeshRenderer bodyRenderer = bodyGo.AddComponent<MeshRenderer>();
            TextMesh bodyTm = bodyGo.AddComponent<TextMesh>();
            bodyTm.font = builtinFont;
            bodyRenderer.sharedMaterial = builtinFont.material;
            bodyTm.fontSize = 48;
            bodyTm.characterSize = 0.075f;
            bodyTm.anchor = TextAnchor.UpperCenter;
            bodyTm.alignment = TextAlignment.Center;
            bodyTm.color = new Color(0.92f, 0.88f, 0.82f);

            MemoryPanel panel = panelRoot.AddComponent<MemoryPanel>();
            panel.Configure(panelRenderer, titleTm, bodyTm);
            panelRoot.SetActive(false); // 选中光点时才弹出

            // 4.8) 环境音（appdata settings.ambientVolume 控制）与导入服务
            AudioClip ambClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/amb_evening_roomtone.wav");
            GameObject ambience = new GameObject("Ambience");
            ambience.AddComponent<AudioSource>().clip = ambClip;
            ambience.AddComponent<AmbienceController>();
            new GameObject("ImportServer").AddComponent<ImportServer>();

            GameObject bridgeGo = new GameObject("XRIAnchorBridge");
            XRIAnchorBridge bridgeComp = bridgeGo.AddComponent<XRIAnchorBridge>();
            bridgeComp.Configure(panel);

            new GameObject("FadeController").AddComponent<FadeController>();

            // 5) 三个演示光点
            Vector3[] positions =
            {
                new Vector3(-0.9f, 1.2f, 2.5f),
                new Vector3(0f, 1.2f, 2.5f),
                new Vector3(0.9f, 1.2f, 2.5f),
            };
            string[] objectNames = { "LP_fresh_1", "LP_private_2", "LP_faded_3" };
            GameObject lightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LightPoint.prefab");

            for (int i = 0; i < demo.Count; i++)
            {
                MemoryFragment fragment = demo[i];
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(lightPrefab);
                instance.name = objectNames[i];
                instance.transform.position = positions[i];
                LightPointAnchor anchor = instance.GetComponent<LightPointAnchor>();
                anchor.BindFragment(fragment.id, fragment.visibility == "private");
                // 褪色不在编辑期烘焙（fadeFactor 非序列化，坑-018）：
                // Play 时由 FadeController.Start → ApplyAllFromDisk 从 appdata.json 应用
            }

            if (GameObject.Find("SmokeProbe") == null)
            {
                GameObject probeGo = new GameObject("SmokeProbe");
                probeGo.AddComponent<SmokeProbe>();
            }

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/EditorScene.unity");
            Debug.Log("[SceneBuilder] EditorScene 布置完成（3 光点 + XR Rig + 模拟器 + 桥接 + 探针）");
        }

        private static void DestroyAll<T>(string label) where T : Component
        {
            T[] stale = UnityEngine.Object.FindObjectsByType<T>(UnityEngine.FindObjectsSortMode.None);
            for (int i = 0; i < stale.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(stale[i].gameObject);
            }
            if (stale.Length > 0) Debug.Log($"[SceneBuilder] 清理旧{label} ×{stale.Length}");
        }

        private static List<MemoryFragment> BuildDemoFragments()
        {
            List<MemoryFragment> fragments = new List<MemoryFragment>();

            MemoryFragment f1 = new MemoryFragment
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
            };
            f1.anchor.position = new float[] { -0.9f, 1.2f, 2.5f };
            fragments.Add(f1);

            MemoryFragment f2 = new MemoryFragment
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
                diary = new DiaryEntry { date = "2015-10-01", text = "戏曲频道永远开到最大声。" },
                mediaItems = new List<MediaItem>
                {
                    new MediaItem { kind = "audio", path = "Media/frag_02/radio.wav", caption = "收音机里的一段戏" },
                },
            };
            f2.anchor.position = new float[] { 0f, 1.2f, 2.5f };
            fragments.Add(f2);

            MemoryFragment f3 = new MemoryFragment
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
                },
            };
            f3.anchor.position = new float[] { 0.9f, 1.2f, 2.5f };
            fragments.Add(f3);

            return fragments;
        }

        private static void SaveDemoAppData(List<MemoryFragment> fragments)
        {
            AppData data = new AppData();
            data.currentMemberId = "mem_01";
            data.members.Add(new MemberData { id = "mem_01", name = "我", role = "owner" });
            RoomData room = new RoomData
            {
                id = "room_01",
                title = "奶奶的客厅",
                panoramaPath = "Panoramas/room_01.jpg",
                theme = "amber",
                ambientPath = "Audio/amb_evening_roomtone.wav",
                visibility = "public",
            };
            room.fragments.AddRange(fragments);
            data.rooms.Add(room);
            SaveDataService.SaveAppData(data);
            Debug.Log("[SceneBuilder] appdata.json 已写: " + SaveDataService.AppDataFilePath);
        }

        /// <summary>appdata.json 落盘回读校验（Newtonsoft 路径回归）。</summary>
        private static void VerifySaveRoundtrip()
        {
            AppData loaded = SaveDataService.LoadAppData();
            bool ok = loaded.rooms.Count == 1
                && loaded.rooms[0].fragments.Count == 3
                && loaded.settings.fadeStartDays == 30
                && loaded.rooms[0].fragments[2].lastVisitedAt == "2026-01-15T10:00:00Z"
                && loaded.rooms[0].fragments[1].visibility == "private";

            if (!ok)
            {
                Debug.LogError("[SceneBuilder] FAIL: appdata.json 回读校验不通过");
                throw new Exception("appdata.json 回读校验失败");
            }
            Debug.Log("[SceneBuilder] appdata.json 回读校验 OK（3 碎片/褪色日期/私密标记/设置默认值）");
        }
    }
}
