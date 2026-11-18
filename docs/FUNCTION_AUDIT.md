# 功能完整性审计（FUNCTION_AUDIT v1）

> 验收工程师 · 2026-11-13 · 对照比赛 P0/P1 清单逐项核对
> 图例：✅ 已实现+有断言 | 🟡 部分实现 | ❌ 未实现（记 R4/R5）

## P0（MVP 必做，8 项）

| # | 功能 | 状态 | 断言/证据 | 缺口 |
|---|---|---|---|---|
| 1 | 光点锚定+射线交互（悬停预览/类型配色/音效） | ✅ | B 段（预览卡/配色/音效/选中） | 预览卡淡入动画（R4 打磨） |
| 2 | 图片/录音/日记三类记忆 | ✅ | L 段（面板照片/标题/录音播放） | 录音 spatialBlend 随面板走（观感待目检） |
| 3 | 全景房间×2+入口切换 | ✅ | F/I 段（穹顶纹理切换+带淡切往返） | 入口物件可视化（目前传送上房，R4 加门框光效） |
| 4 | 手动排序+走读路线+星河回顾 | ✅ | C/D/E/H/H2 段 | VR 内抓取排序（P1，R5） |
| 5 | 浏览器局域网上传 | ✅ | K 段（GET/POST 上传落盘/日记入档） | Emulator 级真实验证（待装机） |
| 6 | **公开/隐私 + zip 导出导入** | 🟡→本轮补齐 | 隐私过滤规则已定义 | **Export/ImportService 本轮实现+Z 段断言** |
| 7 | 记忆褪色 | ✅ | B 段（fresh vs faded）+E 段（回访复位） | — |
| 8 | 欢迎房间/引导 | ✅ | P 段（告示牌显示→收起） | "欢迎房间"概念由 Demo 8 碎片承担 ✓ |

## P1（尽量做）

| # | 功能 | 状态 | 计划 |
|---|---|---|---|
| 9 | 视频记忆 | ✅ R8（代码级，真机解码待验） | VideoPlayer+RenderTexture→RawImage；ffmpeg lavfi 渐变测试视频 994KB/8s/720×360/1Mbps（坑-042 全约束达标）；L 段断言 isPlaying ✓；真机解码差异排真机验证 |
| 10 | 家庭本地多档案合并 | 🟡（成员字段+导入合并规则已定义） | 随导入服务本轮落逻辑，UI 后补 |
| 11 | 多维排序（时间/标签/相关度） |
> R13 注：P1「星点标签」项已解决（R12 charSize 0.07→38 dmm，D2 段断言），从遗留清单移除。 🟡 数据层 ✅ R5（MemorySortService 三维纯函数+Y 段断言，order 不覆写）；UI 陈列视图未接（消费 Sorted() 即可，R7+） |
| 12 | 光点显隐开关+入口可达性 | ✅ R5/R6 | SettingsPanel 设置面板（光点标记/星点标签两路开关，翻转持久化+场景应用）；X 段断言（翻转/持久化/renderer 状态） |
| 13 | 手势辅助 | ❌ | P2（真机后） |

## P2（二期，仅文档承诺）
云端同步 / 链接分享 / 实时共创 / 3DGS —— 不实现，申报文档"二期规划"承载。

## R15 对标学习轮新增（2026-11-18，BENCHMARK.md ①②落地）

| # | 功能 | 状态 | 断言 | 说明 |
|---|---|---|---|---|
| R15-a | 记忆点符号浮标（MoL image 要素） | ✅ | Y5 段 | LightPointSymbol：五类型程序化贴图 billboard（image 山框/audio 声弧/diary 书页/mixed 菱星/private 锁形），色形双编码防混淆；类型映射+billboard 朝向+显隐联动断言；证据 docs/evidence/r15_symbol_frag_*.png ×4 |
| R15-b | 回忆卡片版式合成 | ✅ | Z3 段 | MemoryCardComposer 720×960：房间标题+走读完成真实截图缩略+路径点串（末站高亮）+关键词+时间+站数；专用相机渲染绕坑-041，PNG 落盘 persistentDataPath/screenshots/；证据 docs/evidence/r15_memory_card.png |

## R16 功能完善轮新增（2026-11-18，BENCHMARK ③④⑤ 落地）

| # | 功能 | 状态 | 断言 | 说明 |
|---|---|---|---|---|
| R16-a | 卡片音频角标 | ✅ | Z4 段 | 有音频（mediaItems 含可解析 audio）→ 右上角"声波图标+时长"（WAV 头精确解析/MP3 估算）；无音频完全不渲染；证据 r16_card_badge_compare.png 并排对比 |
| R16-b | 单房间密度上限 | ✅ | D3 段 + builder 数据级检查 | 20 点→2 簇、告警=true、9 成员/簇压暗×0.55+符号×0.7（不打散布点）；证据 r16_density_before/after.png |
| R16-c | 第二主题路线"雨夜校园" | ✅ | R2 段 | room_03×8 碎片（image/audio/diary/mixed 各 2）全有标题+正文+符号；theme=rain 环境光/雾色真落地（ThemeColors）；程序化雨夜全景 17KB+ffmpeg 雨声 517KB；证据 r16_theme_rain_room.png+卡片×2 |
| R16-d | UI/空间合规收尾 | ✅ | D4 段 | R15/R16 元素全令牌化（SymbolSize/CardBoard/BadgeIcon 等 9 新令牌，零新增字面量）；卡片+符号 2/3/5m 可读性实拍×3+dmm 表入 SPATIAL_AUDIT §4 |

## R17 功能完善轮新增（2026-11-19）

| # | 功能 | 状态 | 断言 | 说明 |
|---|---|---|---|---|
| R17-a | 运行时主题选择 | ✅ | W1 段 | SettingsPanel"场景主题"行（default→rain 循环，0.3s 淡入即时生效+持久化）；切 2 次读回 activeThemeId + RenderSettings 环境光/雾色/雾密度实测跟随；对照卡板量化差异 avg=22.4/255≥20 & ratio=46%≥30%；雾改 Exponential（0.02/0.14） |
| R17-b | 音频讲解播放控制 | ✅ | A2 段 | AutoNarrationController：进房自动播/视线离开或走远>2m 暂停/回来续播（offset=1.2s>0 不重头）/静音点静默；HUD（标题+进度条+当前/总时长） |
| R17-c | 性能预算实测 | ✅ | P3 段 | 3 机位×10 帧：最差 0.4ms/37 DC（Renderer 上界）/8k tri，全达标；PERF_BUDGET.md 落盘（含 R15/R16 元素开销占比） |
| R17-d | 令牌不回退 | ✅ | 静态扫描 | R17 新控件零 new Color 字面量，白名单维持 8 处 |

## R18 补测轮新增（2026-11-19）

| # | 功能 | 状态 | 断言/证据 |
|---|---|---|---|
| R18-a | 真渲染性能补测 | ✅ | PERF_BUDGET §2（GUI Play 2.4-2.6ms/1%Low 3.3ms/DC 上界 36/7.4k tri，9994cf2） |
| R18-b | UI 令牌化清零 | ✅ | 四模式扫描 26→5（豁免 5 处有理由，净违规 0）；新增 10 个字号/尺寸令牌；smoke t15 36/36 回归 |
| R18-c | 正文可读性 2m/3m 实测 | ✅ | SPATIAL_AUDIT §3：@2m 19.8 dmm ✅ / @3m 13.2 dmm 🚩 标红+下一步（远景层级设计/字号升档候选）；实拍 r18_readability_body_2m/3m.png |

## 非功能项

| 项 | 状态 | 证据 |
|---|---|---|
| 23+ 段自动化回归 | ✅ | smoke31→93 全绿（每轮新增段，当前 31 段） |
| Android 构建回归 | ✅ | 42.9MB→47.7MB APK（逐轮回归） |
| 性能基线 | ✅ | PERF_AUDIT.md（62.5fps/403MB/粒子 200） |
| 音效体系 | ✅ | SfxLibrary 程序合成（chime/hover/铃/缝纫/翻页）+ASSET_LIST 登记 |
| 旁白文案 | ✅ | NARRATION_SCRIPT.md（8 条碎片段落式旁白，录音即用） |
| 架构文档 | ✅ | ARCHITECTURE.md（mermaid 分层/数据流/管线） |
| GUI 观感目检 | ⏳ | 用户侧（新 UI：圆角面板/徽章/光晕，批处理无法验美学） |

## 结论
P0 8/8 全 ✅（导出导入 R3.5 补齐）；P1 多维排序、显隐开关已落（R5）；**内测可发**。
