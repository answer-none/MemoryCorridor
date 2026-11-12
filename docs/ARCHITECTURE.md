# 架构图（ARCHITECTURE v1.1）

> 生成：R3 · 与代码同步维护（改动涉及架构层必须更新本图）
> 代码入口：`Assets/Scripts/{Core,Interaction,Media}` + `Assets/Editor`（工具链）

---

## 1. 分层架构（运行时）

```mermaid
flowchart TB
    subgraph Data["数据层 Core"]
        APPDATA["AppData / RoomData / MemoryFragment\n(MemberData · MediaItem · DiaryEntry)"]
        SAVEDATA["SaveDataService\n(Newtonsoft · persistentDataPath)"]
        MEDIARES["MediaResolver\n持久化 → StreamingAssets 回退"]
        AUDIOsvc["AudioSettingsService\nmaster→AudioListener / narration / ambient"]
        ROUTE["RouteEditor(排序) · RoutePlayer(走读) · RoomManager(房间)"]
        FADE["FadeController\nlastVisitedAt → 褪色系数"]
    end

    subgraph Interact["交互层 Interaction"]
        ANCHOR["LightPointAnchor\n呼吸光/类型四色/音效/预览事件"]
        BRIDGE["XRIAnchorBridge\n懒接线0.5s退避/选中编排/延迟出面板"]
        RAY["RayInteractor(编辑器冒烟)"]
    end

    subgraph MediaL["体验层 Media"]
        PANEL["MemoryPanel\nUGUI磨砂面板/✕钮/录音播放"]
        PREVIEW["HoverPreviewCard\n悬停预览小卡"]
        REPLAY["ReplaySequencer\n四拍时序(跳过/复原)"]
        WALL["ConstellationWall\n星河回顾墙/星点/缩略/标签"]
        LAYERED["LayeredPhoto\n分层伪3D视差"]
        DOME["PanoramaDome\n自研shader全景穹顶"]
        SPATIAL["SpatialAudioAnchor\n先闻其声"]
        AMB["AmbienceController"]
        SFX["SfxLibrary\n程序合成SFX"]
        BOARD["WelcomeBoard\n欢迎引导"]
        DUST["StardustField\n星尘粒子"]
        IMPORT["ImportServer\n局域网HTTP:8899"]
        EXPORT["ExportService / ImportService\nzip 隐私过滤"]
    end

    subgraph XR["平台层"]
        XRI["XR Interaction Toolkit\n(XR Origin/Ray/SimpleInteractable)"]
        SIM["XR Device Simulator"]
        PICO["PICO Integration SDK v3.3.3\nPXR_Loader(真机)/TOBAPI"]
        OPENXR["OpenXR 1.9.1"]
        URP["URP 14\nBloom/ASTC/Linear"]
    end

    XRI --> BRIDGE
    SIM --> XRI
    PICO --> XRI
    OPENXR --> PICO
    BRIDGE --> ANCHOR
    BRIDGE --> PANEL
    BRIDGE --> REPLAY
    BRIDGE --> PREVIEW
    WALL --> REPLAY
    REPLAY --> LAYERED
    BRIDGE --> ROUTE
    ROUTE --> ANCHOR
    FADE --> ANCHOR
    ROUTE --> DOME
    APPDATA --> SAVEDATA
    SAVEDATA --> MEDIARES
    MEDIARES --> PANEL
    MEDIARES --> DOME
    AUDIOsvc --> PANEL
    AUDIOsvc --> AMB
    PANEL --> SPATIAL
    IMPORT --> SAVEDATA
    EXPORT --> SAVEDATA
    EXPORT --> MEDIARES
```

## 2. 数据流（内容生命周期）

```mermaid
flowchart LR
    PHONE["手机浏览器\nhttp://设备IP:8899"] -->|POST 图/音/日记| IMPORT["ImportServer\n落盘 Media/<id>/"]
    IMPORT --> SAVEDATA["appdata.json\n(members/rooms/fragments/settings)"]
    DEMO["随包 Demo\nStreamingAssets"] --> RESOLVE["MediaResolver\n持久化优先→随包回退"]
    SAVEDATA --> RESOLVE
    RESOLVE --> SPAWN["RoomManager.Start\n生成 LP_<id> 光点(类型配色/褪色)"]
    SPAWN --> TOUR["RoutePlayer 走读\n星尘信标按 order"]
    TOUR -->|EndTour| WALL["ConstellationWall"]
    SPAWN -->|select| REPLAY["ReplaySequencer 四拍"]
    REPLAY -->|Beat3/Done| PANEL["MemoryPanel"]
    SAVEDATA -->|filter private=false| EXPORT["ExportService\nzip(json+Media+Panoramas)"]
    EXPORT --> ZIP["memorycorridor_export.zip"]
    ZIP --> IMPORTSVC["ImportService\n解压合并(id冲突追加后缀)"]
```

## 3. 构建与质量管线

```mermaid
flowchart LR
    CODE["Assets/{Core,Interaction,Media,Editor}"] --> SMOKE["BuildAndSmoke\n23+段断言(编辑器Play)"]
    SMOKE -->|全绿| BUILD["AndroidBuildScript\nIL2CPP/ARM64/ASTC/Linear"]
    BUILD --> APK["_builds/MemoryCorridor_M2.apk"]
    APK --> EMU["PICO Emulator 装机验证"]
    EMU --> DEVICE["PICO 4 Ultra 真机"]
    PERF["PerformanceAudit\n基线报告"] -.-> SMOKE
    GIT["git push\nssh://git@ssh.github.com:443"] --> GH["github.com/answer-none/MemoryCorridor"]
```

## 4. 模块职责速查

| 模块 | 文件 | 一句话职责 |
|---|---|---|
| 根数据 | Core/AppData.cs | 成员+房间+设置+导出清单 |
| 存档 | Core/SaveDataService.cs | appdata.json 读写（Newtonsoft，主线程路径缓存 坑-024） |
| 素材解析 | Core/MediaResolver.cs | 持久化→StreamingAssets 两级查找 |
| 音量 | Core/AudioSettingsService.cs | 三路音量单一消费点 |
| 排序 | Core/RouteEditor.cs | order 交换落盘 |
| 走读 | Core/RoutePlayer.cs | 路线+信标+MarkVisited 回访 |
| 房间 | Core/RoomManager.cs | 光点生成/穹顶/环境音/切换 |
| 褪色 | Core/FadeController.cs | 天数→系数→光点/低通 |
| 光点 | Interaction/LightPointAnchor.cs | 呼吸光/配色/音效/事件 |
| 桥接 | Interaction/XRIAnchorBridge.cs | XRI→契约编排（全组件引用枢纽） |
| 面板 | Media/MemoryPanel.cs | UGUI 磨砂+录音+✕ |
| 预览 | Media/HoverPreviewCard.cs | 悬停小卡 |
| 重演 | Media/ReplaySequencer.cs | 四拍+LastPlaySource 分流 |
| 星河墙 | Media/ConstellationWall.cs | 弧形星座+点星重演 |
| 分层 | Media/LayeredPhoto.cs | 三层视差 |
| 穹顶 | Media/PanoramaDome.cs | appdata 驱动全景 |
| 导入 | Media/ImportServer.cs | HTTP:8899 上传 |
| 导出/导入包 | Core/ExportService·ImportService | zip+隐私过滤（R3 新增） |
| 引导 | Media/WelcomeBoard.cs | 三句告示牌 |
| 粒子 | Media/StardustField.cs | 跟随相机星尘 |
| 音效 | Media/SfxLibrary.cs | 程序合成 SFX（R3 新增） |
| 工具 | Editor/SetupWizard · LightPointSceneBuilder · AndroidBuildScript · PerformanceAudit | 环境/场景/构建/审计 |

## 5. 关键设计决策（索引）

| 决策 | 位置 |
|---|---|
| Newtonsoft 替代 JsonUtility | MEMORY 坑-004 |
| 自研穹顶 shader（URP 内置天空盒失效） | 坑-022 |
| Android 仅留 PXR_Loader（libopenxr_loader 撞库） | 坑-033 |
| 跨对象引用走 Configure 序列化字段 | 坑-026 |
| SNI 阻断→SSH:443 通道 | 坑-035 |
| 运行时状态 Play 时从数据源重建 | 坑-018 |
