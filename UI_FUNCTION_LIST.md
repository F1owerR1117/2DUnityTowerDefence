# DoudizhuTower — 完整 UI 功能清单与实现现状

> 本文档对照 `doudizhutower_optimized.md` 列出所有 UI、按键、功能及当前实现状态。
> 最后更新：2026-05-31

---

## 一、核心游戏流程概览

```
叫分（30s）→ 对局（5min）→ 骤死期（1min）→ 结算
  ❌未实现     ✅ 已实现       ❌未实现       ❌未实现
```

---

## 二、UI 元素与功能完整清单

### 🔴 当前已实现（代码已完成 + 场景可配置）

#### 1. 手牌区（HandArea）
| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 卡牌列表 | 横向排列展示手牌 | `HandArea.cs` + `CardWidget.cs` |
| 卡牌选中 | 点击卡牌 → 上浮 + 放大，实时牌型检测 | `SelectionValidator.cs` |
| 牌型验证 | 实时显示"合规牌型：xxx"或"不合规牌型" | `HandArea.OnSelectionChanged()` |
| 部署按钮 | 合规牌型时绿色可点击 | `deployButton.interactable` |
| 路线切换按钮 | 上一条/下一条路线切换 | `prevRouteButton` / `nextRouteButton` |
| 路线指示器 | 当前路线名 + 索引（如 "1/2 TopLane"） | `routeLabel` + `routeIndicator` |
| 手牌计数 | "当前/上限" 格式 | `handCountLabel` |
| 手牌排序 | 点击按钮按点数排序 | `sortButton` → `CardHand.Sort()` |
| 3换1回收 | 拖入单张牌，累计 3 张自动抽牌 | `OnCardDiscardRequested()` |
| 回收计数器 | 显示当前累计回收数（如 "回收: 2/3"） | `discardCounterLabel` |
| 容器阴影 | 手牌区整体右下阴影 | `Shadow` 组件 |

#### 2. 基地血条（BaseHealthBar）
| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 农民甲血条 | 绿色（玩家控制农民时）/ 红色（敌方） | `BaseHealthBar.cs` |
| 农民乙血条 | 同上，阵营颜色自动判定 | `BaseHealthBar.cs` |
| 地主血条 | 红色（敌方）/ 绿色（玩家控制时） | `BaseHealthBar.cs` |
| 阵营颜色 | 每帧轮询 `PlayerIsLandlord`，自动切换 | `BaseHealthBar.Update()` |

#### 3. 金币显示（GoldDisplay）
| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 金币数额 | 显示 floor 取整值 | `GoldDisplay.cs` |
| 回金速度 | 显示 "+5.0/s" 格式 | `GoldDisplay.cs` |
| 身份差异 | 地主 7g/s，农民 5g/s | `GameBootstrapper.cs` |

#### 4. 记牌器（CardCounterUI）— §1.5
| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 点数矩阵 | 横向 15 格显示"已出/4"格式 | `CardCounterUI.cs` |
| 已断标记 | 同点数 4 张全出后变灰 + brokenIndicator | `CardCounterUI.Refresh()` |
| 牌堆剩余 | "牌堆：X 张" 显示 | `deckRemainingLabel` |

#### 5. 兵种实体
| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 精灵图 | 预制体挂 SpriteRenderer（子物体） | 预制体 |
| 头顶血条 | SpriteRenderer 缩放，从右往左缩减 | `UnitHealthBar.cs` |
| 碰撞箱 | Trigger 模式，可重叠，无物理推挤 | `CardUnit.Initialize()` |
| 动画系统 | SimpleAnimator 支持 12 种动画（3 基础 + 7 Trigger + 2 Bool） | `SimpleAnimator.cs` |
| 路径行走 | MoveTowards 精确定位，每帧重投影 | `CardUnit.MoveTowardEnemyBase()` |
| 战斗追击 | 朝敌人碰撞箱边缘 ClosestPoint 移动 | `CardUnit.MoveTowardTarget()` |
| 路径诊断 | Scene 视图显示绿/红/黄线偏差 + Console 预警 | `CardUnit.MapPathDiagnostics()` |

**动画系统详细说明：**

| 类型 | 动画 | 匹配关键词 | 控制方式 |
|:-----|:-----|:-----------|:---------|
| 基础 | Idle（待机） | idle | `UpdateAnimatorState(0)` |
| 基础 | Walk（行走） | walk | `UpdateAnimatorState(1)` |
| 基础 | Attack（攻击） | attack | `UpdateAnimatorState(2)` |
| Trigger | Charge（冲锋） | charge | `TriggerAnim("Charge")` |
| Trigger | Shockwave（震波） | shockwave | `TriggerAnim("Shockwave")` |
| Trigger | Splash（溅射） | splash | `TriggerAnim("Splash")` |
| Trigger | StunHit（眩晕命中） | stunhit | `TriggerAnim("StunHit")` |
| Trigger | KingAura（君王光环） | kingaura | `TriggerAnim("KingAura")` |
| Trigger | DeathExplosion（死亡爆炸） | deathexplosion | `TriggerAnim("DeathExplosion")` |
| Trigger | Burn（燃烧） | burn | `TriggerAnim("Burn")` |
| Bool | Taunt（嘲讽） | taunt | `SetAnimBool("Taunt", true/false)` |
| Bool | ShieldWall（盾墙） | shieldwall | `SetAnimBool("ShieldWall", true/false)` |

**动画优先级**：特效动画（Trigger/Bool）优先于基础动画，触发后立即打断当前状态。

#### 6. 兵种被动（UnitPassives）— §3.1
| 被动 | Inspector 开关 | 行为 | 触发动画 | 代码文件 |
|:-----|:--------------|:-----|:---------|:---------|
| 点杀 | `enableSniper` | 自动锁定全场血量最低敌方单位 | 无 | `UnitPassives.cs` |
| 人海连击 | `enableSwarm` | 攻击时周围 2m 每个友军追加 50% ATK | 无 | `UnitPassives.cs` |
| 冲锋一击 | `enableCharge` | 蓄力后首击 ATK×2.5，重新蓄力 6s | Trigger: Charge | `UnitPassives.cs` |
| 君王光环 | `enableKingAura` | 每 5s 震退周围 3m 敌军 1m | Trigger: KingAura | `UnitPassives.cs` |
| 盾墙线 | `enableShieldWall` | 身后 3m 友军 20% 远程减免 | Bool: ShieldWall | `UnitPassives.cs` |
| 嘲讽光环 | `enableTaunt` | 吸引周围敌方单位优先攻击自己 | Bool: Taunt | `UnitPassives.cs` |
| 死亡爆炸 | `enableDeathExplosion` | 死亡时对周围敌方造成范围伤害 | Trigger: DeathExplosion | `UnitPassives.cs` |
| 护盾吸收 | `enableShieldAbsorb` | 获得可吸收伤害的护盾值 | 无 | `UnitPassives.cs` |
| 减速光环 | `enableSlowAura` | 周围敌方单位移速降低 | 无 | `UnitPassives.cs` |
| 攻击眩晕 | `enableStunOnHit` | 攻击命中时眩晕目标 | Trigger: StunHit | `UnitPassives.cs` |
| 撕裂 | `enableTear` | 每次攻击为目标叠加易伤效果 | 无 | `UnitPassives.cs` |
| 出场震波 | `enableShockwave` | 出场时震退周围敌人并造成伤害 | Trigger: Shockwave | `UnitPassives.cs` |
| 死亡燃烧 | `enableBurnOnDeath` | 死亡时留下火海持续伤害 | Trigger: Burn | `UnitPassives.cs` |
| 溅射攻击 | `enableSplash` | 攻击时对目标周围造成范围伤害 | Trigger: Splash | `UnitPassives.cs` |
| 骑兵追远程 | `enableCavalryChase` | 优先攻击攻击距离远的敌方单位 | 无 | `UnitPassives.cs` |

#### 7. 牌型涌现行为（SpawnPool + UnitPassives）— §3.2
| 牌型 | 行为 | 实现方式 | 代码文件 |
|:-----|:-----|:---------|:---------|
| 对子 | 合击：第二兵额外 +50% 伤害 | 预制体 UnitPassives | `UnitPassives.cs` |
| 三张 | 分担：血量最高者承伤 60%，其余各 20% | BattleManager 分担逻辑 | `BattleManager.cs` |
| 三带一 | 5 种诱饵类型（屏障/护盾/减速/爆炸/帝王） | SpawnPool._baitPrefabs | `SpawnPool.cs` |
| 三带二 | 5 种骑兵类型（轻骑/重骑/弓骑/突骑/铁骑） | SpawnPool._cavalryPrefabs | `SpawnPool.cs` |
| 顺子 | 链式加速：前排慢后排快 | BattleManager 攻速/移速倍率 | `BattleManager.cs` |
| 炸弹 | 出场震波：震退 2m + 30% ATK 伤害 | SpawnPool._bombPrefabs | `SpawnPool.cs` |
| 炸弹 | 死亡燃烧：3s 火海，每秒 20% ATK | SpawnPool._bombPrefabs | `SpawnPool.cs` |
| 连对 | 撕裂：叠易伤（每层 5%，上限 5 层）+ 减速 | SpawnPool._consecutivePairPrefabs | `SpawnPool.cs` |
| 四带二 | 坦克 + 无人机组合 | SpawnPool._tankPrefabs + _dronePrefabs | `SpawnPool.cs` |
| 飞机 | 地毯式轰炸 + 5 种弹型 | SpawnPool._bomberPrefabs | `SpawnPool.cs` |

> **注意**：`CardTypePassives.cs` 已删除，所有功能已迁移至 `UnitPassives.cs`（通用被动）和 `SpawnPool.cs`（预制体映射）。

#### 8. 基地实体
| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 三个基地 | 场景中任意放置，FactionTag 决定阵营 | `BaseController.cs` + `Installation.cs` |
| 伤害换算 | `max(1, floor(ATK/10))` | `BaseController.TakeDamage()` |
| 摧毁事件 | 触发 OnDestroyed，通知 BattleManager | `Installation.cs` |
| 碰撞半径 | 自动从 Collider2D 半宽读取 | `Installation.GetWorldRadius()` |
| 阵营标签 | FactionTag 独立组件，仅供 Inspector 勾选 | `FactionTag.cs` |

#### 9. 摸牌系统（§1.6）
| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 摸牌按钮 | 消耗金币抽一张牌 | `GameBootstrapper.cs` |
| 自动摸牌 | 地主 5s/张，农民 6s/张 | `TimerQueue.ScheduleLoop()` |
| 价格差异 | 地主 10g，农民 12g | `GameBootstrapper.cs` |
| 手牌上限 | 地主 20 张，农民 17 张 | `CardHand` 容量 |
| 手牌满封锁 | 手牌满时自动暂停分发 + 封锁摸牌按钮 | `GameBootstrapper.cs` |

#### 10. 3 换 1 回收系统（§1.7）
| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 弃牌桶 | 拖入单张牌 | `CardWidget.OnCardDiscardRequested` |
| 回收计数 | "回收: X/3" 显示 | `discardCounterLabel` |
| 自动抽牌 | 累计 3 张自动从牌堆抽 1 张 | `HandArea.OnCardDiscardRequested()` |

#### 11. 路线选择（§2.1）
| 组件 | 功能 | 代码文件 |
|:-----|:-----|:---------|
| RouteGroup | 建筑绑定多路线，支持 Prev/Next 切换 | `RouteGroup.cs` |
| 路线名称 | "1/2 TopLane" 格式显示 | `RouteGroup.CurrentRouteName` |
| 单路线禁用 | 单路线时切换按钮置灰 | `HandArea.UpdateRouteDisplay()` |

#### 12. 摄像机控制
| 功能 | 操作 | 代码文件 |
|:-----|:-----|:---------|
| 键盘移动 | WASD / 方向键 | `CameraController.cs` |
| 边缘滚动 | 鼠标移至屏幕边缘 | `CameraController.cs` |
| 滚轮缩放 | 调节 orthographicSize | `CameraController.cs` |
| 边界限制 | min/max X/Y 边界 | `CameraController.cs` |

#### 13. AI 对手系统（§6.1）— BuildingAI
| 功能 | 行为 | 代码文件 |
|:----|:-----|:---------|
| 出牌频率 | 每 4 秒判定一次 | `BuildingAI.MakeDecision()` |
| 选牌策略 | 枚举所有合规牌型，选最贵且付得起的 | `BuildingAI.MakeDecision()` |
| 自动摸牌 | 地主 5s/农民 6s，独立经济系统 | `BuildingAI.Update()` |
| 经济增长 | 每分钟 +1g/s，与玩家同步 | `BuildingAI.Update()` |
| 挂载方式 | Add Component → BuildingAI（建筑挂载） | `BuildingAI.cs` |
| 选路（地主） | 基于敌方数量选择（预留完整实现） | `BuildingAI.ChooseLane()` |

---

### 🟡 阶段一扩展（代码已有，需场景配置）

#### 1. ScriptableObject 配置资产
| 资产 | 状态 |
|:-----|:-----|
| `SoldierConfig.asset` | 场景中已挂载 |
| `EconomyConfig.asset` | 场景中已挂载 |
| `BaseConfig.asset` × 2 | 农民 + 地主各一个 |
| `CardSpriteDB.asset` | 已创建，用于卡牌精灵映射 |

#### 2. 预制体
| 预制体 | 状态 |
|:-------|:-----|
| `Infantry.prefab` | 需要调整父子结构 + Animator Controller |
| `CardWidget.prefab` | 需要调整精灵图锚点 |
| 各 Rank 兵种预制体 | 需要在 SpawnPool 中拖入映射 |

---

### 🔵 待实现功能

#### 14. 叫分系统 — §1.1
| UI 元素 | 功能 |
|:--------|:-----|
| 叫分面板 | 三个大按钮：【1分】【2分】【3分】 |
| 动态置灰 | 有人点 → 按钮全场置灰 |
| 30 秒倒计时 | 超时随机指派地主 |

#### 15. 要不起领域 — §4.1-4.2
| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 领域按钮 | 地主侧方"手动要不起"按钮 | `DomainOverlay.cs`（待实现） |
| 锁链覆盖 | 农民手牌变灰 + 锁链覆盖 | `DomainSystem.cs`（已创建） |
| 高亮保留 | 能管上的炸弹/王炸保持金色闪烁 | `CardWidget.SetSealed()`（已实现） |
| 倒计时 | 5 秒领域持续时间显示 | `DomainOverlay.cs`（待实现） |

#### 16. 反制护盾 — §4.3
| UI 元素 | 功能 | 代码文件 |
|:--------|:-----|:---------|
| 反击按钮 | 农民侧方"管上/反击"按钮 | `DomainOverlay.cs`（待实现） |
| 封印效果 | 封印地主手牌 2 秒（反客为主） | `DomainSystem.cs`（已创建） |
| 共享 CD | 45 秒全局冷却 | `DomainSystem.cs`（已创建） |
| 领域震碎 | 农民反击时震碎地主领域 | `DomainSystem.ActivateCounterShield()`（已实现） |

**反制护盾新机制（反客为主）：**
- 农民反击的牌型成为新的封印标准
- 地主手牌中所有"管不上"农民反击牌型的牌被封印变灰 + 锁链覆盖，持续 2 秒
- 地主手牌中"能管上"农民反击牌型的牌保持高亮，可自由打出
- 封印规则与要不起领域逻辑对称，但反过来作用于地主

#### 17. 传送飞筒 — §4.4
| UI 元素 | 功能 |
|:--------|:-----|
| 飞筒 UI | 屏幕正中央半透明管道 |
| 拖拽区域 | 拖入单张牌 |
| 暂存槽 | 最多 1 张牌的临时槽位 |
| CD 倒计时 | 6 秒冷却显示 |

#### 18. 战斗飘字 — §3.A.3
| 类型 | 颜色 | 触发 |
|:-----|:-----|:-----|
| 普通伤害 | 白色 | 普攻命中 |
| 暴击 | 黄色 | 暴击/倍率攻击 |
| 技能伤害 | 红色 | 特殊技能 |
| 治疗 | 绿色 | 回血 |

#### 19. 点选信息面板 — §3.A.2
| 功能 | 触发 |
|:-----|:-----|
| 属性显示 | HP/ATK/攻速/移速/射程 |
| Buff/Debuff | 图标 + 剩余秒数 |

#### 20. 地主大牌型完整行为 — §3.2
| 牌型 | 行为 | 优先级 |
|:-----|:-----|:-------|
| 四带二坦克 | HP×3 + 嘲讽 + 溅射 + 无人机 | 中 |
| 三连对（机械三头犬） | 撕裂效果（连对通用） | 中 |
| 顺子 6+（铁甲连环马） | 链式加速 + 碰撞体积递增 | 中 |
| 飞机（末日轰炸机群） | 地毯式轰炸 + 5 种弹型 | 低 |
| 双 Joker 王炸 | 觉醒英雄 | 中 |

#### 21. Joker 英雄系统 — §5
| UI 元素 | 功能 |
|:--------|:-----|
| 战前英雄选择 | 5 选 1 界面（代码已有 _selectedHero） |
| 单 Joker → 英雄 | 属性 ×1.5（BattleManager 骨架完成） |
| 双 Joker 王炸 → 觉醒英雄 | 属性 ×2.0，模型变大（骨架完成） |

#### 22. 图鉴系统 — §7.5
| 分类 | 条目数 |
|:-----|:-------|
| 点数兵种 | 14 条（3~Joker） |
| 牌型组合 | 10 条 |
| 特殊单位 | 15 条 |
| BOSS 单位 | 2 条 |
| 地图机制 | 3 条 |

#### 23. 教程系统 — §7.2
| 阶段 | 关卡数 | 教学内容 |
|:-----|:-------|:---------|
| 基础篇 | 5 关 | 选牌/部署/对子/三张/顺子 |
| 进阶篇 | 4 关 | 三带一/三带二/炸弹/叫分 |
| 高级篇 | 3 关 | 四带二/连对/飞机/王炸 |

#### 24. 其他待实现
| 功能 | 说明 |
|:-----|:-----|
| 伤害类型系统 | DamageType 枚举已定义，物理/燃烧类型生效中 |
| 胜负结算 UI | WinCondition 逻辑已完成，UI 展示待实现 |
| 暴君光环 | 地主兵种属性加成（可通过预制体 Inspector 手动模拟） |
| 叫分挂钩场外结算 | 作为联机功能预留 |

---

## 三、当前场景搭建状态

```
GameSystems (GameObject)
  ├── GameBootstrapper.cs      ← 装配管线（拖入所有引用）
  ├── EconomyManager.cs        ← 经济系统
  ├── TimerQueue.cs             ← 计时器
  ├── BattleManager.cs          ← 战场主循环（含 WinCondition）
  └── CardCounterUI             ← 记牌器（Canvas 子物体）
Map
  ├── FarmerBaseA              ← BaseController + FactionTag(Farmer) + SpawnPool + RouteGroup
  ├── FarmerBaseB              ← BaseController + FactionTag(Farmer) + SpawnPool + RouteGroup
  ├── LandlordBase             ← BaseController + FactionTag(Landlord) + SpawnPool + RouteGroup
  ├── TopLane                  ← RoutePath
  └── BottomLane               ← RoutePath
Canvas
  ├── HandArea                 ← 手牌区（含部署/路线/排序/回收按钮）
  ├── GoldDisplay              ← 金币 HUD
  ├── BaseHealthBar × 3        ← 基地血条
  └── 路线按钮组               ← Prev/Next 路线切换
EntityPool                     ← UnitFactory（兵种对象池）
Camera                         ← CameraController（WASD + 边缘滚动 + 缩放）
```

---

## 四、自上次更新以来的主要变化

| 变化 | 说明 |
|:----|:-----|
| 记牌器 | CardCounterUI 已实现，不再待实现 |
| 兵种被动 | UnitPassives 已实现（15 种被动，全部支持动画触发） |
| 动画系统 | SimpleAnimator 扩展为 12 种动画（3 基础 + 7 Trigger + 2 Bool） |
| 动画优先级 | 特效动画优先于基础动画，Any State → 特效状态 → Idle |
| 大小写匹配 | 动画名称匹配改为大小写不敏感（ToLowerInvariant） |
| 牌型涌现行为 | 已迁移至 UnitPassives + SpawnPool（合击/分担/诱饵/骑兵/震波/燃烧/链式加速/撕裂） |
| AI 系统 | AIController → BuildingAI，挂载到建筑上独立运行 |
| 手牌排序 | sortButton 已实现 |
| 3换1回收 | 弃牌桶 + 自动抽牌已实现 |
| 路线系统 | RouteGroup + 路线切换按钮已实现 |
| 摄像机 | CameraController 已实现 |
| 属性系统 | CardUnit 由预制体 Inspector 字段驱动，SoldierStats 硬编码属性表已移除 |

---

## 五、测试清单

### 核心流程测试
- [x] 选牌 → 牌型检测 → 部署出兵
- [x] 自动摸牌（每 5/6 秒自动补牌）
- [x] 金币摸牌（点击按钮消耗金币）
- [x] 身份差异（地主 7g/s vs 农民 5g/s）
- [x] 上路/下路选择 + 路线标签
- [x] 路线切换（Prev/Next 按钮）
- [x] 手牌排序

### 兵种行为测试
- [x] 小兵沿路径行军（MoveTowards 精准贴线）
- [x] 小兵同路检测敌人 → 追击
- [x] 小兵跨路检测敌人 → 站桩攻击，不跨路移动
- [x] 头顶血条从右往左缩减
- [x] 兵种碰撞箱 Trigger → 可重叠无推挤
- [x] 动画系统（SimpleAnimator 12 种动画配置）
- [x] 基础动画切换（Idle/Walk/Attack）
- [x] 特效动画触发（Trigger/Bool）
- [ ] 点杀被动（自动锁定残血）
- [ ] 人海连击（周围友军追加伤害）
- [ ] 冲锋一击（蓄力首击 ×2.5）+ Charge 动画
- [ ] 君王光环（周期震退）+ KingAura 动画
- [ ] 盾墙线（友军远程减免）+ ShieldWall 动画
- [ ] 嘲讽光环（吸引敌方）+ Taunt 动画
- [ ] 死亡爆炸（范围伤害）+ DeathExplosion 动画
- [ ] 出场震波（震退敌人）+ Shockwave 动画
- [ ] 溅射攻击（范围伤害）+ Splash 动画
- [ ] 攻击眩晕（控制效果）+ StunHit 动画
- [ ] 死亡燃烧（持续伤害）+ Burn 动画

### 牌型涌现测试
- [ ] 对子合击（第二兵 +50%）
- [ ] 三张分担（承伤重分配）
- [ ] 炸弹震波（出场震退）
- [ ] 炸弹燃烧（死亡火海）
- [ ] 顺子链式加速

### AI 测试
- [x] AI 对手自动出牌 + 摸牌
- [x] AI 经济增长
- [ ] AI 地主选路

### 其他
- [x] 战斗结束路径重投影（Resnap，不掉头）
- [x] 记牌器显示
- [x] 3换1回收
- [ ] 叫分系统
- [ ] 要不起领域
- [ ] 传送飞筒
- [ ] 战斗飘字
