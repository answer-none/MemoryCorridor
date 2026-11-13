# 真机就绪清单（DEVICE_READINESS v1）

> R9 静态准备 · 真机窗口（12-20→12-25 预约）前逐项核对 · 本文档不含真机执行记录

---

## 1. 锚点适配方案（静态记录，不跑真机）

**现状**：光点位姿以**世界坐标**存 appdata（RoomManager.SpawnRoomAnchors 直接实例化到坐标）——Editor/模拟器正确，真机重启后漂移。

**迁移方案（真机窗口内执行）**：
1. `PICO Integration SDK` 的 Spatial Anchor API（`PXR_SpatialAnchorSystem`）：
   - SpawnRoomAnchors 前先查锚点缓存（`LoadSpatialAnchor`），命中则用锚点位姿替代 appdata 世界坐标；
   - 未命中则按 appdata 坐标创建锚点并 `SaveSpatialAnchor`（房间 id 为锚点名）。
2. 代码占位：`RoomManager.SpawnRoomAnchors` 已预留数据驱动结构——真机版仅需在 `fragment.anchor.GetPosition()` 处替换为锚点位姿（单点改动）。
3. 回退策略：锚点创建失败 → 按世界坐标放置（现状），功能不受损。

## 2. 真机装机前配置核对

- [x] XR Plug-in Management → Android：PXR_Loader 已勾（Assets/XR/Loaders/PXR_Loader.asset）
- [x] OpenXRLoader 已在 Android 构建时移除（坑-033，AndroidBuildScript 自动处理）
- [x] 包名 com.memorycorridor.app / minSdk 29 / INTERNET 权限
- [ ] PICO 权限清单：Proximity（PICO 4 接近传感器）/麦克风（若用录音）——装机时按 PDC ProjectValidation 提示逐项确认
- [ ] Emulator 装机验证（PDC 应用管理或 adb install）——用户侧 GUI
- [ ] 真机 72fps 性能复核（PDC 性能监测对照 PERF_AUDIT.md 基线）

## 3. 已知真机差异预登记（来自坑库）

| 差异点 | 来源 | 预案 |
|---|---|---|
| StreamingAssets 不能 File 直读（Android jar 路径） | MEMORY 待办 | MediaResolver 加 UnityWebRequest 分支（真机前实现） |
| 硬件解码差异（H.264/HEVC profile） | 坑-042④ | 真机抽测 memories.mp4；不兼容则 ffmpeg 转基线 profile |
| 锚点漂移 | 本文 §1 | Spatial Anchor 迁移方案 |
| PDC ProjectValidation 警告 | 构建期已知 | 装机窗口逐项过 |

## 4. 验证顺序（真机窗口）

Emulator 装机 → 播放链路（光点/面板/视频/录音）→ 性能（PDC 监测）→ 真机 4 Ultra → 锚点迁移方案验证 → 录屏交付。
