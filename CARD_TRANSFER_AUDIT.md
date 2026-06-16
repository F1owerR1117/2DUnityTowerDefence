# 农民传送手牌 + LaneArea 显示异常审计报告

## 问题 1：传送手牌无法正确扣减手牌

### 现象

联机模式下，农民传送手牌后：
- 接收方暂存槽正确收到牌 ✅
- 发送方手牌 UI 未刷新（牌仍显示在手中）❌

### 完整调用链追踪

```
发送方农民 LaunchTubeUI.OnCardTransmitted
  → NetworkGameManager.RequestCardTransfer(card)
  → _net.SendToMaster(CARD_TRANSFER, [senderSlot, cardIndex])
  → Master: HandleCardTransferOnMaster → MasterHandleCardTransfer(senderSlot, cardIndex)
    → FindTeammateSlot(senderSlot) → receiverSlot ✅
    → _slotHands[senderSlot].Remove(card) ✅ 从 Master 追踪的手牌中移除
    → SendToAll(CARD_ARRIVE, ...) ✅ 广播给所有客户端
    → senderSlot == _mySlot? → 仅当 Master 自己是发送方时执行 ↓
      → _playerHand.Remove(card) ✅
      → _cardCounter?.Refresh() ✅
      → _handArea?.NotifyHandChanged() ✅
```

### 🔴 根因

**`MasterHandleCardTransfer()` L1756-1761：仅当 `senderSlot == _mySlot` 时刷新发送方 UI。**

当发送方是**远程农民**（非 Master）时：
- `_slotHands[senderSlot].Remove(card)` ✅ 数据层已移除（Master 追踪）
- `_playerHand.Remove(card)` ❌ 未执行（senderSlot != _mySlot）
- `_handArea?.NotifyHandChanged()` ❌ 未执行
- `_cardCounter?.Refresh()` ❌ 未执行

**结果**：发送方的 `playerHand` 数据虽然被移除（与 `_slotHands[senderSlot]` 是同一引用），但 HandArea UI 未收到通知，手牌显示不刷新。

### 修复方案

在 `MasterHandleCardTransfer()` 末尾，对**所有发送方**广播手牌刷新事件：

```csharp
// 方案 A：广播 hand refresh 给发送方
_net.SendToPlayer(_actorNumbers[senderSlot], NetworkProtocol.HAND_REFRESH, 0);

// 方案 B：在 CARD_ARRIVE 广播中附带 senderSlot，让发送方自行刷新
// （需要新增 HandleCardArrive 中的发送方处理分支）
```

---

## 问题 2：地主 LaneArea 在农民身份时仍然显示

### 现象

联机模式下，农民身份的玩家仍能看到地主的分路选择 UI（LaneArea）。

### 代码追踪

**单机模式** (`GameBootstrapper.cs` L472-480)：
```csharp
if (playerIsLandlord) {
    launchTubeUI?.gameObject.SetActive(false);
    tempSlotUI?.gameObject.SetActive(false);
    teammateTempSlotUI?.gameObject.SetActive(false);
} else {
    laneArea?.SetActive(false);              // ✅ 农民隐藏 LaneArea
    handArea?.SetRouteUIVisible(false);      // ✅ 农民隐藏路线 UI
}
```

**联机模式** (`GameBootstrapper.cs` L351-376)：
```csharp
if (playerIsLandlord)
{
    launchTubeUI?.gameObject.SetActive(false);
    tempSlotUI?.gameObject.SetActive(false);
    teammateTempSlotUI?.gameObject.SetActive(false);
    // ⚠️ 缺少：laneArea?.SetActive(false)
}
else
{
    // 农民：初始化飞筒...
    // ⚠️ 缺少：laneArea?.SetActive(false)
    // ⚠️ 缺少：handArea?.SetRouteUIVisible(false)
}
```

### 🔴 根因

**联机模式的 Step 6b（L351-376）中没有处理 `laneArea` 和 `handArea.SetRouteUIVisible()`。**

单机模式在 L472-480 处理了 UI 显隐，但联机模式的代码路径不经过此处。

### 修复方案

在 `GameBootstrapper.cs` 联机模式 Step 6b 中补充 UI 显隐逻辑：

```csharp
if (_isNetworkMode)
{
    if (playerIsLandlord)
    {
        launchTubeUI?.gameObject.SetActive(false);
        tempSlotUI?.gameObject.SetActive(false);
        teammateTempSlotUI?.gameObject.SetActive(false);
        // 地主：保持 LaneArea 可见（无需额外操作）
    }
    else
    {
        // 农民：隐藏飞筒 + 暂存槽初始化...
        
        // 农民：隐藏 LaneArea
        laneArea?.SetActive(false);
        handArea?.SetRouteUIVisible(false);
    }
}
```

---

## 问题汇总

| # | 问题 | 根因 | 风险 | 修复位置 |
|---|------|------|------|----------|
| 1 | 传送手牌发送方 UI 不刷新 | MasterHandleCardTransfer 仅 Master 自身发送时刷新 UI | P0 | NetworkGameManager.cs L1756-1761 |
| 2 | 农民身份显示 LaneArea | 联机模式 Step 6b 缺少 laneArea 显隐处理 | P1 | GameBootstrapper.cs L351-376 |

---

## 修改计划

### Step 1：修复 LaneArea 显隐（低风险）

**文件**：`GameBootstrapper.cs`
**位置**：L351-376（联机模式 Step 6b）
**改动**：在 `else`（农民）分支末尾添加：
```csharp
laneArea?.SetActive(false);
handArea?.SetRouteUIVisible(false);
```

### Step 2：修复传送手牌 UI 刷新（中风险）

**文件**：`NetworkGameManager.cs`
**位置**：`MasterHandleCardTransfer()` L1723-1763
**改动**：在方法末尾，对非 Master 发送方广播 hand refresh：

需确认是否存在 `HAND_REFRESH` 协议。若不存在，可复用 `GOLD_UPDATE` 模式，在 `CARD_ARRIVE` 广播中附带 senderSlot，让发送方在 `HandleCardArrive` 中刷新手牌。

**方案对比**：

| 方案 | 改动量 | 风险 | 说明 |
|------|--------|------|------|
| A: 新增 HAND_REFRESH 协议 | 中 | 低 | 最清晰，但需改 NetworkProtocol |
| B: CARD_ARRIVE 附带 senderSlot | 小 | 低 | 发送方收到 CARD_ARRIVE 后自行刷新 |
| C: Master 广播 SendToPlayer 给 sender | 小 | 低 | 直接通知发送方刷新 |

**推荐方案 C**：在 `MasterHandleCardTransfer()` 中添加：
```csharp
// 通知发送方刷新手牌 UI
if (senderSlot != _mySlot)
    _net.SendToPlayer(_actorNumbers[senderSlot], NetworkProtocol.CARD_ARRIVE, 
        new object[] { senderSlot, senderSlot, cardIndex });
```
发送方收到后执行 `_handArea?.NotifyHandChanged()` + `_cardCounter?.Refresh()`。
