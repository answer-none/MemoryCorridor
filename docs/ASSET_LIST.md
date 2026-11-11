# 素材清单与授权记录（ASSET_LIST）

> 更新：2026-11-12 · 素材暂存区：`MemoryCorridor/assets_raw/`（进 Unity 时拷入 `Assets/` 并保留本文件授权记录）
> 授权原则：只用 CC0 / CC-BY（署名）/ OFL / Pexels License / 自制素材，杜绝版权不明来源。

## 1. 已下载 ✅

### 1.1 全景房间（assets_raw/panoramas/）

| 文件 | 规格 | 内容 | 授权 | 来源 |
|---|---|---|---|---|
| `living_room_warm.jpg` | 4096×2048 | 暖光客厅（台灯+黄顶灯）→ 房间一"奶奶的客厅" | CC BY 2.0，作者 Wonderlane（需署名） | three.js 官方仓库 `examples/textures/` |
| `courtyard_dusk.jpg` | 2048×1024 | 庭院/回廊 → 房间二"老院子"（CC0） | CC0，Poly Haven | three.js `equirectangular/royal_esplanade_2k.hdr.jpg` |

> 进工程时：ASTC 6×6 压缩；客厅图保留 4K 也可压 2K（显存预算见 TECH_DESIGN_V2 §4.3）。
> 替换升级渠道（网络畅通时手动下）：[polyhaven.com/hdris/indoor](https://polyhaven.com/hdris/indoor) 的 `anniversary_lounge`（温馨客厅）、`artist_workshop`（画室）、`bell_tower`（木结构暖光）均为 CC0。

### 1.2 中文字体（assets_raw/fonts/，下载中）

| 文件 | 用途 | 授权 |
|---|---|---|
| `LXGWWenKai-Regular.ttf` | 日记正文（手写楷体，怀旧感） | SIL OFL 1.1，免费商用 |
| `LXGWWenKai-Medium.ttf` | 标题/重点文字 | 同上 |

> 来源：[github.com/lxgw/LxgwWenKai](https://github.com/lxgw/LxgwWenKai) v1.530。
> UI 正文字体待补：思源黑体（OFL）[github.com/adobe-fonts/source-han-sans](https://github.com/adobe-fonts/source-han-sans)，进工程时经 TMP Font Asset Creator 生成。

### 1.3 演示照片（assets_raw/photos/，均 1024 宽，Pexels License 免费商用无需署名）

| 文件 | 内容 | 叙事位 |
|---|---|---|
| `pexels_38295274.jpg` | 老奶奶缝纫（黑白） | 碎片①缝纫机·主记忆 |
| `pexels_40483933.jpg` | 缝纫机特写（黑白） | 碎片①配图 |
| `pexels_17729627.jpg` | 缝纫机与布料（黑白） | 碎片①配图 |
| `pexels_38295277.jpg` | 缝纫中的手 | 碎片①配图 |
| `pexels_8847262.jpg` | 老式留声机+收音机 | 碎片②老收音机 |
| `pexels_9254536.jpg` | 老收音机上的花瓶 | 碎片②配图 |
| `pexels_12040699.jpg` | 男孩们骑自行车（黑白） | 碎片③童年自行车 |
| `pexels_39485339.jpg` | 乡村两个孩子骑车 | 碎片③配图 |
| `pexels_18608502.jpg` | 屋前骑车男孩 | 碎片③配图 |
| `pexels_3975852.jpg` | 扶着自行车的女孩 | 碎片③配图 |

> 对应页面 ID 已含在文件名中，可在 pexels.com/photos/{id} 溯源。
> 分层伪 3D 素材（fg/mg/bg 三层 PNG）将从这些照片人工/AI 抠图分层，W2 处理。

### 1.4 占位音频（assets_raw/audio/，自制 = 无版权风险，正式版后续替换）

| 文件 | 内容 | 用途 |
|---|---|---|
| `amb_evening_roomtone.wav` | 30s 暖房间底噪 | 房间一环境音 |
| `amb_courtyard_dusk.wav` | 30s 黄昏庭院风声+鸟鸣 | 房间二环境音 |
| `sfx_lightpoint_chime.wav` | 2.5s 暖色调钟声 | 光点悬停/点亮音效 |

> 由 `scripts/generate_placeholder_audio.py` 纯 stdlib 合成（44.1kHz 16bit 立体声），可重新生成与调参。

## 2. 待手动下载 📥（本机网络被墙，浏览器+代理可达）

| 素材 | 来源 | 授权 | 优先级 |
|---|---|---|---|
| 正式环境音（缝纫机声、老式电视、蝉鸣、下雨） | [freesound.org](https://freesound.org)（筛 CC0，需注册） | CC0 | W2 前 |
| 备选环境音 | [pixabay.com/sound-effects](https://pixabay.com/sound-effects/) | Pixabay 免费商用 | W2 前 |
| 旁白人声（中文女声/男声，"奶奶的客厅"文案） | 自录（手机安静环境）或 TTS 生成后人工润 | 自制 | W3 |
| 3 张全景升级图 | Poly Haven（见 §1.1 注） | CC0 | W4 视觉打磨 |

## 3. 计划生成 🛠（开发过程中用工具产出）

| 素材 | 工具 | 时点 |
|---|---|---|
| 老物件 3D 模型（缝纫机/收音机/自行车，低模） | Tripo AI 生成 GLB → Unity 导入 | W2 |
| 分层伪 3D 三层图（fg/mg/bg） | 照片抠图分层（AI 抠图 + 手工修） | W2 |
| 光点/星尘/尘埃粒子材质 | Unity VFX Graph / Shuriken 程序化 | W1-W2 |
| Demo 叙事文案（8-12 条碎片标题+日记正文） | 按"奶奶的客厅/老院子"两幕剧本撰写 | W2 |

## 4. 授权署名义务（打包进 APK 的 README/关于页需含）

- CC BY 2.0：`living_room_warm.jpg` © Wonderlane（[flickr 来源](https://www.flickr.com/photos/wonderlane/2294472375)）
- CC0：`courtyard_dusk.jpg` © Poly Haven
- OFL 1.1：霞鹜文楷 © 落霞孤鹜/lxgw（含字体副本时需附 OFL 协议文本）
- Pexels License：10 张照片 © 各原作者（无需署名但不得单独转售）
- 自制：`assets_raw/audio/*.wav`（项目自制，随仓库 MIT 一起开源）
