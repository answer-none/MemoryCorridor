# 交接文档 · HANDOFF

> 写于 2026-11-12 凌晨 · 移交对象：下一个接手的 agent
> **接手第一步：读完本文档 → 读 `docs/MEMORY.md`（踩坑记忆）→ 再动手。**
> 项目会议纪要式速览，细节以 `docs/TECH_DESIGN_V2.md` 为准。

---

## 一、项目速览

| 项 | 内容 |
|---|---|
| 项目 | 《回响长廊 MemoryCorridor》—— MR 个人与家庭记忆策展空间 |
| 赛事 | PICO Space Program 2026 · 第三赛道"最佳沉浸内容创作"（Unity/Unreal + OpenXR 全沉浸） |
| 技术栈 | Unity 2022.3.62f3c1（中国版）+ OpenXR + XR Interaction Toolkit + URP + PICO SDK（待导入） |
| 目标设备 | PICO 4 Ultra 一体机；开发全程用 PICO Emulator + Editor Play，无需自备头显 |
| 评分权重 | 创新25 + 功能设计25 + 视觉15 + 交互15 + 完成度10 + 路演10 |
| 制胜策略 | 三个签名机制拿创新分：**记忆褪色 / 回忆重演（四拍时序）/ 星河回顾**；P0 收敛为 8 项保完成度 |
| 工期 | 2026-11-11 → **12-19 09:00 提交截止**（38 天）；M1=11-23 闭环 alpha；M2=11-29 内测闭环；M3=12-12 特性冻结；决赛 12-26 北京 |
| 硬性要求 | 必须关联 GitHub 仓库（私有库需给 PICO Deploy Key）；开源可二开加分 |

## 二、仓库与路径地图

```
D:\unityxiangmu\MemoryCorridor\          ← 主工程（git 仓库，main 分支）
├── Assets/
│   └── Editor/SetupWizard.cs            ← W1 环境向导（菜单 MemoryCorridor/W1 环境向导）
├── Packages/manifest.json               ← 依赖已写死（xr.management 用 4.5.3，见坑-011）
├── docs/
│   ├── MEMORY.md                        ← ⭐ 踩坑记忆（每会话必读必更）
│   ├── TECH_DESIGN_V2.md                ← 唯一有效设计文档（P0/P1、视觉/交互规范、排期、路演剧本）
│   ├── RESEARCH_COMPETITORS.md          ← 竞品调研与差异化矩阵
│   ├── ASSET_LIST.md                    ← 素材授权登记（新素材必须登记）
│   └── HANDOFF.md                       ← 本文档
├── README.md                            ← GitHub 门面（随方案同步过）
├── AGENTS.md                            ← agent 工作规矩
├── assets_raw/                          ← 素材暂存：panoramas×2 / photos×10 / audio×3 / fonts×2
├── scripts/generate_placeholder_audio.py ← 占位音频合成脚本
└── staging/                             ← 待进工程的运行时脚本（见"任务队列"第3步）
    └── Scripts/{Core,Interaction,Media}/*.cs

D:\unityxiangmu\_mc_seed\                ← 废弃种子工程（含大 Library，可删腾空间）
C:\Users\Administrator\.zcode\workspace\draft-b849758c-889a-4dfb-b1ac-8af0a597cac4\MemoryCorridor\
                                         ← 仓库的 C 盘镜像（文档编辑来源，注意两边同步）
Unity Editor: D:\unityanzhuang1\2022.3.62f3c1\Editor\Unity.exe
PICO Emulator: D:\PICO_Emulator\swan_rls_spaceos_oversea_K_pico_emulator_win64_20260902\start-emulator.bat
```

## 三、当前状态快照（2026-11-12 00:20 左右）

**已完成：**
- 环境检测全过（Unity/Hub/VS2022+Unity工作负载/PDC/Emulator/adb，详见 MEMORY.md §1）
- 竞品调研 + 借鉴合并进 V2（差异化：褪色/隐私导出/锚定重演三独有）
- 素材 17 件全部落盘且授权合规（全景2、照片10、占位音频3、字体2——字体已下完并同步到 D 盘，字节数与 GitHub release 一致）
- Git 仓库初始化，首个提交 `7b64e29`（方案+素材+文档基线）
- Unity 种子工程创建并合并进仓库；manifest 依赖写死；SetupWizard 已放入 `Assets/Editor/`

**进行中（后台）：**
- 无。**W2 内容层第 1 天完成，smoke13 全绿**：全景穹顶（自研 shader，从 appdata 加载）+ 环境音 + 记忆面板（视线前方 1.5m，真实素材）+ 选中开面板 + **ImportServer HTTP 三连冒烟（上传落盘/日记入档实证）**。新增坑-022/023/024（URP 天空盒失效/CreateAsset 父目录/persistentDataPath 主线程限制）。
- **用户指示的跳过项（勿卡住）**：PICO SDK、GitHub 远端、devlog 录屏、Emulator GUI 验证、旁白音频素材、Android StreamingAssets 异步化（真机前）、UGUI 面板（W4）。
- **接手继续 W2**：星尘路径走读 + 手动排序 + RoomManager 房间切换（dome.SetPanorama 已备）+ ReplaySequencer 四拍时序 → M1 闭环 alpha（11-23）。

**未开始：** 运行时脚本进工程、光点预制体、射线冒烟、PICO SDK 导入、XR Device Simulator 配置。

## 四、任务队列（按序执行，含精确命令）

### 1. 确认向导结果
```bash
grep -E "SetupWizard|error CS|cannot be found" /d/unityxiangmu/MemoryCorridor/wizard4.log
# 四阶段应全 OK；失败项按日志提示处理（坑-010/011/013 均已记录在 MEMORY.md）
```

### 2. 清理与提交
- 删 `D:\unityxiangmu\_mc_seed\`（腾 ~2GB）；确认 D 盘剩余 >30GB
- `git add -A && git commit -m "wip: manifest 依赖+SetupWizard 进工程"`

### 3. 运行时脚本进工程（staging → Assets）
把 `staging/Scripts/` 整个拷到 `Assets/Scripts/`（Core 5 个文件 + Interaction/LightPointAnchor.cs 已写好；Media/* 还没写）。
已写好且编译应过：`AppData/MemberData/RoomData/MemoryFragment/SaveDataService(Newtonsoft)/FadeController/LightPointAnchor`。
**Media 层待写**：`MemoryPanel.cs`、`SpatialAudioAnchor.cs`、`LayeredPhoto.cs`、`ImportServer.cs`（HttpListener→落盘，见 TECH_DESIGN_V2 §5.2，注意坑-004 与 Android 冒烟时点）。

### 4. Editor Play 冒烟（W1 验收线）
- XRI 首次导入会弹"激活 Input System 示例"对话框——批处理环境下若卡住，改用 `-xriAutoImport` 不行就手动：包管理器里 XRI → Samples → 选 Import，或加符号链接跳过
- 给 XR Origin（XRI 3.x 叫 XR Origin (XR Rig)）装 XR Device Simulator（XRI Samples 里自带），键鼠模拟射线/瞬移
- EditorScene 里放 3 个光点（其中 1 个 `lastVisitedAt`=300 天前验证褪色变暗），射线悬停 0.3s 触发 OnHoverPreview、扳机触发 OnSelected 即达标
- 注意：SetupWizard 用的是 `ActiveInputHandler.Both`（坑-012 未证实风险：XRI 3.x 若报 InputSystem 未激活，改回 InputSystem 后重开工程）

### 5. PICO SDK 导入（本周内）
- 渠道：PDC（D:\pico\PICO Developer Center\）内置下载，或浏览器走代理从 developer-cn.picoxr.com 下 `.unitypackage`
- `download-sdk.picoxr.com` 被墙（坑-009 同源）
- 导入后：Project Settings → XR Plug-in Management → Android 勾 PICO Loader；按 PICO 文档确认 OpenXR 版本兼容（openxr 1.9.1 应该 OK，若 PICO SDK 要求特定版本再调 manifest）

### 6. 收尾 W1
- 更新 `docs/MEMORY.md` §5 当前状态 + 新坑
- 录 devlog #1（模拟器走动+射线悬停），存 `docs/devlogs/2026-W46-devlog1.mp4`
- git commit；建 GitHub 远端仓库 `MemoryCorridor`（gh 可用：`gh repo create MemoryCorridor --private --source .`）

## 五、关键决策记录（为什么这么做，勿轻易翻案）

| 决策 | 理由 |
|---|---|
| 选 Unity 不选 Unreal | 16GB 内存跑不动 UE 管线；Unity 全套已就位；PICO 生态 Unity 优先 |
| 全手柄射线为主，手势仅 P1 辅助 | 模拟器可调试、录屏稳定、评委上手零学习（竞品调研共识） |
| 不做视频记忆 P0 | 4GB 级显存+模拟器解码风险；图片/录音/日记已够叙事 |
| JsonUtility→Newtonsoft | 坑-004，V1 实测 null 字段不可控 |
| manifest 写死依赖而非运行时装包 | 域重载会打断 batchmode（第一版向导因此重写为同步版） |
| URP 用 SerializedObject 填 m_RendererDataList | `SetRenderer` 公开 API 各版本签名不一 |
| 素材全走合规授权 | 开源仓库+比赛放大版权风险；CC-BY 的客厅全景打包必须带署名（ASSET_LIST §4） |
| P0 第 8 项"欢迎房间" | 借鉴 immerGallery：评委零上传也要能完整体验 |

## 六、新坑速报（本次会话新增，详版见 MEMORY.md §3）

- **坑-010**：`https://packages.unity.cn/{pkg}/{version}` 探测 URL 一律假 404（连真实存在的版本也 404），**不可作为版本存在性判据**；`/{pkg}` 列表端点可用但 URP 显示不全（只见 ≤10.x，实际 14.0.11 能解析）。**唯一权威判据 = UPM 的报错日志。**
- **坑-011**：中国源确实缺 `com.unity.xr.management@4.4.2`，改 4.5.3 后依赖解析全过（其余包版本均在中国源存在，已验证）。
- **坑-013**：Unity API 凭记忆写连错 5 个（internetAccess / activeInputHandler / SwitchActiveBuildTarget 返回值 / AssignLoader 签名×2）。**规避：先 grep `Library/PackageCache/<包>/…/*.cs` 与 `UnityEditor.CoreModule.xml`，直接 grep .dll 无效**。正确用法已沉淀在 `Assets/Editor/SetupWizard.cs` 头注释。
- **坑-012（待证实）**：activeInputHandler 已通过直改 ProjectSettings.asset=2(Both) 设置，与 XRI 2.5.4 组合待 Play 冒烟验证；若 InputSystem 报未激活，改 YAML 值为 1。
- 启动日志 `Failed to handshake to channel: LicenseClient-*` = 坑-001 自愈型，忽略。

## 七、W1 完成定义（验收清单，给下一个 agent）

- [x] wizard 全绿（URP/Scenes/Android/OpenXR）
- [x] Assets/Scripts 三层目录（Core/Interaction/Media）编译零报错
- [x] EditorScene 含 3 个 LightPointAnchor 光点，自动化 Play 冒烟：悬停 0.3s 预览、扳机 Select 事件（smoke5.log）
- [x] 褪色可见：300 天样例 fade=0.39 vs 新样例 1.00（Play 时从 appdata.json 应用）
- [x] appdata.json 能存能读（Newtonsoft，v2 schema 含 settings/lastVisitedAt）
- [ ] devlog #1 录屏（需 GUI 手动操作）+ GitHub 远端建库（需用户账号确认）
- [x] MEMORY.md 更新完毕（坑-010~019）

**接手任务队列（原第 2~3 步已完成）**：XRI 正式接入（XR Origin/射线/Device Simulator，替换冒烟版 RayInteractor，契约 SetHighlight→TickHover→Select 不变）→ PICO SDK 导入 → GitHub 远端 → devlog 录屏。

**接手后遇到方案级疑问，优先读 TECH_DESIGN_V2.md 对应节；与本文档冲突时以 V2 文档为准。**
