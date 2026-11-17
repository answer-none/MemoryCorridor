# UI 风格指南（UI_STYLE_GUIDE v1）

> R12 令牌化收敛产物 · UiTokens.cs 为唯一权威来源 · 改色/改字号只改 UiTokens.cs
> 与 UI_DESIGN.md（设计规范文档）配套：本文件是**代码实现口径**，UI_DESIGN 是**设计意图口径**。

---

## 1. 色彩令牌表（UiTokens.cs）

| 令牌 | 值 | 用途 | 白名单例外 |
|---|---|---|---|
| `Amber` | (0.91, 0.64, 0.29) | 主强调：标题/描边/光点(图)/门柱 | — |
| `Cyan` | (0.35, 0.78, 0.88) | 录音类/悬停态 | — |
| `Cream` | (0.92, 0.88, 0.82) | 日记类/正文墨色/标签墨 | — |
| `WarmOrange` | (0.95, 0.55, 0.35) | 混合类 | — |
| `WarmRed` | (0.78, 0.42, 0.42) | 私密 | — |
| `PanelBg` | (0.12, 0.10, 0.15, 88%) | 面板底 | — |
| `CardBg` | (0.10, 0.08, 0.13, 94%) | 卡片/告示牌底 | — |
| `HudBg` | (0.10, 0.08, 0.13, 86%) | HUD 引导条底 | — |
| `RowBg` | (0.18, 0.15, 0.22, 90%) | 开关行底 | — |
| `LineSoft` | Amber @ 55% | 柔描边 | — |
| `OnColor` / `OffColor` | 绿/暖红 | 开关状态 | — |
| `DustAmber(Dim)` | (0.91, 0.72, 0.45) | 星尘粒子 | ✅ 粒子专属色 |
| `FrameBg` | (0.16, 0.13, 0.19) | 照片框底 | — |
| `ThumbBg` | (0.2, 0.18, 0.22) | 缩略占位 | — |
| `CloseBg` / `CrossWhite` | 暖红/白 | ✕ 钮 | — |
| `LabelBg` / `LabelInk` | 深/米白 | 星点标签 | — |

## 2. 尺寸令牌表

| 令牌 | 值 | 用途 |
|---|---|---|
| `CornerRadius` | 24px | 面板圆角 |
| `CardRadius` | 16px | 卡片圆角 |
| `BadgeRadius` | 12px | 徽章圆角 |
| `BorderPx` | 2px | 描边宽 |
| `RowHeightPx` | 64px | 开关行高 |
| `PanelTitleSize` / `PanelBodySize` | 42/28 | 面板字号 |
| `SymbolSize` | 0.10m | 记忆点符号浮标边长（R15 ①） |
| `SymbolOffset` | 0.48m | 符号距光点中心上移（光晕壳外，R15 ①） |
| `CardBoardW / CardBoardH` | 0.72/0.96m | 回忆卡板尺寸（R16-4 收敛） |
| `CardShotW / CardShotH` | 0.60/0.36m | 卡片截图缩略框（R16-4 收敛） |
| `BadgeIconSize` | 0.028m | 卡片音频角标图标（R16-1） |

**R16-4 色彩新增**：`CaptionInk`（卡片时间）/`DimInk`（站数）/`RainBlue`/`RainInk`（雨夜校园主题）——四值已入 UiTokens，MemoryCardComposer/ThemeColors 全部引用令牌。
**违规扫描口径（R16 终值）**：全工程 new Color 字面量（除 UiTokens.cs）**14→8 处**（LightPointAnchor [SerializeField]×6 + SmokeProbe 断言期望值×2，全部原白名单）——R16 新增组件零字面量，主题环境光/雾/证据底色 8 值全部令牌化（AmberAmbient/AmberFog/RainAmbient/RainFog/EvidenceBg）。只降不涨 ✅

## 3. 字号 dmm 换算口径

**dmm = 字符物理高度(mm) ÷ 观视距离(m)**

| 场景 | 字符高 | 距离 | dmm | 规范判定 |
|---|---|---|---|---|
| 面板标题 42px | ≈67mm | 2.4m | 28 | ≥20 ✅ |
| 面板正文 28px | ≈45mm | 2.4m | 19 | ≈20 标准 ✅ |
| 引导条 46×0.011 | ≈50mm | 1.2m | 42 | ✅ |
| 星点标签 | ≈95mm | 2.5m | 38 | ≥16 ✅ |
| 设置面板开关 | ≈42mm | 2.38m | 18 | ≈20 ≈标准 ✅ |

**换算公式**：TextMesh 字符高 ≈ `fontSize / 10 × characterSize`（单位：Unity unit=1m）

## 4. 面板一致性标注

| 面板 | 色值 | 圆角 | 字号 | 层级 | 状态 |
|---|---|---|---|---|---|
| MemoryPanel | ✅ tokens | ✅ 24px | ✅ 42/28 | ✅ 照片框→标题→正文→✕ | **已统一** |
| SettingsPanel | ✅ tokens | ✅ 18px | ✅ 46/40×0.0105 | ✅ 标题→5 行开关 | **已统一** |
| TutorialGuide | ✅ tokens | ✅ 16px | ✅ 46×0.011 | ✅ 标题→三行引导 | **已统一** |
| HoverPreviewCard | ✅ tokens | ✅ 16px | ✅ 44/32 | ✅ 底板→缩略→标题/日期 | **已统一** |
| ConstellationWall | ✅ tokens | ✅ 14px | ✅ 40×0.07 | ✅ 底板→星色(hue)→标签 | **已统一** |
| WelcomeBoard | ✅ tokens | ✅ 20px | ✅ 52/34 | ✅ 标题→三行引导 | **已统一** |
| PulseEmissive | ✅ tokens | — | — | ✅ Amber 单色 | **已统一** |
| SceneFader | ⬛ 纯黑(0,0,0) | — | — | 转场专用 | **例外**（语义正确） |

**例外白名单（硬编码残留 14 处，全部有理由）**：
- LightPointAnchor 6 处 [SerializeField]：Inspector 设计参数（可调）——**例外**
- PulseEmissive 1 处 [SerializeField]：同上——**例外**
- SmokeProbe 2 处：断言期望值（独立验证不可引用被测对象）——**例外**
- SceneFader 1 处：纯黑转场（语义正确）——**例外**
- StardustField 2 处：粒子专属色调——**例外**
- 各面板 charSize：独立排版参数（非共享令牌）——**例外**

## 5. 违规扫描命令

```bash
python -c "扫描 Assets/Scripts 里的 new Color(\d / characterSize = 0.\d（排除 UiTokens.cs）"
# R12 基线：32 处 → R12 收敛后：14 处（全白名单）→ 净违规 0
```

## 6. 新组件接入清单

新建 UI 组件时必须：
1. 色值引用 `UiTokens.*`（禁止 `new Color(0.x...)` 字面量）
2. TextMesh 走 `UiTextFactory.CreateTextMesh`
3. 圆角底板走 `RoundedRectTexture.RoundedRect(w, h, UiTokens.CardRadius, ...)`
4. 字号对照 §3 dmm 表（正文 ≥20 dmm）
5. 白名单新增须在本文件登记理由
