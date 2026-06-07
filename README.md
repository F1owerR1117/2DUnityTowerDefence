# 即时斗地主塔防 (DoudizhuTower)

一款 2V1 即时对冲塔防游戏，基于扑克牌型与即时经济驱动的即时战略乱斗。

## 项目信息

- **引擎**: Unity 2023.2.20f1c1
- **语言**: C#
- **分辨率**: 1920 x 1080
- **架构文档**: `ARCHITECTURE.md`
---

## 快速开始

### 1. 打开项目

用 Unity 2023.2 打开项目文件夹。

### 2. 场景列表

| 场景 | 路径 | 说明 |
|---|---|---|
| MainMenu | `Assets/Scenes/MainMenu.unity` | 主菜单 |
| LevelSelect | `Assets/Scenes/LevelSelect.unity` | 关卡选择 |
| Bidding | `Assets/Scenes/Bidding.unity` | 叫分期 |
| OnlineLobby | `Assets/Scenes/OnlineLobby.unity` | 联机大厅 |
| DoudizhuTower_Game | `Assets/Scenes/DoudizhuTower_Game.unity` | 游戏主场景 |
| UI_Scene | `Assets/Scenes/UI_Scene.unity` | UI 场景（自动加载） |

### 3. Build Settings 注册顺序

```
0: MainMenu
1: LevelSelect
2: Bidding
3: OnlineLobby
4: DoudizhuTower_Game
```

### 4. 游戏流程

```
主菜单
├── 单人模式 → 关卡选择 → 游戏场景
├── 对战模式 → 联机大厅 → 叫分场景 → 游戏场景
├── 商店（未实现）
├── 图鉴（未实现）
└── 设置
```

---

## 核心系统

### 战斗系统

- **兵种类**: `CardUnit`（4 个 partial 文件：Core/Combat/Movement/Animation）
- **被动技能**: `UnitPassives`（16 种通用被动，Inspector 勾选启用）
- **对象池**: `UnitFactory` + `VFXManager`
- **伤害结算**: `DamageQueue`（帧末批量结算，消除 Update 执行顺序影响）
- **高度系统**: Ground/Air 双高度，`CanAttackHeight` / `CanBlockHeight` 位运算判定

### 领域系统

- **要不起领域**: 地主开启，封印对方手牌中不能管上当前牌型的牌
- **反制护盾**: 农民反击，封印地主手牌
- **炸弹破封**: 炸弹/王炸可直接破封领域，不需要点反击按钮
- **出牌校验**: `PlayValidator` 拒绝不能管上领域的牌

### 经济系统

- 初始金币: 50
- 回金速度: 农民 5/秒，地主 7/秒，每分钟 +1/秒
- 费用公式: `C_n = 10 × 1.17^(n-3) × M_type`

### 计时系统

- 对局阶段: 300 秒（可配）
- 骤死期: 60 秒（可配）
- `GameStateMachine` 自动推进阶段，`OnTimeUp` 触发结算

### 存档系统

- 基于 `PlayerPrefs`
- 存储: 金币、首次胜利、对局统计
- `SaveSystem.Load()` 启动时读取，`SaveSystem.OnGameEnded()` 结束时写盘

### 叫分系统

- 标准斗地主叫分（1/2/3 分或不叫）
- 30 秒倒计时（`BiddingConfig` 可配）
- AI 叫分策略（权重可配）
- 结果写入 `GameSession`，跨场景传递

### 联机系统（Phase 2）

- 网络抽象层: `INetworkService` 接口
- Photon 实现: `PhotonService`
- 管理器: `NetworkManager`（单例，DontDestroyOnLoad）
- 房间创建/加入/匹配/准备/开始
- 断线自动重连: 超时 30 秒 + 应用失焦/暂停恢复时自动重连，支持房间恢复

---

## 目录结构

```
Assets/
├── Animations/          # 动画控制器
├── AnotherPhoto/        # 第三方美术资源
├── Config/              # ScriptableObject 配置表
│   ├── BiddingConfig.cs     # 叫分配置
│   ├── EconomyConfig.cs     # 经济配置
│   └── HeroConfig.cs        # 英雄配置
├── Editor/              # 编辑器工具
│   ├── UnitPassivesGizmosOverlay.cs   # 被动范围可视化
│   ├── UnitPassivesEditorWindow.cs    # 被动技能调试窗口
│   ├── AudioClipTrimmer.cs            # 音频剪辑工具
│   └── ...
├── Prefabs/             # 预制体
│   ├── Army/ArmyPrefabs/      # 兵种预制体
│   ├── Buildings/             # 建筑预制体
│   ├── BulletAndHitEffect/    # 投射物和特效
│   └── UI/UIPrefabs/          # UI 预制体
├── Resources/Config/    # ScriptableObject 资产
├── Scenes/              # 场景文件
├── Scripts/             # C# 源码
│   ├── Config/              # 配置表脚本
│   ├── Core/                # 纯逻辑层（零 Unity 依赖）
│   │   ├── Battle/          # SoldierStats, HeroType
│   │   ├── Card/            # Card, CardDeck, CardHand, CardTypeDetector
│   │   └── Economy/         # EconomySystem, CardCostCalculator
│   ├── Gameplay/            # 运行时管理层
│   │   ├── Battle/          # BattleManager, DomainSystem, SpawnPool
│   │   ├── Entities/        # CardUnit, UnitPassives, Projectile, UnitVFX
│   │   ├── Network/         # INetworkService, PhotonService, NetworkManager
│   │   └── Systems/         # GameBootstrapper, GameStateMachine, SaveSystem
│   └── UI/                  # 界面层
│       ├── Battlefield/     # DomainUIController, LaunchTubeUI
│       ├── Floating/        # DamageFloatText
│       ├── HUD/             # GameTimerUI, CardCounterUI
│       ├── Hand/            # HandArea, CardWidget, SelectionValidator
│       ├── LevelSelect/     # LevelSelectController, LevelCard
│       ├── Online/          # OnlineLobbyController
│       ├── Panels/          # VictoryPanel, PauseMenu, UnitInfoPanel
│       └── ...
└── Shaders/             # 自定义着色器
```

---

## 关键脚本速查

| 脚本 | 职责 |
|---|---|
| `GameBootstrapper` | 自底向上初始化管线（12 步） |
| `BattleManager` | 战场主循环 + 牌型生成 + 胜负判定 |
| `CardUnit` | 兵种基类（属性/战斗/移动/动画） |
| `UnitPassives` | 16 种通用被动技能 |
| `DomainSystem` | 要不起领域 + 反制护盾状态机 |
| `DomainUIController` | 领域 UI 统一控制器 |
| `GameStateMachine` | 游戏阶段 + 自动计时 |
| `GameSession` | 跨场景会话数据（叫分结果/基地映射） |
| `SaveSystem` | PlayerPrefs 存档 |
| `SceneLoader` | 场景切换 API |
| `BiddingManager` | 叫分期控制器 |
| `LevelSelectController` | 关卡选择轮播 |
| `OnlineLobbyController` | 联机大厅 UI |

---

## Editor 工具

| 工具 | 菜单 | 说明 |
|---|---|---|
| 被动范围叠加 | `Tools > 技能可视化 > 范围叠加` | Scene View 实线逻辑范围 + 虚线 VFX 覆盖 |
| 被动技能调试窗口 | `Tools > 技能可视化 > 被动技能调试窗口` | 编辑/运行时调整被动参数 |
| 音频剪辑工具 | `Tools > 音频剪辑工具` | 波形可视化 + 裁剪 + 试听 |
| Animator 生成 | `Tools > 创建兵种 Animator Controller` | 自动生成 12 状态动画控制器 |
| 字体替换 | `Tools > 替换 All TMP Fonts` | 批量替换场景中 TMP 字体 |

---

## 添加新关卡

1. `Assets/Config/` 右键 → `Create` → `DoudizhuTower` → `LevelConfig`
2. 填写 `levelName`、`description`、`difficulty`、`sceneName`、`sortOrder`
3. 选中 LevelSelect 场景中的 `LevelSelectController` → 把新资产拖入 `Level Configs` 数组

---

## 添加新被动技能

1. 在 `UnitPassives.cs` 中添加 `[Header]` + `bool enableXxx` + 参数字段
2. 在 `Awake()` / `ResubscribeEvents()` 中添加事件订阅
3. 实现被动逻辑方法
4. 在 `OnDrawGizmos()` 中添加 Gizmos 绘制
5. 在 `UnitPassivesEditorWindow.cs` 中添加对应的滑条
6. 在 `UnitPassivesGizmosOverlay.cs` 中添加范围叠加显示

---

## 已知待实现功能

| 功能 | 状态 |
|---|---|
| 商店系统 | 按钮已预留，逻辑未实现 |
| 图鉴/索引系统 | 按钮已预留，逻辑未实现 |
| BuildingAI 路线压力检测 | `CountEnemiesOn()` 返回 0 |
| 联机完整同步 | Phase 2 代码完成，未测试 |
| 教程关卡（12 关） | 未实现 |

---

## 常见问题

### Canvas 缩放问题

确保所有 Canvas 的 `Canvas Scaler` 设置为：
- UI Scale Mode: `Scale With Screen Size`
- Reference Resolution: `1920 x 1080`
- Screen Match Mode: `Match Width Or Height`
- Match: `0.5`

### EventSystem 重复

场景中只能有一个 EventSystem。删除多余的，保留 UI_Scene 中的那个。

### Photon 连接失败

确保 VPN 全局模式开启（Photon 使用 UDP，不走 HTTP 代理）。或注册中国区 App ID。

### Photon 断线重连

项目已内置断线自动重连机制：
- 断线超时设为 30 秒（默认约 10 秒）
- 应用失焦/暂停恢复时自动重连（`ReconnectAndRejoin` 恢复房间，`ConnectUsingSettings` 恢复大厅）
- 如果仍然频繁断线，检查 VPN 稳定性或增大 `DisconnectTimeout`
