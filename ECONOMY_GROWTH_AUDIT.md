# 联机金币异常审计报告

## 问题现象

联机模式下，玩家金币不自动增长。

---

## 完整金币链路追踪

### 1. UpdateEconomy 是否执行

| 位置 | 代码 | 执行条件 | 状态 |
|------|------|----------|------|
| `NetworkGameManager.Update()` L457-461 | `kvp.Value.UpdateEconomy(dt)` | `IsMasterClient` + `_slotEconomies` 遍历 | ✅ Master 执行 |
| `EconomyManager.Update()` L190-203 | `_coreEconomy?.UpdateEconomy(Time.deltaTime)` | `!IsInRoom`（单人模式） | ⚠️ 联机模式跳过 |

**结论**：Master 端 `_slotEconomies` 中的 EconomySystem 正确执行 `UpdateEconomy()`。Client 端 `EconomyManager.Update()` 在联机模式下跳过（由 Snapshot 覆盖）。

### 2. CurrentGold 是否增长

**Master 端**：
- `_slotEconomies[slot].UpdateEconomy(dt)` → `EconomySystem.CurrentGold += IncomeRate * dt` ✅ 正常增长

**Client 端**：
- `_economyManager.CoreEconomy` 不调用 `UpdateEconomy()`（联机模式跳过）
- 依赖 Snapshot 广播更新金币

### 3. Snapshot 是否携带金币

**`BuildCurrentSnapshot()` L525**:
```csharp
snapshot.SlotGold[slot] = _slotEconomies.ContainsKey(slot) 
    ? _slotEconomies[slot].CurrentGold : 0f;
```

**🔴 关键问题：Master 自身槽位未注册到 `_slotEconomies`**

`_slotEconomies` 的填充来源：
| 来源 | 代码位置 | 注册的槽位 |
|------|----------|-----------|
| `RegisterSlotEconomy()` | L739 | 仅 AI 槽位（非 Master 自身） |
| `HandlePlayerReady()` | L2196 | 远程玩家槽位 |
| `HandlePlayRequest()` 自动创建 | L1364 | 远程玩家槽位（延迟到达时） |

**Master 自身槽位 `_mySlot` 从未被注册到 `_slotEconomies`。**

因此 `BuildCurrentSnapshot()` 对 Master 槽位返回 `0f`：
```csharp
_snapshot.SlotGold[mySlot] = 0f;  // ContainsKey 返回 false
```

### 4. Client 是否收到金币

**`HandleMasterStateSync()` L810-814**:
```csharp
foreach (var kvp in snapshot.SlotGold)
{
    int slot = kvp.Key;
    if (_slotEconomies.ContainsKey(slot))
        _slotEconomies[slot].SetGold(kvp.Value);  // SetGold(0f)
}
```

- Client 收到 Snapshot，但 Master 槽位的金币被设为 0
- Client 自身槽位的 `_slotEconomies[mySlot]` 也被设为 0

### 5. UI 是否刷新

**`EconomyManager.SetGold()` L111-114**:
```csharp
public void SetGold(float amount)
{
    _coreEconomy?.SetGold(amount);
}
```

`SetGold()` 内部调用 `OnGoldChanged?.Invoke(CurrentGold)` → 触发 `UpdateGoldUI()`。

**但问题在于**：`HandleMasterStateSync()` 只更新 `_slotEconomies[slot].SetGold()`，不更新 `_economyManager.SetGold()`。

然而，`_slotEconomies[mySlot]` 和 `_economyManager.CoreEconomy` **是同一个对象引用**（都指向 `_economyLogic`），所以 UI 理论上会刷新。但由于金币被设为 0，显示的始终是 0。

---

## 根因分析

### 主因：Master 槽位金币在 Snapshot 中为 0

**调用链**：
```
BuildCurrentSnapshot()
  → _slotEconomies.ContainsKey(mySlot)  // false！Master 槽位未注册
  → SlotGold[mySlot] = 0f
  → SendToAll(MASTER_STATE_SYNC, snapshot)
  → Client HandleMasterStateSync()
  → _slotEconomies[mySlot].SetGold(0f)
  → UI 显示 0
```

**为什么 Master 槽位未注册？**

1. `RegisterSlotEconomy()` (L739) 只为 AI 槽位注册
2. Master 自身不发送 `PLAYER_READY`（L192 直接加入 `_playerReadyReceived`）
3. `HandlePlayerReady()` 只处理远程玩家
4. Master 的 `_economyManager.CoreEconomy` 虽然是有效的 EconomySystem，但未放入 `_slotEconomies[mySlot]`

### 次因：Client 的经济增长被 Snapshot 覆盖

即使 Master 槽位金币正确，Client 端也存在问题：
- `EconomyManager.Update()` 在联机模式下不调用 `UpdateEconomy()`
- Client 的金币完全依赖 Snapshot（每 5 秒一次）
- Snapshot 到达时 `SetGold()` 覆盖 Client 本地值
- 如果 Snapshot 中金币增长正确，Client 会看到阶梯式增长（每 5 秒跳变一次），而非平滑增长

---

## 风险等级

**🔴 P0 - 严重**

Master 自身金币在 Snapshot 中始终为 0，所有 Client 看到的 Master 金币为 0。

---

## 推荐修复方案

### 方案 A：注册 Master 槽位经济（推荐）

在 `NetworkGameManager.Initialize()` 中，Master 初始化时将自身经济注册到 `_slotEconomies`：

```csharp
// NetworkGameManager.Initialize() 末尾
if (_net.IsMasterClient && _economyManager?.CoreEconomy != null)
{
    _slotEconomies[_mySlot] = _economyManager.CoreEconomy;
}
```

**优点**：最小改动，复用现有 `_slotEconomies` 机制
**注意**：需确认 `_economyManager.CoreEconomy` 和 BuildingAI 的 Economy 不是同一个对象（避免双重更新）

### 方案 B：HandleMasterStateSync 同步到 _economyManager

在 `HandleMasterStateSync()` 中增加：
```csharp
if (slot == _mySlot && _economyManager != null)
    _economyManager.SetGold(kvp.Value);
```

**优点**：直接更新 UI 经济系统
**缺点**：不解决 Snapshot 中金币为 0 的根因

### 方案 C：两者结合

同时实施 A 和 B，确保 Master 槽位金币正确且 UI 同步。

---

## 附加发现

### Client 端金币同步的 "自锁" 机制

`HandleGoldUpdate()` L1851-1853：
```csharp
if (slot == _mySlot && !_net.IsMasterClient)
    return;  // Client 忽略自身槽位的 GOLD_UPDATE
```

Client 不接受其他客户端对自己金币的覆盖（合理设计），但这也意味着 Client 的金币只能通过 Snapshot 更新。Snapshot 频率为 5 秒（`STATE_SYNC_INTERVAL = 5f`），导致 Client 金币显示存在最大 5 秒延迟。

### Client 的 "反向同步" 无意义

`NetworkGameManager.Update()` L442-453：Client 每 3 秒向 Master 发送自己的金币值。但 Master 的 `HandleGoldUpdate()` 对自身槽位返回（L1837），且 Master 使用自己追踪的 `_slotEconomies` 而非 Client 报告的值。此机制的实际作用有限。
