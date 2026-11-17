# 对标学习（BENCHMARK v1）

> R15 对标学习轮 · 联网检索同类项目与学术研究，提炼可落地优点
> 检索范围：VR 记忆宫殿 / memory palace VR / VR 空间记忆训练 / 怀旧疗法 / 冥想类 VR / 城市漫步类 VR
> 结论：**5 条可落地优点**，其中 **①② 本轮（R15）已落地**（代码+探针断言+截图证据），③④⑤ 已有对应实现或记入下轮队列。

---

## 检索清单（8 个产品 + 4 篇论文/综述）

### 商业产品
| # | 项目 | 平台 | 链接 | 与本项目关系 |
|---|---|---|---|---|
| 1 | **LOCI — Memory Palace Trainer** | Meta Quest | https://www.meta.com/experiences/loci-memory-palace-trainer/ | 最成熟的 MoL 训练应用：把记忆冠军的位置记忆法做成可交互 VR 训练（牌/数字/演讲） |
| 2 | **Loci Memory Palace** | Windows MR（Microsoft Store） | https://apps.microsoft.com/detail/9nfmz63mgjrf | MoL + 3D 思维导图结合的 MR 应用 |
| 3 | **Wander** | Quest/Steam | https://www.meta.com/blog/virtual-vacation-11-vr-apps-and-films-that-let-you-travel-the-world-from-home/ | Google 街景城市漫步 |
| 4 | **Revisit** | Quest | https://www.facebook.com/groups/Quest2Community/posts/1700504777762578 | 街景漫步 + **时间线效果 + 声音**强化"记忆感"——与本项目"记忆重演"最接近 |
| 5 | **Guided Meditation VR** | Steam | https://store.steampowered.com/app/397750/Guided_Meditation_VR | 低密度环境 + 视觉引导冥想（认知负荷控制） |
| 6 | **Mindergy VR** | **PICO 商店** | https://mindergyvr.com/mindergy-vr-meditation-app | PICO 平台冥想/呼吸/专注训练 |
| 7 | **LociVR 原型** | 学术原型 | https://dl.acm.org/doi/full/10.1145/3747327.3764879 | ACM 2025 VR 记忆宫殿设计研究 |
| 8 | **Road to VR 冥想合集** | 多平台 | https://roadtovr.com/vr-meditation-relaxation-mindfulness-games-quest-rift-steamvr-psvr | 22 款放松类 VR 盘点（环境美学基准） |

### 学术论文/综述
| # | 论文 | 出处 | 链接 | 核心结论 |
|---|---|---|---|---|
| A | **Reggente et al. 2020**《The Method of Loci in Virtual Reality: Explicit Binding of Objects to Places》 | Univ. of Manchester（被引 ~82） | https://research.manchester.ac.uk/en/publications/the-method-of-loci-in-virtual-reality-explicit-binding-of-objects | VR MoL 有效性的关键是**把被记对象显式绑定到位置**（location+image 双编码） |
| B | **Moll et al. 2023**《Optimized virtual reality-based Method of Loci memorization techniques》 | Springer *Virtual Reality* | https://link.springer.com/article/10.1007/s10055-022-00700-z | VR 环境用**极简训练**即可教会 MoL（无需长讲解） |
| C | **Hagström et al. 2024**《Long-Term Recall With an Application Using Virtual Reality》 | *Scandinavian Journal of Psychology* | https://onlinelibrary.wiley.com/doi/full/10.1111/sjop.70089 | VR MoL 组**回忆准确率更高、遗忘曲线更浅**；多线索编码（空间+言语）是关键 |
| D | **VR 怀旧疗法系统综述 2024**（8 项研究） | PMC11599890 | https://pmc.ncbi.nlm.nih.gov/articles/PMC11599890 | 7/8 研究：VR 怀旧疗法改善记忆与情绪；多模态（声音+图像+空间）刺激是效果核心 |

---

## 五条可落地优点

### ① 显式对象-位置绑定：记忆点要有"图像"要素（**R15 已落地**）

- **来源**：Reggente et al. 2020（https://research.manchester.ac.uk/en/publications/the-method-of-loci-in-virtual-reality-explicit-binding-of-objects）
- **它怎么做的**：MoL 在 VR 中生效的机制不是"有个位置"就够，而是把**要记的内容物**（对象）显式放置并与位置绑定——回忆时位置线索会带出内容形象。
- **我们能加什么**：我们的光点强在 location（真实物品位）+ path（R12 信标呼吸导引），image 偏弱。给每个光点加一个**类型符号浮标**（billboard 小牌）：
  - 图片类=山形照片框 / 录音类=声波弧 / 日记类=书页 / 混合类=菱星 / 私密=锁形
  - 色形**双编码**防混淆：符号形状五选一互不相似 + 沿用 R1 四色（琥珀/青/米白/暖橙/暖红）——同一房间相邻两个点即使颜色接近（青=录音 vs 悬停青），形状也完全不同；私密点暖红+锁形双保险
  - 符号随光点显隐设置（R5）联动，0.10m 小牌不挡视线
- **成本/风险**：1 个组件 + 程序化贴图（零外部素材），约 150 行——**成本低**。风险：符号太小难辨识（已用 0.10m≈悬停预览卡尺寸）；billboard 每帧朝向相机（缓存 Camera.main，无 FindObjects 开销）。
- **落地证据**：`Assets/Scripts/Interaction/LightPointSymbol.cs` + smoke Y5 段断言（kind 映射/billboard 朝向/显隐联动）。

### ② 多线索回忆卡片：截图→带路径缩略+时间+关键词的卡片版式（**R15 已落地；R16-1 增补音频角标**）

- **来源**：Hagström et al. 2024（https://onlinelibrary.wiley.com/doi/full/10.1111/sjop.70089）+ Revisit（时间线+声音线索，https://www.facebook.com/groups/Quest2Community/posts/1700504777762578）
- **它怎么做的**：Hagström：多线索编码（visuo-spatial+verbal）使 VR MoL 长期回忆准确率更高；Revisit：给漫步叠加时间线视觉与声音，强化"这段记忆"的包裹感。
- **我们能加什么**：走读结束的回忆产物从"纯 Game 视图截图"升级为**本地合成的卡片版式**（720×960，不联网）：
  - 顶部：房间标题；中部：走读完成瞬间的真实截图缩略（专用相机渲染，绕开坑-041 batchmode ScreenCapture 不落盘）
  - 中下：**路径缩略**（本次走读每一站一个小圆点串成路径，最后一站高亮琥珀）
  - 底部：关键词（本次走读碎片标题串）+ 时间（yyyy-MM-dd HH:mm）+ 站数（"全程 N 站"）
- **成本/风险**：卡片合成器约 220 行（复用 RoundedRectTexture/UiTextFactory），**成本低**。风险：专用相机渲染需临时禁用其他相机（已做保存/恢复）；无截图相机时降级为 ThumbBg 占位（不阻断）。
- **落地证据**：`Assets/Scripts/Media/MemoryCardComposer.cs` + RoutePlayer 接线 + smoke Z3 段断言（PNG 落盘字节数>0 + 元数据含标题/关键词/时间/站数）。

### ③ 极简引导即可教会 MoL（**R16-3 部分落地**：新主题引导走 TutorialGuide 同链路；动态提示语记下轮）

- **来源**：Moll et al. 2023（https://link.springer.com/article/10.1007/s10055-022-00700-z）
- **它怎么做的**：VR 中 MoL 可用极简训练教会，无需长时间方法讲解。
- **我们已有**：TutorialGuide 5 步引导（含设置入口提示，smoke P2 段断言）+ WelcomeBoard 首访告示——与"极简引导"结论一致。**本轮不动**。
- **未来可加**：走读首站引导文案追加一句"记住这个位置与它上面的符号"（呼应 ①）。成本一行文案，风险无。

### ④ 怀旧疗法的多感官线索（**R16-1 已落地**：卡片音频角标——图标+时长，有音频才显示；R8 视频链路+走近听声已有）

- **来源**：VR-RT 综述 2024（https://pmc.ncbi.nlm.nih.gov/articles/PMC11599890）
- **它怎么做的**：多模态刺激（声音+图像+空间）是 VR 怀旧疗法改善记忆与情绪的核心机制。
- **我们已有**：光点走近听声（audio 类 + SpatialAudioAnchor）+ 全景穹顶（PanoramaDome）+ 视频记忆（R8）+ 环境音（AmbienceController）。**本轮不动**。
- **未来可加**：卡片上标注"含音频/含视频"角标（符号复用 ① 的贴图）。成本极低，风险无。

### ⑤ 认知负荷控制（**R16-2 已落地**：单房间光点 >12 告警 + 过密簇视觉降密度（压暗+缩符号不打散布点）；单焦点/距离/暗角已有）

- **来源**：认知负荷自适应 VR 记忆宫殿（arXiv 2506.02700，https://arxiv.org/abs/2506.02700）+ Guided Meditation VR / Mindergy VR 的低密度环境实践
- **它怎么做的**：按个体认知负荷调节环境复杂度；冥想类应用用单焦点+低密度环境控制注意负担。
- **我们已有**：单焦点设计（R12 一次只高亮一个信标/一块面板可交互）+ 面板距离/舒适暗角/传送优先三项设置（R15 前已落，smoke V/W 段断言）。**本轮不动**。
- **未来可加**：单房间光点密度上限（>7 个时提示分房）。成本低，记下轮队列。

---

## 本轮落地映射表（R15 验收口径）

| 优点 | 落地组件 | smoke 段 | 截图证据 |
|---|---|---|---|
| ① image 要素 | LightPointSymbol.cs + LightPointAnchor.SetSymbol + builder 挂载 | Y5 | 卡片合成截图含符号光点 + 待 GUI 目检 |
| ② 卡片升级 | MemoryCardComposer.cs + RoutePlayer.CaptureMemoryShot | Z3 | persistentDataPath/screenshots/memory_card_*.png（PNG 字节数探针断言） |

**验收链**：BUILD SUCCESSFUL + smoke 全段（29→31 段）全绿 + APK 重打 + BENCHMARK 入库——文档+代码+探针断言三者齐备，实拍截图落盘 persistentDataPath（GUI 目检记用户侧）。
