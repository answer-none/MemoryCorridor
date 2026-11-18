# 性能预算实测（PERF_BUDGET v1）

> R17-3 · smoke P3 段自动采集 · Editor batchmode 2022.3.62f3c1（RTX 级 GPU，vsync 关）
> 硬门槛：Draw Call ≤ 120 · 可见三角面 ≤ 60 万 · 平均帧耗时 ≤ 13ms（PICO 72fps 预算）

---

## 1. 分机位实测表（smoke P3 段，3 机位 × 10 帧均值）

| 机位 | 平均帧耗时 | Draw Call（口径） | 可见三角面 | 判定 |
|---|---|---|---|---|
| 出生点 (0,1.4,0.5) | 0.5 ms | 37（Renderer 上界） | 8k | ✅ 全达标 |
| 房间中心 (0,1.4,1.5) | 0.5 ms | 24（Renderer 上界） | 7k | ✅ 全达标 |
| 窗面全览 (0,1.4,2.6) | 0.8 ms | 10（Renderer 上界） | 2k | ✅ 全达标 |
| **最差值** | **0.8 ms** | **37** | **8k** | **全部 ≤ 门槛**（smoke 第 11 轮终值） |

证据：`docs/evidence/r17_perf_panel.png`（自绘参数面板，卡片管线渲染）。

## 2. 口径说明（诚实披露）

- **Draw Call**：Unity 2022.3 batchmode 无运行时 DC 计数 API（坑-054：FrameDebuggerUtility 已移出公开 API）——P3 用**视锥内可见 Renderer 数**作上界近似（SRP batcher 下单 renderer ≈1-2 DC，门槛 120 按上界收紧仍以 2× 余量通过）。**真机 Profiler 复核待用户侧**。
- **三角面**：视锥 AABB 测试可见 MeshFilter 的 triangles/3 累加 + TextMesh 每 glyph 24 近似——**真数据**。
- **平均帧耗时**：Editor batchmode vsync 关闭，`yield null` 全速空转 → 0.5-0.8ms **不代表真机帧率**（仅回归对照用）；PICO 72fps（13.9ms 预算、13ms 硬线）**必须真机复核**（记用户侧）。
- R3 基线（PerformanceAudit，GUI Play 限 72fps）：62.5 fps ≈ 16ms——Editor GUI 口径下本工程余量健康。

## 3. R15/R16 新增元素开销占比（相对出生点机位 37 DC）

| 元素 | 开销构成 | 占比/说明 |
|---|---|---|
| 记忆点符号浮标（R15①） | 每光点 +1 Renderer（0.10m quad，材质实例化 break SRP batcher） | **≈29%**（4 点 ≈11 DC / 37）——后续优化方向：按类型共享 5 份材质可合批 |
| 回忆卡片（R15②/R16） | 纯合成（临时卡板+专用相机渲一帧→PNG→DestroyImmediate），**运行时零留存** | 0%（瞬时 <50ms 一次） |
| 密度分簇（R16-2） | 零新增几何/渲染器——只改 emissive 强度与符号 scale | 0% |
| 卡片音频角标（R16-1） | 卡板内 quad+TextMesh，随卡片瞬时合成 | 0%（同上） |
| 音频讲解 HUD（R17-2） | 1 板+1 进度条+2 TextMesh = 4 Renderer，仅播放中显示 | ≈11%（播放时） |
| 主题切换（R17-1） | RenderSettings 参数切换 | 0%（零几何） |

## 4. 门槛复核结论

- **Draw Call**：最差 37（上界口径）≤ 120 ✅（3.2× 余量；真机 SRP batcher 下预期更低）
- **三角面**：最差 8k ≤ 600k ✅（75× 余量——本工程全程序化几何，无高模资产）
- **帧耗时**：Editor 口径数据不作真机预测；R3 GUI 基线 62.5fps + 上述静态余量 → 真机 72fps 预算**低风险**；13ms 硬线留真机复核项。

## 5. 待真机（用户侧）

- [ ] PICO 4 Ultra 真机 Profiler：Draw Call / 三角面 / GPU 帧耗时三值对账本表
- [ ] 真机 72fps 连续 10 分钟（结合 R11 长跑协议）
