# 回响长廊 · Memory Corridor

> PICO Space Program 2026 · 最佳沉浸内容创作赛道
> 把家的记忆挂回真实的房间——基于 Unity + OpenXR 的 MR 记忆策展体验

## 一句话

在真实物品上挂一粒萤火般的光点，走近它先听见当年的声音，光点绽放成那一刻的回忆；太久不去看，记忆会慢慢褪色，直到你再次点亮它。走完生成一面星河回顾墙；整个房间打包成 zip 发给家人，隐私记忆永远留在本地。

## 核心体验闭环（5 分钟可完整演示）

**挂**（手机浏览器传照片/录音/日记 → 光点挂上老物件）→ **走**（星尘路径引导、先闻其声、回忆重演）→ **传**（星河回顾 + zip 导出导入，隐私过滤）。

## 签名机制（创新点）

| 机制 | 说明 |
|---|---|
| 记忆褪色 | 超过阈值天数未回访的记忆，光点变暗、声音加低通像隔着时间；回访即点亮 |
| 回忆重演 | 四拍时序：现实退后 → 回忆绽放（分层伪 3D）→ 重返当时（空间音频+日记字幕）→ 温柔收回 |
| 星河回顾 | 走读终点，碎片缩略图连成星座，按路线发光连线，点击即重演 |

## 功能分级（V2 收敛后）

| 级别 | 功能 |
|---|---|
| P0（MVP） | 光点锚定+悬停预览、图片/录音/日记、全景房间×2、手动排序+走读路线、浏览器局域网上传、公开/隐私+zip 导出导入、记忆褪色 |
| P1（争取） | 视频记忆（低码率）、家庭本地多档案合并、多维排序、光点显隐开关、手势辅助 |
| P2（二期） | 全景替换、云端家庭同步、链接分享、实时共创、3DGS |

## 环境（本机实测）

| 项 | 值 |
|---|---|
| Unity | 2022.3.62f3c1 LTS（含 Android Build Support / OpenJDK / SDK / NDK），`D:\unityanzhuang1\` |
| Unity Hub | 3.3.7，`D:\unity\Unity Hub` |
| IDE | VS2022 Community（含 Unity 工作负载） |
| PICO Developer Center | v1.5.0，`D:\pico\PICO Developer Center\` |
| PICO Emulator | 0.10.0-preview，`D:\PICO_Emulator\…\start-emulator.bat` |
| 硬件 | Win11 / i9-12900H / RTX 3060 Laptop 6GB / 16GB（注意 D 盘可用仅 ~32GB） |

## 快速开始

1. Unity Hub 打开本工程（Editor 2022.3.62f3c1）。
2. Package Manager 确认：`com.unity.xr.openxr`、`com.unity.nuget.newtonsoft-json`、PICO Unity Integration SDK、`com.unity.render-pipelines.universal`。
3. 日常开发：Editor Play（OpenXR Mock / XR Device Simulator）。
4. 每周验证：Emulator 安装 APK 跑一遍。
5. 场景：`Assets/Scenes/EditorScene.unity`（编排）/ `TourScene.unity`（走读）。
6. Demo 素材：`Assets/Art/Panoramas`、`Assets/Audio`；用户素材落 `PersistentDataPath/Media`。

## 目录结构

```
Assets/
├── Scripts/
│   ├── Core/        # 数据模型(Newtonsoft)、存档、褪色、星河回顾、房间管理、编排与走读
│   ├── Interaction/ # 射线拾取、传送、光点锚点（悬停预览）
│   └── Media/       # ImportServer(HTTP)、分层伪3D、回忆重演时序、空间音频、记忆面板
├── Scenes/  ├── Prefabs/  ├── Art/Panoramas/  └── Audio/
docs/
├── TECH_DESIGN_V2.md        # 当前有效设计文档
├── RESEARCH_COMPETITORS.md  # 竞品调研与差异化
├── ASSET_LIST.md            # 素材清单与授权记录
└── devlogs/                 # 每周进展录屏（路演与申报素材）
assets_raw/                  # 素材暂存区（全景/字体/照片/音频，见 ASSET_LIST.md）
scripts/                     # 工具脚本（占位音频生成等）
```

## 排期（38 天，双里程碑）

```
W1 地基 → W2 内容 → W3 叙事 → M2 内测闭环(11-29) → W4 打磨 → W5 冲刺 → M3 特性冻结(12-12) → W6 提交(12-19 09:00)
```

细节、风险预案、路演剧本见 [`docs/TECH_DESIGN_V2.md`](docs/TECH_DESIGN_V2.md)。

## 提交清单

- [ ] GitHub 仓库（本工程 + README + 数据格式文档）
- [ ] Emulator 可运行 APK / 工程包
- [ ] 3–5 分钟演示视频
- [ ] 申报文档与开发日志（引用 devlogs 周录屏）
- [ ] Demo 素材包（2 全景 + 8–12 碎片，含褪色与私密样例）+ 导出/导入示例 zip
