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

## 三、分阶段优化路线图（R4 起按用户指令"批处理环境持续优化"滚动推进）

### R4 交互与场景补强（本轮）
**功能补强**（对照 FUNCTION_AUDIT）：
- F1/F12 房间入口可视化：RoomDoor 发光门框+牌匾（"前往老院子"）+走近自动切房（BoxTrigger→SwitchToRoomWithFade），双向对称
- F9 星河墙完成提示音
**UI 打磨**（对照 UI_DESIGN tokens）：
- U1 面板照片圆角框（amber 边框底图）
- U4 星点标签底板化（圆角贴图+描边）
- U2 预览卡标题截断（>12 字省略）
- F4/F5 面板/预览卡淡入（0.25s/0.15s alpha 渐变）
**验收升级**：
- 截图证据：Play 采样中 ScreenCapture → docs/evidence/（"不能只报已完成"）
- 新断言：门框存在/标签底板/淡入 alpha/截图文件生成

### R5 候选（下轮）
视频记忆（低码率）· 多维排序数据层 · 光点显隐开关接入 · VR 抓取排序 · 旁白接入（素材到位）

### 持续跳过项
真机/Emulator 装机 · GUI 目检 · devlog 录制 · 申报名单 · 旁白人声录音

---

## 三a、原路线图（R1-R3.5 已完成，存档见轮次表）

---

## 四、轮次记录

| 轮次 | 日期 | 内容 | 验收 | 提交 |
|---|---|---|---|---|
| R0 | 2026-11-12 | 团队组建+评估+路线图 | 本文档评审 | 随 R1 |
| R1 | 2026-11-12 | 交互闭环+类型配色：E2 悬停预览卡 / E3 面板关闭钮 / E4 类型四色+私密优先 / E5 选中音效（内聚 anchor，预制体级注入）/ E6 星点重演定位 / E7 bridge 路径重演第③拍出面板（wall 路径 R2 统一） | smoke30 全绿（B 段 +3 断言：配色/音效/预览卡；L 段 +关闭钮；H 段 +星点重演定位）；APK 回归 42.4MB ✓ | d2b2a02 |
| R2 | 2026-11-13 | 引导+音频+统一：E1 欢迎告示牌（WelcomeBoard，首条记忆后收起）/ E8 录音播放链路（UnityWebRequest 异步 WAV/MP3 + 自制 radio.wav 占位）/ E9 AudioSettingsService（master→AudioListener，narration→面板，ambient→环境音）/ E10 星点标题标签 / E11 接线退避 0.5s / wall 路径延迟出面板统一（bridge 订阅 OnStarSelected） | smoke31 全绿（23 段断言：+P 欢迎牌/L 录音播放/Q 音量服务/H wall 延迟链路）；APK 回归 42.9MB ✓ | 5fcb1ba |
| R3 | 2026-11-13 | 交付材料：①APPLICATION.md 申报骨架（创新点/技术方案/日志索引，【待填】团队/日志链接）②DEVLOG_SCRIPT.md 录制分镜（7 镜头 3:30 + 每周简版模板）③README 全面重写对齐 R2 现状（机制×断言对照表/构建入口/SDK 链）④PerfAudit 性能基线（62.5fps/403MB/粒子 200 ✓）→ docs/PERF_AUDIT.md | PERF_EXIT=0 报告落盘；【待用户】devlog 录制、申报【待填】项、GUI 目检 | e8a48bb |
| R6 | 2026-11-14 | 显隐开关入口可达性：TutorialGuide 第 5 步设置提示+StepText 访问器/SettingsPanel 0.02m 浮动显眼化/P2 段断言扩展/误拷双类清理/FUNCTION_AUDIT #12 ✅ | smoke39 全绿（28 段零 FAIL）+ APK 46.6MB ✓ | 1588616 |
| R7 | 2026-11-14 | 评测员代码级走查 4 项：RoomDoor 防循环切房（OnTriggerExit 重装填）/告示牌抬高 1.75m 不遮挡/选中后预览卡 Hide 保重演沉浸/视频记忆评估+坑-042 预登记 | smoke40 全绿（28 段）+ APK 46.6MB ✓ | 45ecca6 |
| R8 | 2026-11-14 | 视频记忆实现（坑-042 方案落地）：VideoPlayer+RT→RawImage/file:// 直读/inactive 自动停/ffmpeg 渐变测试视频 994KB（全约束达标）/frag_03 三媒体/L 段播放断言 | smoke41 全绿（28 段）+ APK 47.7MB ✓ | 47e8b53 |
| R10 | 2026-11-16 | 用户报告 Play 闪退 → Windows 事件日志定位 0x40000015（abort）→ 根因 SettingsPanel 凹面 MeshCollider trigger（坑-043）→ 换凸面 BoxCollider | smoke43 全绿（28 段零 FAIL）+ APK 回归 ✓ | 待 |

**R10 验收报告（验收工程师）**：
- 崩溃取证：Editor-prev.log（SettingsPanel concave trigger 警告链）+ Windows 事件日志（0x40000015 @ 11-14 0:16:30）。
- 修复后 smoke43 全绿 28 段零 FAIL，待用户 GUI Play 复测确认不再闪退。
| R11 | 2026-11-17 | 舒适性三新项（PICO 规范落地）：面板距离档位（1.5-3.0 默认 2.4）/舒适暗角（开关+强度 0.2/0.4，Global Volume Vignette）/移动方式（传送↔平滑，XR Rig 组件级切换）；探针 S 段静态安全扫描（凹面 trigger 防复发）；MemoryPanel 下倾 10°+距离档读取 | smoke60/61 全绿（30 段，T 段转静态断言见坑-044）+ APK 47.7MB ✓ | 4a46500 |
| R12-R14 | 2026-11-17 | 三轮收口合并：①UI 一致性令牌化（UiTokens 单一来源，净违规 32→0，白名单 14 登记 UI_STYLE_GUIDE）②空间合规运行时实测 4 项修复（水平角/垂直角/热区/标签字高，探针读世界坐标）③MoL path 要素（PulseEmissive 呼吸导引单焦点+传送后 0.5s 观感稳定期）④时间轴着色（星点色相渐变+排列视图，D2 段）⑤P2 收口量化（E9 音量对数五档 dB 映射表/E11 轮询退避 2s 内 4 次 vs 旧每帧 120/E10 星点标签确认已解）⑥闪退根治收口（CRASH_ROOTCAUSE 三级取证+40 分钟长跑零崩溃） | smoke43→92 逐轮全绿（28→29 段）+ APK 47.7MB ✓ | af2f82e/19cc0cc/6bb3ba2/0088805 |
| R15 | 2026-11-18 | 对标学习轮：联网检索 8 产品+4 论文/综述（LOCI Quest/Loci MR/Wander/Revisit/Guided Meditation VR/Mindergy PICO/LociVR/VR-RT 综述 + Reggente20/Moll23/Hagström24/arXiv2506.02700）→ docs/BENCHMARK.md 五条可落地优点；落地 ①记忆点符号浮标（LightPointSymbol：五类型程序化贴图 billboard，色形双编码，坑-051 非 Play 渲染空帧→证据截图并入 Play 协程）②回忆卡片版式合成（MemoryCardComposer 720×960：房间标题+真实截图缩略+路径点串末站高亮+关键词+时间+站数，专用相机绕坑-041）；smoke 新增 Y5/Z3 段 | smoke93 全绿 31 段 + APK 回归 + docs/evidence 符号特写×4+卡片 PNG 落盘 | 待 |
| R9 | 2026-11-14 | 静态一致性自查：UiTokens（11 色值单一来源）+UiTextFactory（TextMesh 工厂）收敛 6 文件重复/静态扫描零 TODO 残留/日志前缀统一/FUNCTION_AUDIT #11 🟡 #12 ✅/DEVICE_READINESS.md 真机就绪清单（锚点迁移方案静态准备） | smoke42 全绿（28 段，token 重构零破坏）+ APK ✓ | 4d294d5 |
| R5 | 2026-11-14 | 数据层+设置双项：①MemorySortService 多维排序（Time 日记日期升序/Tag 类型分组/Relevance 回访紧迫度，纯函数不改 order，UI 解耦）②SettingsService+SettingsPanel 设置面板（光点标记/星点标签显隐开关，翻转即持久化并全场景应用，schema 加 showStarLabels）③LightPointAnchor.SetMarkerVisible（隐标记留交互）/ConstellationWall.SetLabelsVisible | smoke38 全绿（28 段：+X 显隐开关+Y 多维排序）；APK 46.6MB ✓ | 待 |

**R5 验收报告（验收工程师）**：
- smoke38：28 段断言全绿。
- 构建回归：46.6MB ✓。
- 调试发现：①Y 段曾读被 K 段污染的 appdata（HTTP 日记测试改写 frag_01.diary.date）——段落顺序调整 X/Y→K 之前；②Z 段导入副本混入排序集——按 roomId 过滤原始房间；③MemorySortService 乱码字符（`f =�>`）——多行 Edit 的产物，整文件重写根治。
- 结论：R5 两项全 ✅，"手动排序优先"原则（排序不改 order）经 decoupled 断言验证。

| R3.5 | 2026-11-13 | 四线专项：①UI_DESIGN.md 设计 tokens+组件规范 ②ARCHITECTURE.md 3 张 mermaid 架构图 ③FUNCTION_AUDIT.md P0 全核对（发现导出导入缺口）④NARRATION_SCRIPT.md 8 条旁白文案 ⑤SfxLibrary 合成 4 SFX（缝纫/车铃/tick/翻页）⑥**ExportService/ImportService（P0 缺口补齐：zip+隐私过滤+id 冲突合并）** ⑦UI 好看升级：程序化圆角贴图+标题分隔线+光点光晕外球+Sprite3D shader | smoke33 全绿（Z 段导出过滤/导入后缀）；APK 44.7MB ✓ | 待 |

**R3.5 验收报告（验收工程师）**：
- smoke33：24 段断言全绿（+Z 导出/导入段：隐私过滤验证 frag_02 不入包、id 冲突自动后缀）。
- 构建回归：44.7MB（+UI 圆角贴图/SFX）✓。
- 新坑：坑-036（inactive GameObject 上 StartCoroutine 静默失败——Show 里先激活再起协程）。
- P0 清单：8/8 全 ✅（FUNCTION_AUDIT.md），**内测可发**。

| R4 | 2026-11-14 | 交互与场景补强+UI 打磨+截图证据（见"三、路线图 R4"）：RoomDoor 门框/走近切房、星点标签底板、预览卡截断、面板淡入+照片圆角框、截图证据（docs/evidence/） | smoke37（+R 段门框断言，预期 26 段）+ APK + 截图文件落盘 | 待 |

**R4 实施中备注（验收工程师）**：
- 工程锁冲突 2 次（用户 GUI 目检与批处理互斥）——已按坑-040 SOP 协调错峰。
- 截图证据走 PerformanceProbe 内 ScreenCapture（batchmode 兼容性待验证，失败则记录跳过）。

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
