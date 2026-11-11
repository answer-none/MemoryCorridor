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

### 坑-010 中国包源的版本探测方法全不可信（2026-11-12 实测）
- **现象**：`https://packages.unity.cn/{pkg}/{version}` 一律假 404（真实存在的版本也 404）；`/{pkg}` 列表端点能用但数据不全（URP 只显示 ≤10.x，实际 14.0.11 可解析）。
- **规避**：**唯一权威判据 = UPM 的报错日志**（"Project has invalid dependencies" 只列真正缺失的包）。别用 curl 探测版本存在性。

### 坑-011 中国源缺 com.unity.xr.management@4.4.2（已解决）
- **解决**：manifest 改 `4.5.3` → 第二轮批处理依赖解析全过（openxr 1.9.1 / XRI 2.5.4 / inputsystem 1.7.0 / URP 14.0.11 / newtonsoft 3.2.1 中国源都有）。

### 坑-013 Unity API 凭记忆写必翻车——本地源码/XML 是唯一权威（2026-11-12 实测 5 连错）
- **现象**：SetupWizard 连错 5 个 API：`PlayerSettings.Android.internetAccess`（不存在→`forceInternetPermission`）、`PlayerSettings.activeInputHandler`（本版本全无→直改 ProjectSettings.asset YAML）、`SwitchActiveBuildTarget` 返回 bool 不是 BuildReport、`AssignLoader` 先猜 4 参又错（4.5.3 实际 3 参）。
- **规避**：写编辑器 API 前先 grep 包缓存源码 `Library/PackageCache/<包>@<版本>/…/*.cs`（最权威）；编辑器自带 API 用 `Editor/Data/Managed/UnityEngine/UnityEditor.CoreModule.xml` 按 `"M:命名空间.类.方法"` 模式搜；**直接 grep .dll 没用**（元数据压缩，连存在的字符串都搜不到）。在线文档与 c1 中国版可能不一致，以本机为准。
- **已沉淀**：SetupWizard.cs 头注释写明全部正确用法。

### 坑-014 XR Plug-in Management 4.5.3 批处理初始化（已解决）
- **现象**：批处理里 `XRGeneralSettingsForBuildTarget(Android)` 返回 null——XR 设置资产（`Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset`）首次不存在。
- **解决**（已写进 SetupWizard）：反射调 internal `XRGeneralSettingsPerBuildTarget.GetOrCreate()` 建容器 → 公开方法 `CreateDefaultSettingsForBuildTarget(Android)` + `CreateDefaultManagerSettingsForBuildTarget(Android)` → 再 `AssignLoader(settings.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", Android)`（3 参）。
- **方法论**：包源码就在 `Library/PackageCache/<包>@<版本>/`，先列公开方法（grep "public \|internal "）再写代码，别猜。

### 坑-015 Unity 退出时会覆写手工编辑的 ProjectSettings.asset（已解决）
- **现象**：向导运行中把 `activeInputHandler: 0→2` 写进 YAML，Unity 退出序列化时又按内存值（0）覆写回去。
- **解决**：凡手工改 ProjectSettings/*.asset，必须在 **Unity 进程完全退出后** 用 sed 改（Wizard 第 5 轮起已并入批处理尾部）。
- **通用**：Unity 运行期间手改序列化文件 = 无效操作。

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
- **已完成**：方案 V2、竞品调研、素材包 17 件（全部合规并登记）；Git 仓库（首提交 7b64e29）；种子工程合并；manifest 解析通过（坑-011→4.5.3）；SetupWizard API 修复（坑-013）；交接文档 HANDOFF.md；`_mc_seed` 已删
- **进行中**：SetupWizard 第五轮批处理（wizard4=3/4 成功，OpenXR 阶段因 XR 设置资产未初始化失败→坑-014 已修；本轮含退出后 activeInputHandler 补丁→坑-015）
- **下一步**：查 wizard5.log 四阶段结果 → staging 脚本进 Assets/Scripts（Core+Interaction 已写好，Media 层未写）→ 光点预制体+射线冒烟 → PICO SDK 导入 → GitHub 远端（gh 可用）
- **待办提醒**：正式环境音手动下载（Freesound/Pixabay，ASSET_LIST §2）
