# 开发记忆 · 踩坑与经验（MEMORY.md）

> **维护规矩（硬性）**：每次会话结束前更新本文档；每踩一个新坑立刻记录；每条坑必须带"规避方式"。宁可记多不可漏记。
> 用途：跨会话/跨人协作的长期记忆，防止重复踩坑。新会话先读此文件再动手。

---

## 1. 环境事实（已实测确认，勿重复探测）

| 项 | 值 | 备注 |
|---|---|---|
| Unity Editor | 2022.3.62f3c1 **中国版**，`D:\unityanzhuang1\2022.3.62f3c1\Editor\Unity.exe` | 含 Android Build Support + OpenJDK + Android SDK + NDK（全在 Editor\Data\PlaybackEngines\AndroidPlayer\） |
| Unity Hub | 3.3.7，`D:\unity\Unity Hub\Unity Hub.exe` | |
| adb | `D:\unityanzhuang1\2022.3.62f3c1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe` | 未加入 PATH，用时全路径 |
| VS2022 | Community，**已含 Unity 工作负载**（vswhere 验证过：`Microsoft.VisualStudio.Workload.ManagedGame`） | 验证命令：`"/c/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe" -requires Microsoft.VisualStudio.Workload.ManagedGame` |
| PICO Developer Center | v1.5.0，`D:\pico\PICO Developer Center\` | |
| PICO Emulator | 0.10.0-preview，`D:\PICO_Emulator\swan_rls_spaceos_oversea_K_pico_emulator_win64_20260902\start-emulator.bat` | preview 版，只做每周验证，不做日常依赖 |
| 硬件 | i9-12900H / RTX 3060 Laptop 6GB / 内存 16GB / Win11 Pro 26200 | WMI 显示 4GB 是 32 位截断假象，实际 6GB |
| 磁盘 | C: ~21GB free；D: ~32GB free（曾 45GB，持续下降，<20GB 必须清理） | 工程一律放 D 盘 |
| 工具链 | git 2.55 / gh 2.102 / node 24 / python 3.14 | gh 已可用（公开仓库匿名 API） |

## 2. 网络矩阵（这台机器在中国，GFW 拦截明显）

| 域名 | 状态 | 用法 |
|---|---|---|
| github.com / api.github.com / objects.githubusercontent.com / codeload.github.com | ✅ 通 | GitHub 下载走 `gh api repos/{o}/{r}/contents/{path} -H "Accept: application/vnd.github.raw"` 或 `gh release download` |
| raw.githubusercontent.com | ❌ 墙 | 用上面 gh api 方式绕过 |
| images.pexels.com | ✅ 通 | 照片直链 `https://images.pexels.com/photos/{id}/pexels-photo-{id}.jpeg?auto=compress&cs=tinysrgb&w=1024` |
| images.unsplash.com | ✅ 通 | 但 unsplash.com 主站墙，拿不到 ID 映射 |
| packages.unity.cn / packages.unity.com | ✅ 通 | UPM 正常 |
| developer-cn.picoxr.com | ✅ 通(307) | PICO 文档/SDK 下载入口 |
| download-sdk.picoxr.com | ❌ 墙 | PICO SDK 若走此域名需换渠道（PDC 内置下载或手动浏览器） |
| dl.polyhaven.org / api.polyhaven.com | ❌ 墙 | 全景图改从 three.js 仓库拿（gh api raw） |
| cdn.pixabay.com / upload.wikimedia.org | ❌ 墙 | 素材需手动浏览器+代理下载 |
| WebFetch 工具 | api.polyhaven 带查询参数会 400 | JSON API 尽量不带 & 参数；或让模型总结页面 |

## 3. 已踩坑记录（按时间序，只增不删）

### 坑-001 Unity 启动日志出现 licensing 报错
- **现象**：`D:\unityxiangmu\_fail.txt` 出现 `Code 10 while verifying Licensing Client signature`、`Access token is unavailable`。
- **真相**：瞬时授权校验抖动，日志随后 `Successfully updated license` + `resolved entitlement`，编辑器正常启动。
- **规避**：看到 Code 10 先查日志后半段是否自愈，别急着重装。

### 坑-002 VS2022 是否装 Unity 工作负载，手工查目录会误判
- **现象**：在 `CommonExtensions/Microsoft` 下 find "*unity*" 找不到 → 误判未安装。
- **规避**：只用 vswhere 判定：`vswhere -requires Microsoft.VisualStudio.Workload.ManagedGame`（实测已安装）。

### 坑-003 WMI 查显存显示 4GB
- **现象**：`wmic path win32_videocontroller get adapterram` = 4293918720（≈4GB）。
- **真相**：WMI 32 位字段截断，RTX 3060 Laptop 实际 6GB。
- **规避**：性能预算按 6GB 算，但设计仍按保守 4GB 做（一体机才是真目标）。

### 坑-004 JsonUtility 反序列化 null 字段不可控（V1 BUG-004 同源）
- **现象**：嵌套对象（diary/mediaItems/layers）为 null 时 JsonUtility 行为怪异、编译期还易踩"字段未初始化即引用"（V1 BUG-003）。
- **规避**：全项目 JSON 一律用 `com.unity.nuget.newtonsoft-json`（3.2.1），字段显式类型、显式初始化。

### 坑-005 下载文件必须做魔数校验
- **现象**：CDN 失败时可能落盘 HTML 错误页且后缀仍是 .jpg。
- **规避**：下载后立即校验（JPEG=`ffd8ff`），失败即删，本会话 10 张 Pexels 照片全按此流程零坏文件。已沉淀为标准动作。

### 坑-006 Bash 里 PowerShell 命令的 `$_` 会被 Git Bash 展开
- **现象**：`powershell -Command "... $_.Used ..."` 报解析错误（$_ 被 bash 替换成路径）。
- **规避**：PowerShell 单行命令里的 `$` 全部转义为 `\$`，或改用简单命令/写 .ps1 文件执行。

### 坑-007 WebSearch 并发上限
- **现象**：同批 4 个 WebSearch 第 4 个报 `user concurrency limit exceeded`。
- **规避**：WebSearch 每批最多 3 个并行。

### 坑-008 素材授权红线
- **现象**：随手抓的图/音频可能版权不明，比赛开源仓库会放大风险。
- **规避**：只进 CC0/CC-BY/OFL/Pexels License/自制素材；每个文件登记 `docs/ASSET_LIST.md`；CC-BY 的 `living_room_warm.jpg` 打包必须带署名。

### 坑-009 Unity Hub 下载模块 ECONNRESET（V1 BUG-001）
- **现状**：已绕过（Android 模块当时已装好）。
- **规避**：以后补装 Unity 模块若再遇 ECONNRESET，检查代理/重试，不要重复下载已完整的模块。

## 4. 工作规矩（每会话自查）

1. 开工先读本文档 + `docs/TECH_DESIGN_V2.md` 变更记录；结束时更新本文档"当前状态"。
2. 素材：下载→魔数校验→`assets_raw/` 对应子目录→登记 `ASSET_LIST.md`（来源/授权）。
3. 代码：显式类型（禁隐式推断）；JSON 用 Newtonsoft；命名空间 `MemoryCorridor.*`。
4. Git：小步提交，提交信息中文说清"做了什么+为什么"；`Library/`、`Temp/`、`Logs/`、`obj/`、构建产物不进库（.gitignore 已配）。
5. 每周六录 devlog 到 `docs/devlogs/`；M3（12-12）后只修不加。
6. D 盘 <20GB 时停下清理；Unity batchmode 同时只跑一个实例。
7. 改动涉及方案层面的，同步更新 `TECH_DESIGN_V2.md` 并在文首"最后更新"改日期。

## 5. 当前状态（每次会话末更新）

- **阶段**：W1 地基（2026-11-12 开始）
- **已完成**：方案 V2、竞品调研、素材包（2 全景/10 照片/3 占位音频/字体下载中）
- **进行中**：Unity 工程创建（D:\unityxiangmu\MemoryCorridor）、Git 初始化
- **下一步**：manifest 包配置 → SetupWizard（URP/OpenXR/XRI）→ 光点预制体 → 射线交互冒烟
- **待办提醒**：正式环境音需手动下载（Freesound/Pixabay，见 ASSET_LIST §2）
