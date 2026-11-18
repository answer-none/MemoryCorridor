# 性能预算实测（PERF_BUDGET v1）

> R17-3 batchmode 基线 + R18-1 真渲染补测 · smoke P3 段 / R18RealRenderProbe · Unity 2022.3.62f3c1
> 硬门槛：Draw Call ≤ 120 · 可见三角面 ≤ 60 万 · 平均帧耗时 ≤ 13ms（PICO 72fps 预算）

---

## §1 batchmode 采样（R17 P3 段，3 机位 × 10 帧均值）

| 机位 | 平均帧耗时 | Draw Call（口径） | 可见三角面 |
|---|---|---|---|
| 出生点 | 0.5 ms | 37（Renderer 上界） | 8k |
| 房间中心 | 0.5 ms | 24（Renderer 上界） | 7k |
| 窗面全览 | 0.8 ms | 10（Renderer 上界） | 2k |
| **最差值** | **0.8 ms** | **37** | **8k** |

## §2 真渲染采样（R18-1，GUI Play 模式，3 机位 × 30 帧）

> 采集：R18RealRenderProbe（Editor GUI 窗口真渲染，非 -batchmode），RoomManager/主题/桥接稳定后采样。
> 1% Low 口径：30 帧样本过小，取最差帧代表（真机需 300+ 帧窗口）；Draw Call 同 §1 上界口径。

| 机位 | 平均帧耗时 | 1% Low | Draw Call（口径） | 可见三角面 |
|---|---|---|---|---|
| 出生点 | 2.4 ms | 2.8 ms | 36（Renderer 上界） | 7.4k |
| 房间中心 | 2.6 ms | 3.3 ms | 28（Renderer 上界） | 5.7k |
| 窗面全览 | 2.5 ms | 3.0 ms | 11（Renderer 上界） | 2.6k |
| **最差值** | **2.6 ms** | **3.3 ms** | **36** | **7.4k** |

**§1 vs §2 口径差异与结论**：
- batchmode（§1）**带 GfxDevice 但 vsync 关、无 GUI 窗口合成** → 帧耗时 0.5-0.8ms 只反映渲染提交耗时；GUI Play（§2）叠加 **Editor 窗口合成与帧调度开销** → 2.4-2.6ms，更接近真实设备侧负载结构。
- **Draw Call / 三角面两口径一致**（几何统计与呈现方式无关，37 vs 36 为相机位微差）——两者均 ≤120 门槛（3× 余量）。
- 真渲染最差均值 2.6ms、1% Low 3.3ms，**距 13ms 硬线 5× 余量**；PICO 真机（ARM GPU + 单缓冲 72fps）仍待真机复核，本表为可参赛兜底数据的 Editor 上限。

## §3 主题对照真渲染量化（R18-1 重拍，门槛不改变）

- 平均色差 **21.4/255（≥20 ✅）**，色差>25/255 像素占比 **42%（≥30% ✅）**——真渲染 GUI 口径下达标。
- 证据：`docs/evidence/r18_theme_compare.png`（左右场景+实测值标注）、`r18_theme_default.png`、`r18_theme_rain.png`。
- R17 batchmode 对照（avg 22.4/255、ratio 46%）保留于 §4，两口径结论一致：主题切换差异真实可辨。

## §4 R15/R16 新增元素开销占比（相对出生点机位 36 DC）

| 元素 | 开销构成 | 占比/说明 |
|---|---|---|
| 记忆点符号浮标（R15①） | 每光点 +1 Renderer（0.10m quad，材质实例化 break SRP batcher） | **≈29%**（4 点 ≈11 DC / 36）——优化方向：按类型共享 5 份材质可合批 |
| 回忆卡片（R15②/R16） | 纯合成（临时卡板+专用相机渲一帧→PNG→DestroyImmediate），**运行时零留存** | 0%（瞬时 <50ms 一次） |
| 密度分簇（R16-2） | 零新增几何/渲染器——只改 emissive 强度与符号 scale | 0% |
| 卡片音频角标（R16-1） | 卡板内 quad+TextMesh，随卡片瞬时合成 | 0%（同上） |
| 音频讲解 HUD（R17-2） | 1 板+1 进度条+2 TextMesh = 4 Renderer，仅播放中显示 | ≈11%（播放时） |
| 主题切换（R17-1） | RenderSettings 参数切换（环境光/雾色/指数雾密度） | 0%（零几何） |

## §5 口径说明与待真机

- **Draw Call**：坑-054（FrameDebuggerUtility 2022.3 移除）——「视锥内可见 Renderer 数」上界口径（SRP batcher 下单 renderer≈1-2 DC），门槛按上界收紧仍有 3× 余量；**真机 Profiler 复核**（用户侧）。
- **三角面**：视锥 AABB 测试可见 MeshFilter triangles/3 累加 + TextMesh 每 glyph 24 近似——真数据。
- **帧耗时**：batchmode（§1）=渲染提交耗时口径；GUI Play（§2）=含窗口合成口径；两者均远低于 13ms。**PICO 4 Ultra 真机 72fps/13ms 硬线复核**（用户侧，结合 R11 长跑协议）。
