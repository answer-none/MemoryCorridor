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

### 坑-017 协程 yield 不得位于 try/catch 内（CS1626）
- **现象**：SmokeProbe 首版把 `yield return null` 写进 try 块，编译失败。
- **规避**：协程断言逻辑不包 try/catch；需要兜底时另起独立看门狗协程（`WaitForSecondsRealtime(60)` + `EditorApplication.Exit(1)`），批处理 Play 冒烟必须配看门狗防挂死。

### 坑-018 非序列化私有字段的编辑期改值 = 空气（双杀坑）
- **现象 A**：编辑期调用 `SetFade(0.39)` 修改非 [SerializeField] 的私有字段，场景保存后 Play 加载时重置为 1——冒烟褪色断言首跑 FAIL。
- **规避**：**运行时状态不要在编辑期烘焙进场景**；Play 时由控制器从数据源（appdata.json）重建（FadeController.Start → ApplyAllFromDisk）。这反而让冒烟覆盖了真实数据流。
- **现象 B**：batchmode Play 帧率不设限，"90 帧"可能只够 0.09s——悬停 0.3s 阈值按帧数等待永远不触发。
- **规避**：批处理探针等待一律用 `Time.realtimeSinceStartup` 时间预算，不用帧数。

### 坑-019 批处理场景搭建器必须幂等
- **现象**：搭建器连跑三轮后场景里 6 个光点、2 个 FadeController（每轮叠加一次）；且 `GameObject.Find` 只返回第一个匹配，按名清理会漏删。
- **规避**：搭建器进场景后先用 `FindObjectsByType<T>` 按类型 DestroyImmediate 全部旧实例（光点/控制器/探针），再重新布置。修改后验证：FadeController 日志应恰好出现 1 次、光点数恒为 3。

### 坑-020 XRI 官方样本脚本在无显示批处理下抛异常（无害，勿修）
- **现象**：Play 时 `XRDeviceSimulatorUI.Initialize` 抛 ReadOnlyArray index 越界；`StarterAssetsSampleProjectValidation.AddRules`（Editor 延迟调用）抛 NRE。
- **结论**：都是样本自身的显示/编辑器 UI 依赖，批处理无屏幕导致，不影响核心模拟器与装配；GUI 编辑器里正常。别浪费时间修它们。

### 坑-021 无 XR 设备时 XRInputModalityManager 会禁用控制器层级（Play 断言必含未激活对象）
- **现象**：编辑期诊断 Rig 完整（Ray Interactor 全激活），Play 期探针按默认 `FindObjectsByType`（排除未激活）搜 XRRayInteractor = 0，误判"装配缺失"。
- **真相**：无 XR 设备时 XRInputModalityManager 把 Left/Right Controller 整个 SetActive(false)——**这是正确运行时行为**（真机上设备接入会自动恢复）。
- **规避**：装配完整性断言用 `FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)`；交互功能验证放 GUI/模拟器步骤。

### XRI 接入备忘（2026-11-12，W1 完成）
- 官方样本已连 meta 整包拷入 `Assets/Samples/XR Interaction Toolkit/2.5.4/`（Starter Assets + XR Device Simulator，GUID 引用完整）。
- 光点预制体挂 `XRSimpleInteractable`；`XRIAnchorBridge` 把 hoverEntered/Exited→SetHighlight、selectEntered→Select，悬停期间逐帧 TickHover——锚点契约与冒烟版 RayInteractor 一致。
- 场景搭建器现在装配：XR Origin (XR Rig) + XR Device Simulator + FadeController + XRIAnchorBridge + 3 光点 + SmokeProbe，全程幂等。
- 批处理冒烟命令不变（BuildAndSmoke），不传 -quit。

### 坑-022 URP 工程里 Skybox/Panorama 内置天空盒不可靠（已解决，自研穹顶）
- **现象**：`Shader.Find("Skybox/Panorama")` 在 URP 工程批处理里返回 null（内置 shader 未被引用不加载）。
- **解决**：自研 `Assets/Shaders/PanoramaDome.shader`（倒置球 Cull Front + 等距柱状 UV）+ `PanoramaDome.cs`（Start 从 appdata.panoramaPath 经 MediaResolver 加载贴图，真实数据流）。W3 房间切换只需 dome.SetPanorama(texture)。

### 坑-023 AssetDatabase.CreateAsset 不自动创建父目录
- **现象**：`CreateAsset("Assets/Materials/…")` 在目录不存在时直接 UnityException。
- **规避**：编辑器脚本写资产前 `if (!IsValidFolder) CreateFolder(...)`。

### 坑-024 Application.persistentDataPath / streamingAssetsPath 只能主线程调用（重要）
- **现象**：ImportServer 后台线程调 `Application.persistentDataPath` → UnityException，HTTP 500。
- **解决**：`SaveDataService.WarmCache()`（主线程入口调用一次）缓存静态字段；后台线程读 `SaveDataService.PersistentRoot/StreamingRoot`；MediaResolver 已切换。
- **通用**：Unity Application.* 路径属性在 Thread/ThreadPool 里都是雷，模式=主线程预热+静态缓存。

### 坑-025 Start 顺序竞态：动态生成的对象不能被别的 Start 缓存
- **现象**：桥接器 Start 缓存光点数组，但 RoomManager.Start 才生成光点——谁先谁后不确定，先到者拿空集。
- **解决**：桥接器改 Update 懒接线（HashSet 去重，每帧补接新光点，房间切换后自动生效）；RoomManager 光点命名固定 `LP_{fragmentId}` 供定位。

### 坑-026 编辑期 C# 事件订阅不序列化（域重载即丢）
- **现象**：搭建器在编辑期 Configure 里给 RoomManager.OnRoomSwitched 挂 RoutePlayer 订阅 → 进 Play 域重载全丢 → 房间切换后走读不终止。
- **规避**：跨对象事件订阅必须在 **Play 期的 Start/Awake** 里做（编辑期 Configure 只序列化字段引用）；订阅用 `-=` 再 `+=` 防重复。

### 坑-027 顺序移动要按 order 值找交换对象，不是列表下标
- **现象**：MoveFragment 按"列表位置+delta"找邻居——列表顺序≠order 顺序（交换后立刻错位），前移出界。
- **规避**：先取 self.order，按 `order+delta` 查交换对象；这也是为什么探针要"交换+恢复"两次验证。

### W2 续备忘（2026-11-12 第 2 批，smoke19 全绿）
- 新增：`RouteEditor`（手动排序，order 交换落盘）、`RoomManager`（两房间切换：卸载/生成光点+穹顶 SetPanorama+环境音换 clip）、`RoutePlayer`（走读路线+星尘信标+MarkVisited 回访复位褪色）、`ReplaySequencer`（四拍：环境光压暗→LayeredPhoto 展开→旁白位→复原；SkipToEnd 跳过；OnDisable 兜底复原）、`StardustBeacon`（信标标记件）。
- 演示数据升级 2 房间 5 碎片（room_02 老院子：村口的路+摇椅日记）。
- smoke19 断言 11 项：房间初始态/XRI 装配/悬停/选中/褪色/排序交换恢复/走读信标两站/回访复位/房间切换（穹顶纹理+光点 3↔2+走读终止）/重演绽放跳过复原/ImportServer HTTP。

### W2 内容层备忘（2026-11-12，第 1 天）
- 素材入工程：`Assets/Art/Panoramas`（2 全景）、`Assets/StreamingAssets/Media/frag_01|03`（演示照片）、`Assets/StreamingAssets/Panoramas/room_01.jpg`（穹顶用）、`Assets/Audio`（3 占位 WAV）。
- 新增：MediaResolver（持久化→StreamingAssets 回退）、AmbienceController（2D 环境音+settings.ambientVolume）、PanoramaDome。
- MemoryPanel：Show 定位于视线前方 1.5m 固定不随头（§5.3），素材缺失警告不炸。
- XRIAnchorBridge：selectEntered → anchor.Select() + panel.Show(fragment)。
- **ImportServer HTTP 冒烟（W2 第 1 天硬节点）批处理全绿**：GET 首页 / POST 上传落盘 / POST 日记写回 appdata（探针 TestImportServer）。

## 4. 工作规矩（每会话自查）

1. 开工先读本文档 + `docs/TECH_DESIGN_V2.md` 变更记录；结束时更新本文档"当前状态"。
2. 素材：下载→魔数校验→`assets_raw/` 对应子目录→登记 `ASSET_LIST.md`（来源/授权）。
3. 代码：显式类型（禁隐式推断）；JSON 用 Newtonsoft；命名空间 `MemoryCorridor.*`。
4. Git：小步提交，提交信息中文说清"做了什么+为什么"；`Library/`、`Temp/`、`Logs/`、`obj/`、构建产物不进库（.gitignore 已配）。
5. 每周六录 devlog 到 `docs/devlogs/`；M3（12-12）后只修不加。
6. D 盘 <20GB 时停下清理；Unity batchmode 同时只跑一个实例。
7. 改动涉及方案层面的，同步更新 `TECH_DESIGN_V2.md` 并在文首"最后更新"改日期。

## 5. 当前状态（每次会话末更新）

- **阶段**：W2 核心机制完成（2026-11-12 第 2 批），smoke19 全绿（11 项断言）
- **已完成**：W2 第 1 天全部 + 房间切换 + 手动排序 + 星尘走读 + 回访复位（褪色闭环）+ 回忆重演四拍（含跳过）——**M1 闭环 alpha 的机制层全部就绪**
- **smoke19 断言**：A 房间初始/B XRI+悬停+选中+褪色/C 排序/D 走读信标/E 回访/F 切房间/G 重演四拍/H 导入服务
- **已跳过（勿卡住）**：① PICO SDK（被墙+GUI）② GitHub 远端（gh 未登录）③ devlog #1（GUI）④ Emulator GUI 验证 ⑤ 旁白/叙事音频（Freesound 手动）⑥ Android StreamingAssets 异步化（真机前）⑦ UGUI 磨砂面板/全屏暗幕/星河回顾墙（W4 视觉）⑧ VR 内抓取编排（W3，当前排序走数据层）
- **下一步**：W3 叙事打磨：星河回顾墙（ConstellationWall）、传送接 XRI Teleportation、房间切换淡入淡出、先闻其声接真实旁白（素材到位后）；M1 闭环 alpha（11-23）前做一轮 Editor GUI 手动走查
- **待办提醒**：D 盘 30GB；样本目录勿删（GUID 引用）
