# 竞品调研与借鉴报告

> 调研日期：2026-11-11/12 · 调研目的：开工前吸收同类产品优点，明确差异化定位
> 结论先行：**同类产品验证了我们全部核心设计的合理性；而"记忆褪色、隐私导出、真实物品锚定叙事"三项无人做过，是差异化主阵地。**

---

## 1. 竞品概览

### 1.1 Rendever（美国，养老机构 VR 怀旧疗法平台）

- **做什么**：面向养老社区/居家养老的 VR 怀旧疗法（reminiscence therapy，学术可追溯到 1963）。家属/护工上传照片与老视频，构建"人生故事旅程"（reminiscence journey），带老人逐地重访童年故居、婚礼地、旅行地。
- **验证了**：我们"路线（Route）+ 走读"的叙事结构正是它最有效的设计；"家属上传素材"的工作流（我们用浏览器局域网上传）是刚需。
- **值得借鉴**：
  1. **"VR 是对话的门，而非独享的媒体"**——它的核心理念：体验是为了唤起交谈。→ 我们的回忆重演后应留出"想说话"的间隙（重演结束不立即弹下一站指引，星尘路径延迟 5s 点亮）。
  2. **按人生阶段组织内容**（childhood/marriage/travel）→ 我们 P1 的多维排序应支持"按人生阶段分组"而不只是时间。
  3. **AI 陪伴对话**（2025 年推出 Nova）→ 写入申报文档二期规划，体现产品视野。
- **信源**：[rendever.com](https://www.rendever.com) · [NYT 报道](https://www.nytimes.com/2022/05/06/well/mind/virtual-reality-therapy-seniors.html) · [2025 系统综述](https://pmc.ncbi.nlm.nih.gov/articles/PMC11599890)

### 1.2 Alcove（AARP 创新实验室 → Rendever 运营，家庭 VR 空间）

- **做什么**：第一个"家庭向"VR 应用：虚拟家里传照片/视频、建相册与合集，**"自选它们出现在哪里、以什么方式出现"**，异地家人同处一室对抗孤独。
- **验证了**：家庭共享是真实需求；"摆放在哪"的自由度是核心乐趣（= 我们的光点锚定摆放）。
- **值得借鉴**：相册/合集（Collection）组织层 → P1 多维排序时给碎片加"合集"概念（一条路线之外的自由陈列墙）。
- **信源**：[AARP 发布稿](https://www.aarp.org/press/releases/2020-8-20-innovation-labs-introduces-alcove-virtual-reality-app-on-oculus-quest.html) · [Rendever–Alcove](https://www.rendever.com/alcove)

### 1.3 Meta Quest 原生（v65 空间照片 / Photo Gallery: Spatial Anchor Photo）

- **做什么**：Quest v65 起原生支持在透视环境展示空间照片/视频与全景图；第三方《Photo Gallery: Spatial Anchor Photo》可把照片挂上真实墙面（裸手排列）。
- **验证了**：**"照片挂在真实空间"是一体机厂商押注的方向**，我们的 MR 光点锚定路线正确。
- **值得借鉴**：裸手抓取排列照片的自然感 → P1 手势辅助时优先做"裸手拖动光点"这一个手势（其余保持手柄）。
- **它没有的**：叙事路线、旁白、重演时序、褪色、隐私导出——它只是"挂照片的相框"。
- **信源**：[Quest v65 更新](https://www.meta.com/blog/v65-software-update-spatial-video-panorama-stills-improved-passthrough/) · [Photo Gallery: Spatial Anchor Photo](https://www.meta.com/experiences/photo-gallery-spatial-anchor-photo/933380572862455/)

### 1.4 immerGallery（Quest 平台最佳 VR 相册，口碑标杆）

- **做什么**：2D/3D/180°/360°/空间照片的全格式查看器，圆柱形 3D 展示墙，每张照片可配音旁白与音效，自带 50 张精选带声效的示例集。
- **值得借鉴**：
  1. **每张照片 = 画面 + 专属声音**的呈现美学 → 我们 Demo 素材包按此标准做（每条碎片配环境音/音效，不只是图片）；
  2. **精选示例集的"开箱即惊艳"** → 首次进入的欢迎房间必须自带 3 条精修记忆（用户零上传也能体验完整流程）；
  3. 自动识别图片格式与比例 → P1 细节：上传的图片自动按横竖比例调整卡片布局。
- **信源**：[官网](https://www.immervr.com) · [MIXED 评测](https://mixed-news.com/en/meta-quest-2-immergallery-vr-photo-gallery-app-review) · [Top 20 特性](https://www.reddit.com/r/QuestPro/comments/10y5pff/the_top_20_features_of_immergallery_vr_image)

### 1.5 学术/学位项目（申报文档引用弹药）

- VR 怀旧疗法系统综述（2024，被引 68 次）：[PMC11599890](https://pmc.ncbi.nlm.nih.gov/articles/PMC11599890) —— 支撑"真实需求"论证；
- 2025 RCT 研究：VR 怀旧疗法降低老年冷漠、提升情绪与认知参与：[PMC12109972](https://pmc.ncbi.nlm.nih.gov/articles/PMC12109972)；
- Texas A&M 论文（2025）：用 AI 把老照片增强为沉浸式 VR 环境 → 我们的**分层伪 3D 是不依赖神经重建的轻量替代**，在申报文档中引用并对比（显存友好、无需云端、可离线）。

---

## 2. 差异化定位（评委问"你和 XX 有什么区别"时的答案）

| 能力 | Rendever | Alcove | Quest 原生/Photo Gallery | immerGallery | **回响长廊** |
|---|---|---|---|---|---|
| 照片挂真实空间（MR 锚定） | ✗（虚拟场景） | ✗ | ✓ | ✗ | ✓ |
| 叙事路线 + 旁白串珠 | ✓ 旅程 | ✗ | ✗ | ✗ | ✓ 走读 |
| 回忆重演（分层伪 3D + 空间音频时序） | ✗ | ✗ | ✗ | ✗ | ✓ |
| **记忆褪色（遗忘机制）** | ✗ | ✗ | ✗ | ✗ | **✓ 独有** |
| **隐私分级 + 家庭包导出** | 机构管理 | 账号体系 | ✗ | ✗ | **✓ 本地离线** |
| 中文家庭场景审美 | ✗ | ✗ | ✗ | ✗ | ✓ |

一句话定位：**它们帮你"看回过去"，回响长廊帮你"把过去挂回它发生的地方，并对抗遗忘"。**

---

## 3. 吸收进 V2 方案的变更清单（已合并至 TECH_DESIGN_V2 §1.5）

| 借鉴点 | 来源 | 落点 |
|---|---|---|
| 重演后留白 5s 再指引（对话之门） | Rendever | §3.2 回忆重演第④拍 |
| 碎片配专属音效，Demo 包按"画面+声音"标准 | immerGallery | §11 交付清单 Demo 素材包 |
| 欢迎房间自带 3 条精修记忆（零上传可体验） | immerGallery | §2.1 P0 新增第 8 项 |
| 裸手拖动光点为 P1 唯一手势 | Quest 原生 | §5.1 交互原则 |
| 合集（Collection）陈列墙 | Alcove | P1 多维排序 |
| AI 陪伴对话、人生阶段分组 | Rendever | 申报文档二期规划 |
| 学术引用（怀旧疗法综述/RCT） | PMC | 申报文档"真实需求"论证 |
