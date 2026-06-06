# DoudizhuTower — UI 功能清单与实现现状

> 最后更新：2026-06-07

---

## 一、核心游戏流程

```
主菜单 → 关卡选择/联机大厅 → 叫分（30s）→ 对局（5min）→ 骤死期（1min）→ 结算
  ✅         ✅              ✅ 已实现      ✅ 已实现     ✅ 已实现     ✅ 已实现
```

---

## 二、已实现功能

### 1. 手牌区（HandArea）

| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 卡牌列表 | 横向排列展示手牌 | `HandArea.cs` + `CardWidget.cs` |
| 卡牌选中 | 点击上浮 + 放大，实时牌型检测 | `SelectionValidator.cs` |
| 部署按钮 | 合规牌型时绿色可点击 | `HandArea.cs` |
| 路线切换 | 上一条/下一条路线 | `RouteGroup.cs` |
| 手牌排序 | 按点数排序 | `CardHand.Sort()` |
| 3换1回收 | 拖入单张牌，累计 3 张自动抽牌 | `HandArea.OnCardDiscardRequested()` |
| 牌型封印 | 被封印的牌显示锁链 + 拒绝交互 | `CardWidget.SetSealed()` |

### 2. 基地血条（BaseHealthBar）

| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 三个基地血条 | 阵营颜色自动判定（绿=友方/红=敌方） | `BaseHealthBar.cs` |
| 血量读取 | 从 `CardUnit(_isBuilding)` 读取 HP/MaxHP | `BaseHealthBar.cs` |

### 3. 金币显示（GoldDisplay）

| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 金币数额 | floor 取整显示 | `GoldDisplay.cs` |
| 回金速度 | "+5.0/s" 格式 | `GoldDisplay.cs` |

### 4. 记牌器（CardCounterUI）

| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 点数矩阵 | 横向 15 格"已出/4"格式 | `CardCounterUI.cs` |
| 已断标记 | 4 张全出后变灰 | `CardCounterUI.Refresh()` |
| 牌堆剩余 | "牌堆：X 张" | `CardCounterUI.cs` |

### 5. 叫分系统（BiddingManager）

| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 叫分面板 | 1/2/3 分 + 不叫按钮 | `BiddingManager.cs` |
| 30 秒倒计时 | 超时随机分配 | `BiddingManager.cs` |
| AI 叫分 | 权重可配 | `BiddingConfig.cs` |

### 6. 要不起领域（DomainUIController）

| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 领域按钮 | 地主侧方激活按钮 | `DomainUIController.cs` |
| 反击按钮 | 农民侧方，始终可见，状态联动 | `DomainUIController.cs` |
| 锁链覆盖 | 被封印手牌变灰 + 锁链 | `CardWidget.SetSealed()` |
| 冷却效果 | 钟表式冷却视觉 | `CoolDownEffect.cs` |

### 7. 传送飞筒（LaunchTubeUI）

| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 飞筒 UI | 拖拽传牌给队友 | `LaunchTubeUI.cs` |
| 暂存槽 | 接收飞筒传牌 | `TempSlotUI.cs` |
| CD 倒计时 | 6 秒冷却 | `CoolDownEffect.cs` |

### 8. 对局计时器（GameTimerUI）

| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 正计时显示 | 00:00 → 05:00 → ... | `GameTimerUI.cs` |
| 骤死期变色 | 红色文字提示 | `GameTimerUI.cs` |

### 9. 伤害飘字（DamageFloatText）

| 类型 | 颜色 | 代码文件 |
|:-----|:-----|:---------|
| 物理伤害 | 白色 | `DamageFloatText.cs` |
| 特殊伤害 | 紫色 | `DamageFloatText.cs` |
| 真实伤害 | 橙色 | `DamageFloatText.cs` |
| 大伤害（≥50） | 红色加粗 | `DamageFloatText.cs` |

### 10. 兵种信息面板（UnitInfoPanel）

| 功能 | 代码文件 |
|:-----|:---------|
| 点选兵种显示属性面板 | `UnitSelector.cs` + `UnitInfoPanel.cs` |
| 世界空间跟随目标，面向摄像机 | `UnitInfoPanel.cs` |
| 实时刷新 HP | 订阅 `CardUnit.OnHPChanged` |

### 11. 胜利/结算面板（VictoryPanel）

| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 胜负显示 | 对局结束弹出 | `VictoryPanel.cs` |
| 统计数据 | 时长/出牌数/击杀/金币 | `VictoryStats.cs` |
| 按钮 | 重新开始/返回主菜单 | `VictoryPanel.cs` |

### 12. 暂停菜单（PauseMenu）

| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| ESC 切换 | 暂停/恢复 | `PauseMenu.cs` |
| 音量滑块 | BGM + SFX | `PauseMenu.cs` |
| 按钮 | 重新开始/退出 | `PauseMenu.cs` |

### 13. 关卡选择（LevelSelectController）

| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 轮播选择 | 中心最大，两侧缩小，拖拽滑动 | `LevelSelectController.cs` |
| 关卡卡片 | 缩略图 + 信息 + 动态缩放 | `LevelCard.cs` |
| 配置 | ScriptableObject 驱动 | `LevelConfig.cs` |

### 14. 联机大厅（OnlineLobbyController）

| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 单排匹配 | 随机加入房间 | `OnlineLobbyController.cs` |
| 创建房间 | 输入房间号 | `OnlineLobbyController.cs` |
| 加入房间 | 输入房间号 | `OnlineLobbyController.cs` |
| 房间管理 | 玩家列表/准备/开始 | `OnlineLobbyController.cs` |
| 断线重连 | 失焦自动重连 + 房间恢复 | `PhotonService.cs` |

### 15. 主菜单（MainMenuController）

| 按钮 | 功能 | 代码文件 |
|:-----|:-----|:---------|
| 单人模式 | → 关卡选择 | `MainMenuController.cs` |
| 对战模式 | → 联机大厅 | `MainMenuController.cs` |
| 商店 | 预留 | — |
| 图鉴 | 预留 | — |
| 设置 | 音量等 | `MainMenuController.cs` |
| 退出 | 退出游戏 | `MainMenuController.cs` |

### 16. 其他

| 功能 | 代码文件 |
|:-----|:---------|
| 存档系统 | `SaveSystem.cs`（PlayerPrefs） |
| 场景淡入淡出 | `SceneFader.cs` |
| 全屏震动 | `ScreenEffect.cs` |
| 按钮音效 | `ButtonAudio.cs` |
| 按钮悬停/按压动画 | `ButtonEffect.cs` |
| 摄像机控制 | `CameraController.cs`（WASD + 缩放） |

---

## 三、兵种被动系统（UnitPassives）

| 被动 | Inspector 开关 | 行为 | 动画 |
|:-----|:--------------|:-----|:-----|
| 点杀 | `enableSniper` | 锁定全场血量最低敌方 | 无 |
| 人海连击 | `enableSwarm` | 周围友军追加 50% ATK | 无 |
| 冲锋一击 | `enableCharge` | 首击 ATK×2.5 | Trigger: Charge |
| 君王光环 | `enableKingAura` | 每 5s 震退周围敌人 | Trigger: KingAura |
| 盾墙线 | `enableShieldWall` | 友军远程减免 20% | Bool: ShieldWall |
| 嘲讽光环 | `enableTaunt` | 吸引敌方优先攻击 | Bool: Taunt |
| 死亡爆炸 | `enableDeathExplosion` | 死亡范围伤害 | Trigger: DeathExplosion |
| 护盾吸收 | `enableShieldAbsorb` | 吸收伤害护盾 | 无 |
| 减速光环 | `enableSlowAura` | 周围敌人移速降低 | 无 |
| 攻击眩晕 | `enableStunOnHit` | 命中眩晕目标 | Trigger: StunHit |
| 撕裂 | `enableTear` | 叠加易伤（每层 5%） | 无 |
| 出场震波 | `enableShockwave` | 出场震退周围敌人 | Trigger: Shockwave |
| 死亡燃烧 | `enableBurnOnDeath` | 死亡留下火海 | Trigger: Burn |
| 溅射攻击 | `enableSplash` | 攻击范围伤害 | Trigger: Splash |
| 骑兵追击 | `enableCavalryChase` | 优先锁定远程敌人 | 无 |
| 召唤师 | `enableSummoner` | 定时/击杀召唤 | Trigger: Summon |

---

## 四、待实现功能

| 功能 | 说明 |
|:-----|:-----|
| 商店系统 | 主菜单按钮已预留 |
| 图鉴/索引系统 | 主菜单按钮已预留 |
| BuildingAI 路线压力检测 | `CountEnemiesOn()` 返回 0 |
| 联机出牌/兵种/经济同步 | 网络层已实现，游戏逻辑同步未实现 |
| 教程关卡 | 未实现 |
