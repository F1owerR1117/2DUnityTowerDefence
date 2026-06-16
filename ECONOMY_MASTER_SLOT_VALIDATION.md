# Master 槽位经济注册验证报告

## 检查 1：赋值时机审计

### `_mySlot` 赋值

| 项目 | 值 |
|------|-----|
| 文件 | `NetworkGameManager.cs` |
| 行号 | L165 |
| 代码 | `_mySlot = NetworkProtocol.GetPlayerSlot(_net.LocalActorNumber, _actorNumbers);` |
| 调用时机 | `Initialize()` 内，L164 之后 |

### `_economyManager.CoreEconomy` 创建

| 项目 | 值 |
|------|-----|
| 创建文件 | `GameBootstrapper.cs` |
| 创建行号 | L151 |
| 创建代码 | `_economyLogic = new EconomySystem(econConfig.initialGold, incomeRate);` |
| 注入行号 | L216 |
| 注入代码 | `economyManager.Initialize(_economyLogic, timerQueue, gameStateMachine);` |
| 调用时机 | Step 5（依赖注入焊接），远早于 Step 11 |

### 时序确认

```
GameBootstrapper.Start()
  Step 5 (L151):  _economyLogic = new EconomySystem(...)
  Step 5 (L216):  economyManager.Initialize(_economyLogic, ...)  → CoreEconomy = _economyLogic
  Step 11 (L711): _networkGameManager.Initialize(net, battleManager, economyManager, ...)
                   → L165: _mySlot = NetworkProtocol.GetPlayerSlot(...)
                   → L144: _economyManager = economyManager  (引用同一对象)
```

**结论**：`_economyManager.CoreEconomy` 在 `_mySlot` 赋值之前已创建完毕。两者无时序冲突。

---

## 检查 2：所有 `_slotEconomies[slot] = xxx` 写入点

| # | 文件 | 函数 | 行号 | 注册槽位 | 包含 Master? |
|---|------|------|------|----------|-------------|
| 1 | NetworkGameManager.cs | `RegisterSlotEconomy()` | L108 | 由外部传入（AI 槽位） | ⚠️ 由调用方决定 |
| 2 | NetworkGameManager.cs | `HandlePlayRequest()` | L1365 | `playerSlot`（远程玩家，`playerSlot != _mySlot`） | ❌ 排除 Master |
| 3 | NetworkGameManager.cs | `HandleDrawRequest()` | L1586 | `targetSlot`（远程玩家，`targetSlot != _mySlot`） | ❌ 排除 Master |
| 4 | NetworkGameManager.cs | `OnPlayerLeft()` | L2121 | `disconnectedSlot`（断线玩家转 AI） | ⚠️ 理论上可能 |
| 5 | NetworkGameManager.cs | `HandlePlayerReady()` | L2197 | `slot`（远程玩家发送 PLAYER_READY） | ❌ Master 不发送 |

### 详细分析

**写入点 1 — `RegisterSlotEconomy()` (L108)**
- 由 `GameBootstrapper` L739 调用：`_networkGameManager.RegisterSlotEconomy(slot, ai.Economy)`
- 遍历条件：`GameSession.AISlots.Contains(slot)`（L734）
- Master 自身槽位：`_mySlot` 通常不在 `AISlots` 中（Master 是真人玩家）
- **结论**：Master 槽位不通过此路径注册

**写入点 2 — `HandlePlayRequest()` (L1365)**
- 条件守卫：`playerSlot != _mySlot`（L1359）
- **结论**：Master 自身的出牌请求不走此分支，不会注册

**写入点 3 — `HandleDrawRequest()` (L1586)**
- 条件守卫：`targetSlot != _mySlot`（L1581）
- **结论**：Master 自身的摸牌请求不走此分支，不会注册

**写入点 4 — `OnPlayerLeft()` (L2121)**
- 触发条件：其他玩家断线，`disconnectedSlot` 转为 AI
- Master 自身断线：Master 断线后不再是 Master，不会执行此代码
- **结论**：理论上不涉及 Master 槽位

**写入点 5 — `HandlePlayerReady()` (L2197)**
- 触发条件：远程客户端发送 `PLAYER_READY`
- Master 自身：L192 直接 `_playerReadyReceived.Add(_mySlot)`，不发送 PLAYER_READY
- **结论**：Master 不通过此路径注册

### 额外发现：Master 的出牌/摸牌使用 `_economyManager.CoreEconomy` 绕过 `_slotEconomies`

| 场景 | 代码位置 | 使用的经济引用 |
|------|----------|---------------|
| Master 出牌 | L1350-1351 | `_economyManager?.CoreEconomy`（直接引用） |
| Master 摸牌 | L1577-1578 | `_economyManager?.CoreEconomy`（直接引用） |

这意味着 Master 的**金币消耗**通过 `_economyManager.CoreEconomy` 正常执行，但**金币增长**（`UpdateEconomy`）虽然也在 `_economyManager.CoreEconomy` 上执行，却不会反映到 Snapshot 中（因为 `_slotEconomies[mySlot]` 不存在）。

---

## 检查 3：运行时验证方案

### 建议添加的临时诊断日志

在 `NetworkGameManager.BuildCurrentSnapshot()` 中添加（**仅用于验证，验证后删除**）：

```csharp
// === 临时诊断日志 START ===
Debug.Log($"[ECONomy_DIAG] _mySlot={_mySlot}, " +
    $"_slotEconomies.Keys=[{string.Join(",", _slotEconomies.Keys)}], " +
    $"ContainsKey(_mySlot)={_slotEconomies.ContainsKey(_mySlot)}, " +
    $"CoreEconomyGold={_economyManager?.CoreEconomy?.CurrentGold ?? -1f:F1}");
// === 临时诊断日志 END ===
```

### 预期输出

```
[ECONomy_DIAG] _mySlot=0, _slotEconomies.Keys=[1,2], ContainsKey(_mySlot)=False, CoreEconomyGold=150.0
```

### 验证要点

| 检查项 | 预期值 | 说明 |
|--------|--------|------|
| `_slotEconomies.ContainsKey(_mySlot)` | `False` | 确认 Master 槽位未注册 |
| `_slotEconomies.Keys` | 不含 `_mySlot` | 确认只有 AI/远程玩家槽位 |
| `CoreEconomyGold` | > 0 且持续增长 | 确认 Master 经济系统正常运行 |
| `SlotGold[_mySlot]` | `0f` | 确认 Snapshot 中 Master 金币为 0 |

### 进一步验证：Client 端日志

在 `HandleMasterStateSync()` 中添加：

```csharp
// === 临时诊断日志 START ===
if (snapshot.SlotGold.ContainsKey(_mySlot))
    Debug.Log($"[ECONomy_CLIENT_DIAG] received Master gold={snapshot.SlotGold[_mySlot]:F1}");
else
    Debug.Log($"[ECONomy_CLIENT_DIAG] Master slot NOT in snapshot");
// === 临时诊断日志 END ===
```

预期：Client 收到的 Master 金币始终为 0。

---

## 检查 4：对象一致性验证

### 引用关系图

```
GameBootstrapper._economyLogic
  │
  │  new EconomySystem(initialGold, incomeRate)   ← L151 创建
  │
  ├──── economyManager.Initialize(_economyLogic)  ← L216 注入
  │       │
  │       └── EconomyManager._coreEconomy         ← CoreEconomy 属性
  │             │
  │             └── NetworkGameManager._economyManager  ← L144 赋值
  │                   │
  │                   └── _economyManager.CoreEconomy   ← 同一个 EconomySystem 实例
  │
  └──── (未注册到 _slotEconomies[_mySlot])        ← 🔴 缺失

GameBootstrapper (per AI base)
  │
  └── new EconomySystem(...)                      ← L882 独立创建
        │
        └── ai.Economy                            ← BuildingAI.Economy
              │
              └── RegisterSlotEconomy(slot, ai.Economy)  ← L739 注册
                    │
                    └── _slotEconomies[slot]      ← AI 槽位的独立 EconomySystem
```

### 结论

| 对象 | 实例 | 说明 |
|------|------|------|
| `_economyManager.CoreEconomy` | EconomySystem A | 玩家的经济系统，由 `_economyLogic` 初始化 |
| `_slotEconomies[AI_slot]` | EconomySystem B/C/... | 每个 AI 基地独立创建 |
| `_slotEconomies[_mySlot]` | **不存在** | 🔴 未注册 |

**`_economyManager.CoreEconomy` 和 `_slotEconomies[_mySlot]` 不指向同一个实例，因为后者根本不存在。**

### 双重 UpdateEconomy 风险评估

如果执行修复 `RegisterSlotEconomy(_mySlot, _economyManager.CoreEconomy)`：

| 更新源 | 是否调用 UpdateEconomy | 条件 |
|--------|----------------------|------|
| `NetworkGameManager.Update()` | ✅ 是 | `IsMasterClient` + 遍历 `_slotEconomies` |
| `EconomyManager.Update()` | ❌ 否 | 联机模式跳过（L194-198） |
| `BuildingAI.Update()` | ❌ 否 | `_networkGameManager != null` 时跳过（L119） |
| `BuildingAI.Update()` (Master 自身基地) | ❌ 否 | Master 自身基地 `ai.enabled = false`（L735） |

**风险等级：无双重更新风险。**

注册后只有 `NetworkGameManager.Update()` 驱动 `UpdateEconomy()`，与现有 AI 槽位行为一致。

---

## 综合结论

### 1. Master 槽位是否缺失

**是。** 5 个写入点均不注册 Master 自身槽位。`_slotEconomies.ContainsKey(_mySlot)` 始终返回 `false`。

### 2. 是否适合注册到 `_slotEconomies`

**是。** 条件满足：
- `_economyManager.CoreEconomy` 已在 `Initialize()` 之前创建
- 注册后无双重 `UpdateEconomy` 风险（EconomyManager 和 BuildingAI 在联机模式下均跳过）
- 与现有 AI 槽位注册模式一致

### 3. 是否存在双 UpdateEconomy 风险

**不存在。** 三个潜在更新源中，只有 `NetworkGameManager.Update()` 在联机模式下执行 `UpdateEconomy()`。

### 4. 推荐最终修复方案

**方案 A（最小改动，推荐）**：在 `NetworkGameManager.Initialize()` 中，Master 初始化后注册自身经济：

```csharp
// NetworkGameManager.Initialize() 末尾，L194 之后
if (_net.IsMasterClient && _economyManager?.CoreEconomy != null)
{
    _slotEconomies[_mySlot] = _economyManager.CoreEconomy;
}
```

**修复效果**：
- `BuildCurrentSnapshot()` L525: `_slotEconomies.ContainsKey(_mySlot)` → `true`
- `snapshot.SlotGold[_mySlot]` → 正确的 Master 金币值
- Client `HandleMasterStateSync()` → `SetGold(正确值)`
- UI 显示正确金币

**额外考虑**：`HandlePlayerReady()` L2186-2189 对已存在的槽位执行 `SetGold(initGold)`。如果远程玩家发送的 `initGold` 与 Master 追踪的值不一致，会覆盖 Master 的金币。但这只影响远程玩家槽位，不影响 Master 自身。
