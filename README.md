# 即时斗地主塔防 (DoudizhuTower)

一款 2V1 即时对冲塔防游戏，基于扑克牌型与即时经济驱动的即时战略乱斗。

## 项目信息

- **引擎**: Unity 2023.2.20f1c1
- **语言**: C#
- **分辨率**: 1920 x 1080
- **架构文档**: `ARCHITECTURE.md`（v7.4）

---

## 快速开始

### 1. 打开项目

用 Unity 2023.2 打开项目文件夹。

### 2. 联机配置

项目使用 Photon PUN 2 进行联机。需要自行注册 App ID：

1. 复制 `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset.example`
2. 重命名为 `PhotonServerSettings.asset`
3. 将 `AppIdRealtime` 替换为你的 Photon App ID

### 3. 场景列表

| 场景 | 路径 | 说明 |
|---|---|---|
| MainMenu | `Assets/Scenes/MainMenu.unity` | 主菜单 |
| LevelSelect | `Assets/Scenes/LevelSelect.unity` | 关卡选择 |
| Level_1 ~ Level_5 | `Assets/Scenes/Level_*.unity` | 5 个关卡场景 |
| Bidding | `Assets/Scenes/Bidding.unity` | 叫分期 |
| OnlineLobby | `Assets/Scenes/OnlineLobby.unity` | 联机大厅 |
| DoudizhuTower_Game | `Assets/Scenes/DoudizhuTower_Game.unity` | 游戏主场景 |
| UI_Scene | `Assets/Scenes/UI_Scene.unity` | UI 场景（自动加载） |

### 4. 游戏流程

```
主菜单
├── 单人模式 → 关卡选择（5 关）→ 叫分场景 → 游戏场景
├── 对战模式 → 联机大厅 → 叫分场景 → 游戏场景
├── 图鉴（已实现，6 分类/搜索/解锁/进度）
└── 设置
```

---

## 核心系统

### 战斗系统

- **Combat Gate System**: `IsValidCombatTarget()` 统一门禁，所有战斗判断通过单一入口
- **AttackTimeline**: 时间驱动攻击帧，替代 Animation Event（确定性 + 联机安全）
- **兵种类**: `CardUnit`（4 个 partial 文件：Core/Combat/Movement/Animation）
- **被动技能**: `UnitPassives`（16 种通用被动，Inspector 勋选启用）
- **对象池**: `UnitFactory` + `VFXManager`
- **伤害结算**: `DamageQueue`（帧末批量结算，消除 Update 执行顺序影响）
- **高度系统**: Ground/Air 双高度，支持运行时覆盖

### BOSS 系统

- **激活控制**: `BossController` 管理生命周期（OnStart/OnTimer/OnBuildingDestroyed）
- **未激活隐藏**: Renderer/Collider/HealthBar/BossSkillSystem 全部禁用
- **门禁查询**: `BossController.IsActive` 供 `GetEnemiesFor` 过滤
- **技能系统**: `BossSkillSystem`（6 种效果 + 门禁过滤 + 特效缩放）

### 领域系统

- **要不起领域**: 地主开启，封印对方手牌中不能管上当前牌型的牌
- **反制护盾**: 农民反击，封印地主手牌
- **炸弹破封**: 炸弹/王炸可直接破封领域

### 经济系统

- 初始金币: 50
- 回金速度: 农民 5/秒，地主 7/秒，每分钟 +1/秒
- 费用公式: `C_n = 10 × 1.17^(n-3) × M_type`

### 联机系统

- **Master 权威架构**: Master 计算，Client 表现
- **Event + Snapshot + Tick 三层模型**: 确定性同步
- **真相源收敛**: Deck/Hand/Economy/Buff/Stun/Knockback/HP/Target/Position 9 个系统
- **Client 战斗表现层**: UNIT_ATTACK/UNIT_HIT/UNIT_STUN/UNIT_KNOCKBACK 事件驱动
- **音效双轨**: Master 本地事件 + Client 网络事件
- **断线转 AI**: 保留断线玩家实际金币和剩余手牌

---

## 目录结构

```
Assets/
├── Scripts/
│   ├── Config/              # ScriptableObject 配置表
│   ├── Core/                # 纯逻辑层（零 Unity 依赖）
│   │   ├── Battle/          # SoldierStats, HeroType
│   │   ├── Card/            # Card, CardDeck, CardHand, CardTypeDetector
│   │   └── Economy/         # EconomySystem, CardCostCalculator
│   ├── Gameplay/            # 运行时管理层
│   │   ├── Battle/          # BattleManager, BossController, DomainSystem, SpawnPool
│   │   ├── Entities/        # CardUnit, UnitPassives, Projectile, BossSkillSystem
│   │   ├── Network/         # NetworkGameManager, PhotonService
│   │   └── Systems/         # GameBootstrapper, GameStateMachine, SaveSystem
│   └── UI/                  # 界面层
│       ├── Battlefield/     # DomainUIController, LaunchTubeUI, TempSlotUI
│       ├── Codex/           # CodexUIController（图鉴系统）
│       ├── Hand/            # HandArea, CardWidget
│       └── ...
├── Scenes/                  # 场景文件
├── Resources/               # ScriptableObject 资产
└── Prefabs/                 # 预制体
```

---

## 关键脚本速查

| 脚本 | 职责 |
|---|---|
| `BattleManager` | 战场主循环 + `IsValidCombatTarget` 统一门禁 + 胜负判定 |
| `CardUnit` | 兵种基类（属性/战斗/移动/动画） |
| `CardUnit.Combat` | AttackTimeline + ExecuteHit + OnAttackHitFrame |
| `BossController` | BOSS 生命周期 + IsActive 门禁 |
| `BossSkillSystem` | BOSS 技能（门禁过滤 + 特效缩放） |
| `NetworkGameManager` | 联机游戏管理器（Master 权威同步） |
| `GameBootstrapper` | 自底向上初始化管线 |
| `UnitPassives` | 16 种通用被动技能 |
| `DomainSystem` | 要不起领域 + 反制护盾状态机 |

---

## 架构原则

1. **战斗系统 = 唯一真相**: `IsValidCombatTarget()` 是所有战斗判断的唯一入口
2. **攻击帧 = 时间驱动**: AttackTimeline 替代 Animation Event（确定性）
3. **控制效果 = 唯一打断源**: 眩晕/死亡/强制控制可打断攻击，目标死亡不打断
4. **BOSS 未激活 = 不存在**: Renderer/Collider/HealthBar/BossSkillSystem 全部禁用
5. **门禁统一**: TryAttack/OnAttackHitFrame/BossSkillSystem/GetEnemiesFor 均经过门禁

---

## 已知待实现功能

| 功能 | 状态 |
|---|---|
| 攻击特效 (P1) | 待实现 |
| 技能特效 (P1) | 待实现 |
| 商店系统 | 按钮已预留，逻辑未实现 |
| 图鉴内容化 (P2) | 待实现（V1 框架已完成） |
| 新 Boss/新牌型/新建筑/新关卡 (P3) | 待实现 |

---

## 最近更新

### 2026-06-17

- **Combat Gate System**: `IsValidCombatTarget()` 统一门禁，三层架构（门禁层/状态机/控制层）
- **AttackTimeline**: 时间驱动攻击帧替代 Animation Event + HitFrameCoroutine，确定性 + 联机安全
- **BOSS 未激活修复**: Awake 禁用所有组件，ActivateBoss 恢复，IsActive 供门禁查询
- **金币系统修复**: Master 槽位注册到 `_slotEconomies`，解决 Snapshot 金币为 0
- **远程攻击频率修复**: 非 Invulnerable 路径添加 `_animDone` 检查
- **嘲讽延迟切换**: `_pendingTauntTarget` 不中断攻击动画
- **溅射目标快照**: `_attackSnapshotTargets` 攻击前锁定
- **传送手牌修复**: 发送方 UI 刷新
- **农民 LaneArea**: 联机模式农民隐藏分路选择
- **UnitHealthBar**: 死亡帧闪烁修复
- **Debug 清理**: 删除 ~50 条冗余日志
- **ARCHITECTURE.md v7.4**
