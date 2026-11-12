# 团队分工与优化路线图（TEAM_PLAN）

> 建立日期：2026-11-12 · 主管维护 · 每轮迭代后更新轮次记录
> 目标：把《回响长廊》做到**可参赛、可内测交付**的最高完成度。M2 内测（11-29）→ M3 冻结（12-12）→ 提交（12-19 09:00）。

---

## 一、角色分工

| 角色 | 职责 | 当前主责事项 |
|---|---|---|
| **主管** | 统筹排期、裁决方案冲突、质量把关、每轮汇总 | 本文档维护；R1-R4 排期 |
| **UI 设计师** | 布局/信息层级/组件一致性/WorldSpace 面板可读性与反馈 | 预览小卡片、面板关闭钮、告示牌排版 |
| **美学大师** | 视觉调性/配色/光影/粒子/构图一致 | 光点四类型配色、重演琥珀色调、星点文案牌 |
| **Unity 全栈工程师** | 脚本/系统/场景/XR 交互/构建/性能 | 落地全部改动 + smoke 断言扩展 |
| **验收工程师** | 每轮断言/回归/构建，出验收报告 | smoke 系列维护 + APK 构建回归 |
| **项目评测员** | 评委/用户视角走查，问题排序 | R0 走查报告（见下） |
| **叙事策划**（增设） | 文案/叙事节奏/路演脚本 | 旁白文案表（待音频素材）、路演剧本核对 |
| **音频设计**（增设） | 环境音/音效/旁白接线 | 选中音效接入、音频缺失降级策略 |
| **XR/真机适配**（增设） | Emulator/真机/锚点标定 | 装机验证（用户侧 GUI），标定预案 |

> 工作方式：每轮先出方案→实现→验收（smoke 全绿+构建）→MEMORY/HANDOFF/git。跳过项（登录/被墙/GUI/真机）记录不卡住。

---

## 二、现状评估（R0，2026-11-12）

### 已完成节点
- 方案 V2 + 竞品调研 + 素材包（授权合规登记）
- 机制层全链路：光点/褪色/走读/回访复位/重演四拍（含跳过）/星河墙/房间切换/排序/转场/传送装配——**17 段 smoke 断言兜底**
- 视觉第一梯队：UGUI 磨砂面板（TMP 动态字体）/星尘粒子/星河墙缩略图/穹顶编辑期贴图
- Demo 叙事包：2 房间 8 碎片（褪色×2/私密×1/纯日记×1）
- PICO Integration SDK v3.3.3 集成 + PXR_Loader；**首个 APK 42MB 打包成功**（IL2CPP/ARM64/ASTC/Linear）

### 评测员 R0 走查报告（评委/用户视角，按优先级）

| # | 问题 | 影响 | 优先级 |
|---|---|---|---|
| E1 | **首次进入零引导**：评委不知道"走近光点→扳机选择→沿星尘走读" | 前 30 秒体验断裂 | P0 |
| E2 | **悬停预览无 UI**：OnHoverPreview 事件已发但无任何呈现（用户悬停 0.3s 后看不到任何反馈） | 交互承诺未兑现 | P0 |
| E3 | **面板无法关闭**：MemoryPanel.Show 后只能等重演覆盖，Hide() 无人调用 | 交互死角 | P0 |
| E4 | **光点无类型区分**：四类记忆一个颜色（规范 §4.1 类型配色未实现） | 视觉信息量低 | P1 |
| E5 | **选中无音效反馈**：sfx_lightpoint_chime.wav 已生成但从未接入 | 反馈缺失 | P1 |
| E6 | 星河墙点星重演发生在墙中心而非星点位置 | 体验错位 | P1 |
| E7 | 重演与面板同时出现，剧透日记（beat3 才该浮现） | 叙事节奏 | P1 |
| E8 | 音频类媒体的 audio 播放路径不存在（radio.wav 缺素材时静默降级 OK，但链路要通） | 功能完整性 | P1 |
| E9 | 音量设置（settings.volume）未接 AudioMixer/音频源 | 设置无效 | P2 |
| E10 | 星河墙无星点标签（评委不知道星星是什么） | 可读性 | P2 |
| E11 | XRIAnchorBridge 每帧 FindObjects（懒接线无退避） | 性能 | P2 |

### 风险与跳过项（持续跟踪）
- GUI 目检未做（视觉细节靠程序化判断）；旁白音频素材缺（用户侧 Freesound）；GitHub 未建库（gh 未登录）；Emulator/真机装机验证未做（GUI）
- D 盘 30GB（余量可控）；构建产物在仓库外 _builds/

---

## 三、分阶段优化路线图

### R1 交互闭环补全 + 类型配色（本轮）
**目标**：把"交互承诺"全部兑现——悬停有卡、选中有声、面板可关、光点有色、重演有节奏。
**改动方案**（全栈落地清单）：
1. 光点四类型配色（美学：image 琥珀/audio 青蓝/diary 米白/mixed 暖橙；私密暖红优先）——`LightPointAnchor.BindFragment` 增 type 参数
2. **HoverPreviewCard**（新组件）：世界空间预览小卡（缩略图+标题+日期），桥接器 OnHoverPreview 驱动显示、hoverExited 隐藏
3. MemoryPanel 关闭按钮（3D ✕ + XRSimpleInteractable → Hide()）——E3
4. 选中音效：bridge 注入 AudioClip（builder 序列化引用）+ PlayOneShot——E5
5. 星河墙点星重演位置改为星点位置——E6
6. 重演延迟出面板：sequencer.OnBeat(Beat3) 才 Show 面板，Done 兜底——E7（bridge 主路径；星河墙路径 R2 统一）
**验收**：smoke28 全绿（新增 N 预览卡段/O 关闭段/B 配色断言/H 音效+位置断言）；构建回归（增量 APK）。

### R2 引导 + 音频链路 + 星河墙统一
- 欢迎引导告示牌（E1）：世界空间说明牌（走近光点/扳机选择/星尘路径三句话）
- audio 媒体播放链路（E8）+ 音量设置接线（E9）
- 星河墙路径统一"重演延迟出面板"（E7 收尾）+ 星点标签（E10）
- 桥接器接线退避优化（E11）

### R3 交付材料 + 性能
- devlog 录制脚本、申报文档骨架、README 对齐实际
- 性能预算核对（粒子/加载/帧率日志）
- GitHub 建库提醒（用户侧）

### R4 内测反馈消化（M2 后）
- 按 2-3 位内测用户反馈排 P0 修复

### 持续跳过项
GitHub 登录 / 旁白素材下载 / GUI 目检 / Emulator 装机——记录在 MEMORY §5，不卡轮次。

---

## 四、轮次记录

| 轮次 | 日期 | 内容 | 验收 | 提交 |
|---|---|---|---|---|
| R0 | 2026-11-12 | 团队组建+评估+路线图 | 本文档评审 | 随 R1 |
| R1 | 2026-11-12 | 交互闭环+类型配色：E2 悬停预览卡 / E3 面板关闭钮 / E4 类型四色+私密优先 / E5 选中音效（内聚 anchor，预制体级注入）/ E6 星点重演定位 / E7 bridge 路径重演第③拍出面板（wall 路径 R2 统一） | smoke30 全绿（B 段 +3 断言：配色/音效/预览卡；L 段 +关闭钮；H 段 +星点重演定位）；APK 回归 42.4MB ✓ | d2b2a02 |
| R2 | 2026-11-13 | 引导+音频+统一：E1 欢迎告示牌（WelcomeBoard，首条记忆后收起）/ E8 录音播放链路（UnityWebRequest 异步 WAV/MP3 + 自制 radio.wav 占位）/ E9 AudioSettingsService（master→AudioListener，narration→面板，ambient→环境音）/ E10 星点标题标签 / E11 接线退避 0.5s / wall 路径延迟出面板统一（bridge 订阅 OnStarSelected） | smoke31 全绿（23 段断言：+P 欢迎牌/L 录音播放/Q 音量服务/H wall 延迟链路）；APK 回归 42.9MB ✓ | 5fcb1ba |
| R3 | 2026-11-13 | 交付材料：①APPLICATION.md 申报骨架（创新点/技术方案/日志索引，【待填】团队/日志链接）②DEVLOG_SCRIPT.md 录制分镜（7 镜头 3:30 + 每周简版模板）③README 全面重写对齐 R2 现状（机制×断言对照表/构建入口/SDK 链）④PerfAudit 性能基线（62.5fps/403MB/粒子 200 ✓）→ docs/PERF_AUDIT.md | PERF_EXIT=0 报告落盘；【待用户】devlog 录制、申报【待填】项、GUI 目检 | e8a48bb |
| R3.5 | 2026-11-13 | 四线专项：①UI_DESIGN.md 设计 tokens+组件规范 ②ARCHITECTURE.md 3 张 mermaid 架构图 ③FUNCTION_AUDIT.md P0 全核对（发现导出导入缺口）④NARRATION_SCRIPT.md 8 条旁白文案 ⑤SfxLibrary 合成 4 SFX（缝纫/车铃/tick/翻页）⑥**ExportService/ImportService（P0 缺口补齐：zip+隐私过滤+id 冲突合并）** ⑦UI 好看升级：程序化圆角贴图+标题分隔线+光点光晕外球+Sprite3D shader | smoke33 全绿（Z 段导出过滤/导入后缀）；APK 44.7MB ✓ | 待 |

**R3.5 验收报告（验收工程师）**：
- smoke33：24 段断言全绿（+Z 导出/导入段：隐私过滤验证 frag_02 不入包、id 冲突自动后缀）。
- 构建回归：44.7MB（+UI 圆角贴图/SFX）✓。
- 新坑：坑-036（inactive GameObject 上 StartCoroutine 静默失败——Show 里先激活再起协程）。
- P0 清单：8/8 全 ✅（FUNCTION_AUDIT.md），**内测可发**。

**R2 验收报告（验收工程师）**：
- smoke31：23 段断言全绿。
- 构建回归：42.9MB ✓。
- 调试中发现并修复：①MemoryPanel.cs 被误拷至 Scripts/Core/（双类定义）；②坑-026 变体再现——`ConstellationWallRef`/`WelcomeBoardRef` 属性赋值不序列化 → 改 Configure 序列化字段；③bridge/panel 整文件重写（多轮 Edit 出重复定义）；④`UnityWebRequest.Result` 是嵌套枚举（坑-029 同类）。
- 遗留到 R3：面板朝向/引导牌观感 GUI 目检；音效音量未走 narration 组（chime 走 master 全局即可）；ASSET_LIST 补 radio.wav 登记（自制）。

**R3 验收报告（验收工程师）**：
- 交付材料四件套落盘：APPLICATION（骨架）/DEVLOG_SCRIPT（分镜）/README（重写）/PERF_AUDIT（基线）。
- PerfAudit 首版失败（坑-026 编辑器版：Play 域重载丢静态订阅）→ 改场景组件 PerformanceProbe ✓。
- 性能基线：62.5fps（限 72）/403MB 分配/粒子 200——预算内；Mono 646MB 异常偏高待真机复核（编辑器 Mono 运行时差异，非阻塞）。
- 待用户：按 DEVLOG_SCRIPT 录制、申报【待填】、GUI 目检。

**R1 验收报告（验收工程师）**：
- smoke30：20 段断言全绿，零回归。
- 构建回归：AndroidBuildScript.Build 成功，42.4MB。
- 遗留：R2 项目（E1 引导告示牌、E8 音频链路、E9 音量接线、E10 星点标签、E11 接线退避、wall 路径延迟出面板统一）。
- 评测员备注：预览卡/关闭钮的**视觉观感**待 GUI 目检（用户侧），程序断言只覆盖存在性与数据正确性。
