# 远程单位攻击频率异常审计报告

## 问题现象

远程单位攻击速度配置正常，但实际攻击频率明显偏低。

---

## 完整远程攻击链路追踪

### 攻击状态机流程

```
TryAttack()
  → _isAttacking = true
  → HitFrameCoroutine(interval) 启动
  → 播放攻击动画 (State=2)
  → 广播攻击事件

动画播放中...
  → Animation Event → OnAttackHitFrame()
    → SpawnProjectile() 生成子弹
    → _hitCountDealt++

HitFrameCoroutine 等待:
  → _hitCountDealt >= Stats.HitCount → 退出协程

OnUpdate 每帧检查:
  → _isAttacking == true
  → 检查攻击完成条件
  → 满足条件 → _isAttacking = false
```

---

## 检查 1：是否存在子弹数量限制

**`OnAttackHitFrame()` L359-364**:
```csharp
if (_isRanged && _projectilePrefab != null)
{
    if (_projectileSpawned) { _hitCountDealt++; return; }
    _projectileSpawned = true;
    float totalDmg = ComputeAndConsumePassiveDamage();
    SpawnProjectile(totalDmg);
}
```

- `_projectileSpawned` 在**单次攻击**内防止重复生成
- 在 `TryAttack()` 中重置为 `false`（L236）
- **不存在跨攻击的子弹数量限制**
- 场上可以同时存在多个子弹

**结论**：无全局子弹数量限制。✅

### 检查 2：是否存在"子弹命中前不能发射下一发"逻辑

**搜索 `_activeProjectile`/`_projectileInFlight`/`_hasProjectile`**：

在整个代码库中搜索，**未找到**任何此类字段。

`Projectile.cs` 是独立的 MonoBehaviour，不与发射者保持引用关系（`Fire()` 后发射者不追踪子弹状态）。

**结论**：不存在子弹阻塞下一次攻击的逻辑。✅

### 检查 3：是否存在 Animation Event 未触发情况

**`OnAttackHitFrame()` 触发机制**：
1. 由 Animator 的 Animation Event 调用
2. 在攻击动画剪辑的指定帧设置
3. 通过 `GetHitNormalizedTime()` 读取 Event 时间点

**`HitFrameCoroutine()` 兜底机制** (L409-424):
```csharp
// 等待 Animation Event 触发
while (_hitCountDealt < Stats.HitCount && elapsed < attackInterval)
{
    elapsed += Time.deltaTime;
    yield return null;
}
// 兜底：Animation Event 未全部触发时补足
while (_hitCountDealt < Stats.HitCount)
{
    OnAttackHitFrame();
}
```

- 即使 Animation Event 未触发，协程超时后会自动补调 `OnAttackHitFrame()`
- **不会因 Animation Event 缺失而永久卡死**

**结论**：有兜底机制，不会因 Animation Event 问题导致攻击中断。✅

### 检查 4：Projectile 是否未销毁导致攻击锁死

**Projectile 销毁条件** (`Projectile.cs`):
| 条件 | 代码行 | 说明 |
|------|--------|------|
| 目标死亡 | L90-92 | `_destroyOnTargetDeath && !_target.IsAlive` |
| 射手死亡 | L93 | `_destroyOnShooterDeath && !_shooter.IsAlive` |
| 超时 | L94 | `Time.time - _launchTime > _maxLifetime`（默认 5 秒） |
| 命中 | L270 | `Hit()` 末尾 `Destroy(gameObject)` |

- Projectile 是独立 GameObject，不持有发射者引用链
- Projectile 销毁不影响发射者的攻击状态
- **Projectile 不会阻塞攻击**

**结论**：Projectile 不会导致攻击锁死。✅

### 检查 5：攻击冷却是否依赖 Projectile Hit/Destroy

**攻击冷却机制**：**不存在显式冷却计时器**

攻击频率由以下机制控制：

1. **`_isAttacking` 标志**：阻止新攻击直到当前攻击完成
2. **`HitFrameCoroutine`**：持续 `AttackInterval` 秒
3. **`_justFinishedAttack`**：攻击结束后跳过 1 帧

**冷却不依赖 Projectile 的任何状态。**

---

## 🔴 核心问题定位

### `_isAttacking` 重置逻辑缺陷

**`OnUpdate()` 攻击状态处理** (CardUnit.cs L675-728):

```
if (_isAttacking)
{
    // 1. 目标死亡 → 中断 ✅
    // 2. 超时安全阀（3×AttackInterval）→ 中断 ✅
    
    if (!Invulnerable)  // ← 非无敌路径
    {
        // 3a. 嘲讽打断 ✅
        // 3b. 建筑目标超范围 → 中断 ✅
        // ⚠️ 无攻击完成检查！
    }
    else  // ← 无敌路径（Boss施法等）
    {
        // 4. 检查动画完成 + 命中完成 → 重置 _isAttacking ✅
        if (IsAttackAnimDone()) _animDone = true;
        if (_animDone && _hitCountDealt >= Stats.HitCount)
        {
            _isAttacking = false;  // ← 唯一正常完成路径
        }
    }
    return;
}
```

**🔴 问题：`_animDone` 仅在 `Invulnerable` 路径中设置**

对于**非无敌的远程单位**（所有普通远程兵种）：
- `HitFrameCoroutine()` 完成后，`_hitCountDealt >= Stats.HitCount`
- 但 `_animDone` 永远不会被设为 `true`
- `_isAttacking` 永远不会在正常流程中被重置为 `false`

### 攻击完成的实际路径

非无敌远程单位的 `_isAttacking` 只能通过以下方式重置：

| 触发条件 | 代码位置 | 概率 |
|----------|----------|------|
| 目标死亡 | L679-682 `InterruptAttack()` | 依赖目标死亡 |
| 超时安全阀 | L686-689 `InterruptAttack()` | **必然触发** |
| 眩晕/控制 | `InterruptAttack()` | 依赖外部效果 |

**超时安全阀触发条件**：`_attackStateTimer > Stats.AttackInterval * 3f`

---

## 攻击频率计算

### 配置值 vs 实际值

以 `AttackInterval = 0.5s` 的远程单位为例：

**配置攻击频率**：2 次/秒

**实际攻击流程**：
```
t=0.0s:  TryAttack() → _isAttacking=true, 启动 HitFrameCoroutine(0.5s)
t=0.0s:  播放攻击动画
t≈0.15s: Animation Event → OnAttackHitFrame() → SpawnProjectile() → _hitCountDealt=1
t=0.5s:  HitFrameCoroutine 完成（_hitCountDealt >= HitCount）
         ⚠️ _isAttacking 仍为 true（无完成检查）
         
t=0.5s→1.5s: OnUpdate 每帧检查
  _attackStateTimer 持续累加
  非 Invulnerable 路径无完成检查
  _isAttacking 保持 true

t=1.5s:  _attackStateTimer > 0.5×3 = 1.5s → InterruptAttack() → _isAttacking=false
t=1.5s:  _justFinishedAttack=true → 跳过 1 帧
t=1.6s:  重新索敌 → TryAttack()

总周期: 1.6s（配置 0.5s）
实际频率: 0.625 次/秒（配置 2 次/秒）
降低比例: ~69%
```

### 更长 AttackInterval 的影响

| 配置 Interval | 超时阈值 (3×) | 实际周期 | 实际频率 | 降低比例 |
|---------------|---------------|----------|----------|----------|
| 0.3s | 0.9s | ~1.0s | 1.0/s | 67% ↓ |
| 0.5s | 1.5s | ~1.6s | 0.625/s | 69% ↓ |
| 1.0s | 3.0s | ~3.1s | 0.32/s | 68% ↓ |
| 1.5s | 4.5s | ~4.6s | 0.22/s | 69% ↓ |

**降低比例恒定约 68%**，因为超时阈值固定为 `3×AttackInterval`。

---

## 风险等级

**🔴 P0 - 严重**

远程单位实际攻击频率仅为配置值的约 1/3，严重影响游戏平衡。

---

## 调用链总结

```
TryAttack() [L226]
  → _isAttacking = true [L232]
  → HitFrameCoroutine(interval) [L250]
  
HitFrameCoroutine() [L409]
  → 等待 _hitCountDealt >= Stats.HitCount
  → 兜底调用 OnAttackHitFrame()
  → 协程结束
  
OnAttackHitFrame() [L317]
  → SpawnProjectile() [L364] (远程)
  → _hitCountDealt++ [L372]
  
OnUpdate() [L662] - 每帧执行
  → _isAttacking == true [L676]
  → 非 Invulnerable 路径:
    → 嘲讽/范围检查 [L696-711]
    → ⚠️ 无 _animDone 检查
    → _attackStateTimer 累加 [L693]
  → 超时安全阀: _attackStateTimer > 3×AttackInterval [L686]
    → InterruptAttack() → _isAttacking = false
  
_total cycle = AttackInterval + 3×AttackInterval = 4×AttackInterval
实际频率 = 1/(4×AttackInterval) = 配置频率/4
```

**注意**：上面的计算假设超时恰好在 3× 时触发。实际中断发生在 `_attackStateTimer` 超过阈值的那一帧，所以总周期约等于 `AttackInterval + 3×AttackInterval = 4×AttackInterval`。但 HitFrameCoroutine 在 `AttackInterval` 时就完成了，超时安全阀在 `3×AttackInterval` 时触发，所以总延迟约 `3×AttackInterval`（协程完成后还需等待 `2×AttackInterval` 才超时）。

更精确的计算：
```
协程完成时间: AttackInterval
超时触发时间: 3×AttackInterval（从攻击开始计时）
额外等待: 3×AttackInterval - AttackInterval = 2×AttackInterval
实际周期: AttackInterval + 2×AttackInterval = 3×AttackInterval
实际频率: 1/(3×AttackInterval) = 配置频率/3
```

---

## 推荐修复方案

### 方案 A：在非 Invulnerable 路径添加完成检查（推荐）

在 `OnUpdate()` 的 `!Invulnerable` 路径中添加攻击完成检查：

```csharp
if (!Invulnerable)
{
    // 现有嘲讽/范围检查...
    
    // 新增：检查攻击是否正常完成
    if (IsAttackAnimDone()) _animDone = true;
    if (_animDone && _hitCountDealt >= Stats.HitCount)
    {
        _isAttacking = false;
        _attackTarget = null;
        _attackStateTimer = 0f;
        SetAnimSpeed(1f);
        UpdateAnimatorState(0);
        _justFinishedAttack = true;
        return;
    }
}
```

**优点**：精确匹配攻击动画和伤害完成，攻击间隔最小化
**影响范围**：所有非无敌的远程和近程单位

### 方案 B：统一完成检查到 Invulnerable 路径外

将 `_animDone` 检查从 Invulnerable 路径移到公共路径：

```csharp
if (_isAttacking)
{
    // 目标死亡/超时检查...
    
    // 公共路径：检查攻击完成
    if (!Invulnerable)
    {
        // 嘲讽/范围检查...
    }
    
    // 统一完成检查（移到 Invulnerable 外面）
    if (IsAttackAnimDone()) _animDone = true;
    if (_animDone && _hitCountDealt >= Stats.HitCount)
    {
        _isAttacking = false;
        _attackTarget = null;
        _attackStateTimer = 0f;
        SetAnimSpeed(1f);
        UpdateAnimatorState(0);
        _justFinishedAttack = true;
    }
    return;
}
```

**优点**：逻辑更清晰，所有单位共用完成检查
**风险**：需确认 Invulnerable 单位的动画行为不受影响

### 方案 C：使用 HitFrameCoroutine 回调重置

让 `HitFrameCoroutine` 在完成后直接重置 `_isAttacking`：

```csharp
private IEnumerator HitFrameCoroutine(float attackInterval)
{
    // 现有逻辑...
    while (_hitCountDealt < Stats.HitCount)
    {
        OnAttackHitFrame();
    }
    
    // 新增：协程完成后等待动画结束，然后重置
    while (!IsAttackAnimDone())
        yield return null;
    
    _isAttacking = false;
    _attackTarget = null;
    _attackStateTimer = 0f;
    SetAnimSpeed(1f);
    UpdateAnimatorState(0);
    _justFinishedAttack = true;
}
```

**优点**：攻击状态与协程生命周期绑定，逻辑自包含
**风险**：需处理协程被 StopCoroutine 中断的情况

---

## 附加发现

### `_justFinishedAttack` 跳帧机制

`OnUpdate()` L732-736：
```csharp
if (_justFinishedAttack)
{
    _justFinishedAttack = false;
    return;  // 跳过本帧
}
```

攻击完成后跳过 1 帧，让 Animator 回到 Idle 状态再重新索敌。这是合理设计，防止攻击动画残留导致立即重新攻击。

### 近程单位是否受影响

**是**。近程单位也使用相同的 `_isAttacking` 状态机，但近程单位的 `OnAttackHitFrame()` 直接调用 `DealAttackDamage()`（不生成 Projectile），攻击完成逻辑相同。

但近程单位的攻击动画通常较短（`AttackInterval` 较小），超时安全阀更快触发，影响相对较小。

### Invulnerable 路径的适用范围

仅 BossSkillSystem 中的 Boss 在施法期间设置 `Invulnerable = true`。普通远程单位（Archer、Wizard 等）不走 Invulnerable 路径，因此都受此 bug 影响。
