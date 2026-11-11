# AGENTS.md — 回响长廊 MemoryCorridor

PICO Space Program 2026 第三赛道（最佳沉浸内容创作）参赛工程。
MR 记忆策展：真实物品挂光点 → 先闻其声 → 回忆重演 → 星河回顾 → 隐私导出。

## 开工必读（按序）

1. `docs/MEMORY.md` — 踩坑记忆与环境事实（**每个会话先读，结束时必须更新"当前状态"**）
2. `docs/TECH_DESIGN_V2.md` — 唯一有效设计文档（P0/P1 分级、视觉交互规范、排期）
3. `docs/ASSET_LIST.md` — 素材授权登记（新素材必须登记）

## 关键路径

- Unity 工程：`D:\unityxiangmu\MemoryCorridor\`（Editor 2022.3.62f3c1 中国版）
- 素材暂存：`assets_raw/`（进 Unity 前先看授权）
- 硬性约束：显存按 4GB 预算、内存 16GB、D 盘 <20GB 先清理

## 硬规矩

- JSON 一律 Newtonsoft（坑-004）；代码显式类型；命名空间 `MemoryCorridor.*`
- 下载素材必须魔数校验（坑-005）；授权只收 CC0/CC-BY/OFL/Pexels/自制（坑-008）
- M3 特性冻结（12-12）后只修 bug；每周六 devlog 录屏进 `docs/devlogs/`
- 提交小步走；Library/Temp/Logs 不进库
