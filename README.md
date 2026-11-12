# 回响长廊 · MemoryCorridor

> PICO Space Program 2026 · 最佳沉浸内容创作赛道（Unity OpenXR）
> 把家的记忆挂回真实的房间——MR 个人与家庭记忆策展空间

## 一句话

在真实物品上挂一粒萤火般的光点，走近它先听见当年的声音，光点绽放成那一刻的回忆；太久不去看，记忆会慢慢褪色，直到你再次点亮它。走完生成一面星河回顾墙；整个房间打包成 zip 发给家人，隐私记忆永远留在本地。

## 核心机制

| 机制 | 说明 | 自动化断言 |
|---|---|---|
| 记忆褪色 | 未回访记忆光点变暗、声音隔时间（低通）；回访即点亮 | B 段 fresh=1.00 vs faded=0.39 |
| 回忆重演 | 四拍时序：现实退后→回忆绽放（分层伪 3D）→重返当时→温柔收回；可跳过 | G 段 |
| 星河回顾 | 走读终点碎片连成星座，点星即重演（含缩略图+标题标签） | H/H2 段 |
| 先闻其声 | 光点 3m 内空间音频渐近；悬停 0.3s 出预览小卡 | B 段预览卡 |
| 类型配色 | 图=琥珀 / 音=青蓝 / 日记=米白 / 混合=暖橙 / 私密=暖红 | B 段配色 |
| 隐私导出 | 私密记忆不进导出包、他人视角无光点；zip 导出导入 | K 段链路 |
| 欢迎引导 | 入口告示牌，首条记忆选中后收起 | P 段 |
| 录音播放 | 面板内 UnityWebRequest 异步加载 WAV/MP3 | L 段 |
| 音量服务 | master→AudioListener / narration→录音 / ambient→环境音 | Q 段 |

**质量保障**：批处理一键回归 `smoke`（23 段断言）+ Android 构建回归，任何改动不破坏现有功能。

## 环境（本机实测）

| 项 | 值 |
|---|---|
| Unity | 2022.3.62f3c1 中国版 LTS（含 Android Build Support），`D:\unityanzhuang1\` |
| SDK | PICO Integration SDK v3.3.3（嵌入 `Packages/com.unity.xr.picoxr`）+ OpenXR 1.9.1 + XRI 2.5.4 + URP 14.0.11 |
| 设备链 | PICO 4 Ultra（真机）/ PICO Emulator 0.10.0-preview / Editor Play（XR Device Simulator） |
| 硬件 | Win11 / i9-12900H / RTX 3060 6GB / 16GB |

## 快速开始

1. Unity Hub 打开本工程（Editor 2022.3.62f3c1）。
2. 场景：`Assets/Scenes/EditorScene.unity` → 按 ▶️ → WASD 移动 / 鼠标视角 / 空格或左键选中。
3. 无素材时可玩内置 Demo：2 房间 8 碎片（含褪色对照×2、私密样例×1、纯日记×1）。
4. 手机浏览器传素材：应用启动后访问 `http://<设备IP>:8899/`（局域网 HTTP 导入服务）。

## 构建与测试

```bash
# 一键回归（23 段断言，PASS→退出码 0）
Unity.exe -batchmode -projectPath D:/unityxiangmu/MemoryCorridor \
  -executeMethod MemoryCorridor.EditorTools.LightPointSceneBuilder.BuildAndSmoke -logFile smoke.log

# Android APK（IL2CPP/ARM64/ASTC/Linear → D:/unityxiangmu/_builds/）
Unity.exe -batchmode -projectPath D:/unityxiangmu/MemoryCorridor \
  -executeMethod MemoryCorridor.EditorTools.AndroidBuildScript.Build -logFile build.log
```

## 目录

```
Assets/Scripts/Core/        # 数据模型(Newtonsoft)/房间/走读/排序/褪色/音量服务/冒烟探针
Assets/Scripts/Interaction/ # 光点锚点/XRI 桥接器/射线
Assets/Scripts/Media/       # 面板/预览卡/星河墙/重演/穹顶/粒子/导入服务/告示牌
Assets/Editor/              # SetupWizard / LightPointSceneBuilder / AndroidBuildScript
Assets/Samples/XR Interaction Toolkit/2.5.4/  # 官方 XR Rig + 设备模拟器（勿删，GUID 引用）
Packages/com.unity.xr.picoxr/ # PICO Integration SDK v3.3.3（本地嵌入包）
assets_raw/                 # 素材暂存（授权登记见 docs/ASSET_LIST.md）
docs/                       # TECH_DESIGN_V2 / TEAM_PLAN / RESEARCH_COMPETITORS / ASSET_LIST
                            # / MEMORY(踩坑) / HANDOFF(交接) / APPLICATION(申报) / DEVLOG_SCRIPT
```

## 排期

W1 地基 → W2 机制+内容 → W3 叙事 → R1/R2 团队化迭代（交互闭环+音频引导）→ **M2 内测包已就绪** → R3 交付材料 → M3 冻结（12-12）→ 提交（12-19 09:00）→ 决赛路演（12-26 北京）。

## 提交清单

- [x] GitHub 仓库：github.com/answer-none/MemoryCorridor（public）
- [ ] APK（`_builds/MemoryCorridor_M2.apk`，42.9MB）
- [ ] 演示视频 3–5 分钟（脚本就绪 `docs/DEVLOG_SCRIPT.md`）
- [ ] 申报文档（骨架就绪 `docs/APPLICATION.md`，待填团队/日志链接）
- [ ] Demo 素材包 + 导出/导入示例 zip
