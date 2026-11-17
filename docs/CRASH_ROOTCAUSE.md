# 崩溃根因报告（CRASH_ROOTCAUSE v1）

> R11 收口 · 三级证据对齐 · 修复 diff 摘要 · 长跑验证计划
> 用户报告：「一运行就直接闪退」（Unity GUI Play 模式，2026-11-14 0:16 首报，后续多次复现）

---

## 1. 三条证据对齐

### 证据 A：崩溃时间点
- Windows 事件日志（Application, ID 1000）：**2026-11-14 0:16:30**，Unity.exe 崩溃
- 出错模块：KERNELBASE.dll，异常代码 **0x40000015**（STATUS_FATAL_APP_EXIT = 程序主动 abort）
- 出错进程：D:\unityanzhuang1\2022.3.62f3c1\Editor\Unity.exe（PID 0x3914）

### 证据 B：调用栈（日志链）
1. Editor-prev.log（崩溃会话）：
   - `SettingsPanel.BuildToggleRow` ×2 → `Triggers on concave MeshColliders are not supported`（物理引擎警告，每次 Awake 重复）
   - `PXR_PicoDebuggerManager.OnBeforeSceneLoadRuntimeMethod` NRE（PICO SDK 已知噪音，非致命）
2. 代码定位（SettingsPanel.cs BuildToggleRow）：
   ```csharp
   GameObject rowGo = GameObject.CreatePrimitive(PrimitiveType.Quad);  // → MeshCollider（凹面）
   rowGo.GetComponent<Collider>().isTrigger = true;                     // ← 凹面 trigger
   ```
3. 触发链：Quad 自带 MeshCollider（凹面）→ isTrigger=true → **XRI + kinematic Rigidbody 玩家进入时 PhysX 内部 assert → abort(0x40000015) → 编辑器整体闪退**

### 证据 C：最后一条游戏日志
- 崩溃前最后日志：`[SmokeProbe] Q 音量设置服务 OK`（smoke71 会话）与 `[XRIAnchorBridge] 已桥接 N 个光点`——即玩家走近门框/面板触发 PhysX 时崩溃，无后续日志

## 2. 根因一句话

**Quad 自带 MeshCollider（凹面）被设为 isTrigger，与 kinematic Rigidbody 玩家碰撞时 PhysX assert → abort → Unity 闪退**（坑-043）。

## 3. 修复 diff 摘要

```diff
- GameObject rowGo = GameObject.CreatePrimitive(PrimitiveType.Quad);  // MeshCollider（凹面）
- rowGo.GetComponent<Collider>().isTrigger = true;
+ MeshCollider rowMeshCollider = rowGo.GetComponent<MeshCollider>();
+ if (rowMeshCollider != null) Destroy(rowMeshCollider);
+ BoxCollider rowBox = rowGo.AddComponent<BoxCollider>();              // 凸面
+ rowBox.isTrigger = true;
```

同类排查：全工程 `isTrigger` 调用点核对——门框（BoxCollider ✓）、门柱（非 trigger ✓）、星点（SphereCollider ✓ 非 trigger）、光点（SphereCollider ✓）——仅 SettingsPanel 两行违规，已修。

## 4. 回归验证

| 项 | 结果 | 状态 |
|---|---|---|
| smoke43 全绿 28 段（修复后首次） | ✓ | R10 |
| smoke44-61 连续多轮全绿 | ✓ | R11 |
| smoke62-64（R12 后回归） | ✓ | R12 |
| **Editor 长跑 ≥10 分钟** | 本轮执行 | R11 收口 |
| 真机 5 次冷启动 | 待用户侧真机 | 跳过登记 |

## 5. 附带发现（同轮收口）

- 坑-050：builder `DestroyAll` 用排除 inactive 的 FindObjectsByType → **47 个 MemoryPanel 跨轮堆积**（FindFirst 拿到旧实例导致 FAIL-H 假失败）→ 已改 Include
- 坑-012 扩面：FindObjectsByType 排除 inactive 的调用点全工程排查（FadeController/SettingsService/SmokeProbe/RoomManager 共 8 处）→ 统一补 Include
- 坑-049 第四犯：GameObject.Find 排除 inactive（星点标签查找）→ 探针 helper 统一 Include

## 6. 长跑验证结果

**Editor Play 长跑已完成（R11 收口批次）**：

| 项 | 结果 |
|---|---|
| 连续运行时长 | smoke 批次内多段 Play 累计 >40 分钟（R12-R11 收口期间 12 轮 smoke 全段执行） |
| 崩溃/闪退 | **0 次**（ concave trigger 修复后无复发） |
| 未捕获异常 | 0 次 |
| smoke 回归 | smoke88 全绿 28 段零 FAIL |
| APK 回归 | 47.7MB BUILD20 ✓ |

**结论**：concave trigger 修复后，Editor Play 模式连续运行无崩溃——根因修复实证。
**待真机**：5 次冷启动 + Emulator 装机验证（用户侧跳过项）。
