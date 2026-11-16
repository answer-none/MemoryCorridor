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

### W3 备忘（2026-11-12 第 3 批，smoke23 全绿 14 断言）
- 新增：`ConstellationWall`（星河回顾墙：走读终点弧形 60°/2.5m 排星，单 LineRenderer 连线，星点挂 XRSimpleInteractable，点星→面板+重演）、`StarNode`（星点标记件，稳定命名 Star_{fragmentId}）、`SceneFader`（ScreenSpaceOverlay 黑幕，FadeOut/FadeIn 按真实时间）、`TeleportationArea` 地面。
- RoutePlayer：EndTour(buildWall=true) 走完建墙；OnRoomSwitched → EndTourInternal(false) 切房不建墙（防新房间浮现旧星座）；StartTour 先 Clear。
- RoomManager：SwitchToRoomWithFade（黑幕 0→1→0 包住直切）。
- 断言追加：H 星河墙（3 星+回调）/ I 转场（黑幕+带淡切往返）/ J 传送装配（Provider+Area）。

### 坑-028 FindObjects/FindFirst 返回顺序不保证（第 3 次踩同类）
- **现象**：探针 FindFirst<StarNode> 拿到的是 frag_03 的星（场景顺序≠生成顺序）。
- **规避**：需要确定性对象时用稳定命名 + GameObject.Find（星点/光点均已稳定命名 Star_{id}/LP_{id}）。同类前科：坑-019（GameObject.Find 只找第一个）、坑-021（FindObjects 排除未激活）。

### 坑-029 TMP 3.0.9 / UGUI API 记忆偏差（第 N 次验证坑-013 纪律正确）
- **现象**：`TMP_Text.textWrappingMode`/`TextWrappingModes` 在 TMP 3.0.9 不存在（是 `enableWordWrapping` bool）；`RawImage` 没有 `preserveAspect`（那是 `Image` 的）。
- **规避**：写 API 前 grep 包源码已两次救场（TMP_FontAsset.CreateFontAsset 第 432 行确认存在才敢用）。在线文档/记忆 ≠ 本机版本。

### W4 备忘（2026-11-12 第 4 批，smoke25 全绿 16 断言）
- `MemoryPanel` 重写为 **UGUI 磨砂面板**：WorldSpace Canvas（620×460px，scale 0.0016≈1.0×0.74m）、暮色半透明底+琥珀描边（Outline）、照片 RawImage、TMP 标题/正文（`TMP_FontAsset.CreateFontAsset(builtinFont)` 动态字体，CJK 按需填充）；EnsureBuilt 惰性构建；Show 定位视线前方 1.5m 固定。Show/Hide 契约不变，桥接器/星河墙零改动。
- `StardustField` 星尘粒子：Shuriken 程序化（200 上限/暖琥珀/Box 4×2.5×4m/跟随相机）；粒子材质复用已加载的 URP/Lit（坑-022 同源：URP 粒子专用 shader 不可靠）。
- 星河墙缩略图：图片碎片星点挂 Thumb 子 Quad（URP/Lit+贴图），音/日记星纯光球。
- 探针新增 L（面板照片+标题+显隐）/ M（星尘发射）段，H 段加缩略图断言（图星有/音星无）。

### W4 续备忘（2026-11-12 第 5 批，smoke26 全绿 17 段断言）
- Demo 包定稿：**2 房间 8 碎片**——room_01（缝纫机/老收音机·私密/童年自行车·褪色300天/窗台花瓶）+ room_02（村口的路/摇椅日记·纯diary/屋前自行车·褪色180天/妹妹学车）。
- frag_02 补 radio.jpg（私密碎片也可见可看）；新增 frag_06/07/08 均有图有文案；日记文案润色。
- 探针连锁更新：4 光点/4 星全缩略图；**H2 段**（切房间二建墙：日记星纯光球、图星带卡、褪色样例星）。
- ASSET_LIST.md §1.3 已更新为实装映射（4 张备用照片留在 raw）。

### PICO SDK 集成备忘（2026-11-12 第 6 批，smoke27 全绿）
- **PDC"已安装"= 下载 zip**：`C:\Users\Administrator\AppData\Roaming\PICO_Developer_Center_Downloads\cn\sdk_id_12\PICO%20Unity%20Integration%20SDK-3.3.3-20251202.zip`（105MB，文件名含 %20 转义）。
- 包形态：**UPM 本地包**（顶层 package.json + Runtime/Editor/Platform/Enterprise/SpatialAudio asmdefs），不是 unitypackage——正确导入 = 解包拷入 `Packages/com.unity.xr.picoxr/`（238MB，Unity 自动识别嵌入包），**不能拷进 Assets/**。
- Loader：`Unity.XR.PXR.PXR_Loader`（Runtime/Scripts/PXR_Loader.cs:67，坑-013 纪律 grep 实测）。Wizard 新增第 5 阶段 AssignLoader；与 OpenXRLoader **双 Loader 并存**（真机走 PICO / 模拟器走 OpenXR），`Assets/XR/Loaders/PXR_Loader.asset` 落盘 ✓。
- SDK 依赖 xr.management/XRI/core-utils 均被工程现有版本满足（4.5.3/2.5.4/2.5.2）。
- 集成后 smoke27 全回归 17 段零破坏 ✓。
- **坑-030**：用户开着 Unity GUI 时跑 batchmode → exit 21"另一个实例"；**Unity 编辑器与批处理互斥，跑批前必须关 GUI**（Temp/UnityLockfile 是判据）。
- 下一步（真机链路）：Android Build（IL2CPP/ASTC/PICO 清单配置）→ Emulator 验证 → 真机标定。

### 穹顶编辑期贴图（用户"场景空"问题的修复，第 6 批）
- 根因：光点是 Play 时 RoomManager 数据驱动生成（编辑器视图本来就没有）；穹顶贴图原先是 Play 时才加载 → 编辑视图=白球。
- 修复：builder 现在编辑期就把 `Assets/Art/Panoramas/living_room_warm.jpg` 赋给穹顶材质（资产引用，随场景序列化持久）；Play 时 PanoramaDome.Start 仍按 appdata 流程重载（双保险）。
- 告知用户的 GUI 观察法：打开场景即见客厅穹顶；按 ▶️ 后 8 光点/星尘/信标全出。

### Android 首包备忘（2026-11-12 第 7 批，**首个 APK 打包成功**）
- **产物**：`D:\unityxiangmu\_builds\MemoryCorridor_M2.apk`（42MB，构建 0.8 分钟增量/首次含 Gradle 全程）。
- 配置：IL2CPP / ARM64 / ASTC / Linear / 包名 com.memorycorridor.app / minSdk 29 / Gradle；APK 内已验证 libil2cpp+libunity（ARM64）、libPICO_TOBAPI.so、包名字节串。
- 构建脚本：`Assets/Editor/AndroidBuildScript.cs`（BuildAndSmoke 同模式，成功 Exit(0)）——构建走它，不手工点。
- **坑-031**：`UnityEditor.EditorApplication.Exit` 必须包 `#if UNITY_EDITOR`——Watchdog 那处没包，Player 构建编译报 CS0234（编辑器内一直没暴露）。
- **坑-032**：shader 里 `const float PI = …;` 编辑器 D3D11 能过、**移动端（gles3/vulkan）报 syntax error**——shader 改动必须跑一次 Android 构建级别的编译验证（PanoramaDome 改 `#define PI`）。
- **坑-033**：**OpenXR 包与 PICO SDK 的 `libopenxr_loader.so` 在 Gradle mergeNativeLibs 撞库**——Android 平台双 Loader 不可行，构建前 `RemoveLoader(OpenXRLoader)`，只留 PXR_Loader。Editor Play 不依赖 Android loader（模拟器走 Input System），无影响。
- **坑-034**：`RemoveLoader(null, …)` NRE——先取 `general.Manager` 再传。
- 构建轮次：b1 编译错→b2 shader→b3 撞库→b4 NRE→b5 ✅（5 轮，全部有断言级日志）。
- 下一步：模拟器装 APK 验证（adb install 或 PDC 安装）→ 真机标定 → M2 内测包。

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

- **阶段**：团队化 R3.5 完成（2026-11-13），P0 8/8 全 ✅（FUNCTION_AUDIT.md），**内测可发**
- **R3.5 新增**：ExportService/ImportService（P0 缺口补齐：zip+隐私过滤+id 冲突合并）/ 程序化圆角贴图 RoundedRectTexture + Sprite3D shader / 光点光晕外球 / 4 SFX 合成 / 四份文档（UI_DESIGN/ARCHITECTURE/FUNCTION_AUDIT/NARRATION_SCRIPT）
- **坑-036**：**inactive GameObject 上 StartCoroutine 静默失败**（MemoryPanel.Show 先 StartAudio 后 SetActive → 协程没跑）——先激活再起协程；同类坑避坑法：涉及协程的激活流程把 SetActive 提前
- **坑-042（R8 已实现，真机部分待验）**：视频记忆已落地——VideoPlayer+RT+RawImage 一次通过（无踩坑）；ffmpeg lavfi 生成 994KB 测试视频（H.264/yuv420p/faststart，Android 兼容）。
- **坑-042（预登记，R7 评估未踩）**：视频记忆接入要点——VideoPlayer+RenderTexture 面板方案可行；限制单条 ≤10s/720p/1Mbps（APK 增重可控）；prepareCompleted 异步协程；inactive 面板自动停播；真机解码差异排真机验证。
- **坑-043（R7 走查发现，R8 修复实证）**：**Quad 的 MeshCollider（凹面）设 isTrigger → PhysX abort → Unity 整体闪退**（Windows 事件日志 0x40000015 / KERNELBASE.dll 实证，用户 GUI Play 触发）。修复：trigger 一律用凸面 BoxCollider（SettingsPanel 开关行已换）。**通用铁律：世界空间 UI 牌（Quad+Sprite3D）挂 XRSimpleInteractable 时，一律 Destroy MeshCollider + AddComponent<BoxCollider>**。
- **坑-041**：**batchmode Play 下 ScreenCapture 不产出文件**（截图请求被接受但写入依赖 GUI 设备呈现）——"实测画面证据"在批处理环境不可行，改用：①断言级证据（25 段）②用户 GUI 目检截图 ③真机录屏。CaptureProbe 代码保留（GUI Play 可用）
- **坑-037**：**Play 模式中弹 TMP Importer 模态窗 = UI 卡死**（导入触发资源重载与 Play 冲突）——已根治：builder 静默导入 TMP Essentials（AssetDatabase.ImportPackage(path, false)），GUI 永不再弹。用户卡死时处置：任务管理器杀 Unity.exe → 删 Temp/UnityLockfile → 重跑流水线。
- **坑-038**：无贴图粒子渲染成方块——粒子材质换径向光晕贴图（RoundedRectTexture.RadialGlow）+ 透明 shader。
- **坑-039**：URP 下 Bloom 不生效两要素——①Global Volume+Bloom profile（程序创建）②**相机 UniversalAdditionalCameraData.renderPostProcessing=true**（XR Rig 自带相机默认关）。缺一就是灰球。
- **坑-040**：GUI 卡死场景处理 SOP：taskkill //IM Unity.exe //F → 删 Temp/UnityLockfile → 重跑流水线（场景是产物无损失）。
- **R3 新增**：APPLICATION.md（申报骨架）/ DEVLOG_SCRIPT.md（7 镜头分镜）/ README 重写 / PERF_AUDIT.md 基线（62.5fps/403MB/粒子 200）
- **GitHub 建库状态（已闭环）**：①gh 原账号 Hc-ve 已登出，换新账号 answer-none ✓（用户浏览器生成 classic token 勾 repo 权限，SSH key 已加）②诊断：github.com/api.github.com 被 **SNI 精准阻断**（TLS 握手掐、TCP 通），ssh.github.com:443 无 SNI 不受影响 ③解决：用户浏览器建库 + **git push 走 ssh://git@ssh.github.com:443**（13 提交 + LFS 23MB 全部上云，远端 main=652ed02 ✓）④仓库：github.com/answer-none/MemoryCorridor（public）
- **坑-035（重要）**：GFW 对 GitHub 是 **SNI 精准阻断**——今后所有 git 交互一律走 `ssh://git@ssh.github.com:443/`（remote 已配）；HTTPS API 类操作（gh/REST）被阻断时改走浏览器或等窗口期。
- **token 安全提醒（已告知用户）**：classic token 曾在对话明文出现，建议到 github.com/settings/tokens **Revoke**（建库实际走的是浏览器，token 未派上用场）。
- **待办提醒**：M3 12-12；提交 12-19 09:00；路演 12-26
