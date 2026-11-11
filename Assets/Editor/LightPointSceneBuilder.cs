using System;
using System.Collections.Generic;
using MemoryCorridor.Core;
using MemoryCorridor.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MemoryCorridor.EditorTools
{
    /// <summary>
    /// W1 场景搭建器：光点预制体 + EditorScene 三个演示光点（新鲜/私密/褪色 300 天）
    /// + appdata.json 落盘回读校验 + 进入 Play 由 SmokeProbe 自动断言并退出。
    /// 用法：Unity.exe -batchmode -projectPath … -executeMethod MemoryCorridor.EditorTools.LightPointSceneBuilder.BuildAndSmoke -logFile …
    /// 注意：不传 -quit（Play 内由探针 EditorApplication.Exit 结束进程）。
    /// </summary>
    public static class LightPointSceneBuilder
    {
        private static readonly Color AmberGlow = new Color(0.91f, 0.64f, 0.29f);

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

            // 2) 光点预制体：球体 + 点光源 + 音源(带低通) + 锚点
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
            PrefabUtility.SaveAsPrefabAsset(proto, "Assets/Prefabs/LightPoint.prefab");
            UnityEngine.Object.DestroyImmediate(proto);
            Debug.Log("[SceneBuilder] LightPoint.prefab OK");

            // 3) 演示碎片数据（同时写 appdata.json）
            List<MemoryFragment> demo = BuildDemoFragments();
            SaveDemoAppData(demo);

            // 4) 布置 EditorScene（幂等：先清旧光点/旧控制器，重跑不叠加——坑-019）
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/EditorScene.unity");
            LightPointAnchor[] staleAnchors = UnityEngine.Object.FindObjectsByType<LightPointAnchor>(UnityEngine.FindObjectsSortMode.None);
            for (int i = 0; i < staleAnchors.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(staleAnchors[i].gameObject);
            }
            FadeController[] staleFades = UnityEngine.Object.FindObjectsByType<FadeController>(UnityEngine.FindObjectsSortMode.None);
            for (int i = 0; i < staleFades.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(staleFades[i].gameObject);
            }

            Camera cam = Camera.main;
            if (cam == null) throw new Exception("EditorScene 缺少 Main Camera");
            cam.transform.position = new Vector3(0f, 1.2f, 0f);
            cam.transform.rotation = Quaternion.identity;
            if (cam.GetComponent<RayInteractor>() == null) cam.gameObject.AddComponent<RayInteractor>();

            Vector3[] positions =
            {
                new Vector3(-0.9f, 1.2f, 2.5f),
                new Vector3(0f, 1.2f, 2.5f),
                new Vector3(0.9f, 1.2f, 2.5f),
            };
            string[] objectNames = { "LP_fresh_1", "LP_private_2", "LP_faded_3" };
            GameObject lightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LightPoint.prefab");

            GameObject fadeGo = new GameObject("FadeController");
            FadeController fade = fadeGo.AddComponent<FadeController>();

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
            Debug.Log("[SceneBuilder] EditorScene 布置完成（3 光点 + 褪色样例 + 探针）");
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
