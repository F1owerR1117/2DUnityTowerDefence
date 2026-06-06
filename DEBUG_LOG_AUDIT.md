# Debug Log 审计报告

**总计: 143 条** (106 Log / 31 Warning / 16 Error)

---

## 1. DomainSystem.cs — 41 条 (33 Log / 7 Warning / 1 Error)

| Line | Level | Message |
|------|-------|---------|
| 318 | Log | `[DomainSystem] SetDomainPending 被调用` |
| 323 | Warning | `[DomainSystem] 反制护盾生效中，无法开启领域` |
| 330 | Warning | `[DomainSystem] 要不起领域冷却中，剩余 Xs` |
| 338 | Log | `[DomainSystem] 要不起领域待激活，出牌后生效` |
| 348 | Log | `[DomainSystem] 取消领域待激活状态` |
| 358 | Log | `[DomainSystem] SetCounterPending 被调用` |
| 363 | Warning | `[DomainSystem] 反制护盾冷却中，剩余 Xs` |
| 370 | Warning | `[DomainSystem] 当前没有要不起领域，无法反制` |
| 378 | Log | `[DomainSystem] 反制护盾待激活，出牌后生效` |
| 387 | Log | `[DomainSystem] CancelPending 被调用` |
| 388 | Log | `[DomainSystem] 调用堆栈` |
| 421 | Log | `[DomainSystem] OnCardPlayed 被调用` |
| 422 | Log | `[DomainSystem] 当前状态` |
| 433 | Log | `[DomainSystem] 出牌者身份不匹配领域发起者` |
| 441 | Warning | `[DomainSystem] 单张牌型无法开启要不起领域，保持待激活状态` |
| 449 | Log | `[DomainSystem] 激活领域` |
| 466 | Log | `[DomainSystem] 出牌者身份不匹配反制发起者` |
| 477 | Warning | `[DomainSystem] 牌型无法管上当前领域，反制失败` |
| 501 | Log | `[DomainSystem] 要不起领域持续时间修改为 Xs` |
| 511 | Log | `[DomainSystem] 要不起领域冷却时间修改为 Xs` |
| 521 | Log | `[DomainSystem] 反制护盾持续时间修改为 Xs` |
| 531 | Log | `[DomainSystem] 反制护盾冷却时间修改为 Xs` |
| 540 | Log | `[DomainSystem] 要不起领域冷却已重置` |
| 549 | Log | `[DomainSystem] 反制护盾冷却已重置` |
| 559 | Log | `[DomainSystem] 所有冷却已重置` |
| 632 | Log | `[DomainSystem] ActivateDomainInternal 被调用` |
| 646 | Log | `[DomainSystem] 领域激活成功` |
| 665 | Log | `[DomainSystem] 准备封印所有 AI 农民手牌` |
| 694 | Log | `[DomainSystem] 要不起领域开启` |
| 739 | Log | `[DomainSystem] 反制护盾开启` |
| 770 | Log | `[DomainSystem] 要不起领域关闭` |
| 794 | Log | `[DomainSystem] 反制护盾关闭` |
| 824 | Log | `[DomainSystem] 封印完成` |
| 861 | **Error** | `[DomainSystem] UpdateCardHandSealState: cardHand 为 null！` |
| 868 | Log | `[DomainSystem] ===== 开始封印 AI 手牌 =====` |
| 869 | Log | `[DomainSystem] 封印规则: sealType, 总牌数, 未封印牌数` |
| 870 | Log | `[DomainSystem] cardHand 实例ID` |
| 877 | Log | `[DomainSystem] ❌ 封印牌` |
| 879 | Log | `[DomainSystem] ✅ 未封印牌` |
| 882 | Log | `[DomainSystem] ===== AI 封印完成 =====` |

---

## 2. GameBootstrapper.cs — 35 条 (30 Log / 4 Warning / 1 Error)

| Line | Level | Message |
|------|-------|---------|
| 67 | Log | `[Bootstrapper] === 工业级自动装配管线启动 ===` |
| 83 | Log | `[Bootstrapper] 牌堆种子, 初始金币` |
| 111 | Log | `[Bootstrapper] 玩家阵营, 回金, 手牌上限` |
| 144 | Log | `[Bootstrapper] BOSS 控制器注入` |
| 154 | Log | `[Bootstrapper] AI 注入` |
| 160 | **Error** | `[Bootstrapper] handArea 未在 Inspector 中赋值！` |
| 179 | Log | `[出牌] 开始处理` |
| 182 | Log | `[出牌] 牌型, 费用` |
| 186 | Warning | `[Bootstrapper] 金币不足！` |
| 200 | Log | `[出牌] 触发领域系统前` |
| 202 | Log | `[出牌] 触发领域系统后` |
| 206 | Warning | `[出牌] domainSystem 为 null，无法触发领域系统` |
| 212 | Log | `[Bootstrapper] 出牌成功` |
| 216 | Log | `[Bootstrapper] OnPlayRequest 事件已注册` |
| 235 | Log | `[摸牌-自动] 牌堆剩余` |
| 240 | Log | `手牌已满` |
| 241 | Log | `金币不足` |
| 248 | Log | `[摸牌-手动] 抽出` |
| 269 | Log | `[Bootstrapper] 暂停菜单已就绪` |
| 273 | Log | `[Bootstrapper] Step 10` |
| 277 | Log | `[Bootstrapper] domainSystem.Initialize 完成` |
| 286 | Log | `[Bootstrapper] AI 手牌` |
| 298 | Log | `[Bootstrapper] DomainSystem.SetCardHands` |
| 308 | Log | `[DomainButton] 点击` |
| 315 | Log | `[DomainButton] 取消领域待激活` |
| 325 | Log | `[DomainButton] 标记领域待激活` |
| 329 | Warning | `[DomainButton] 无法标记领域待激活` |
| 339 | Log | `[Bootstrapper] DomainOverlay 已初始化` |
| 343 | Warning | `[Bootstrapper] DomainOverlay 未在 Inspector 中赋值` |
| 351 | Log | `[Bootstrapper] DomainCoolDownUI 已初始化` |
| 354 | Log | `[Bootstrapper] 领域系统已初始化` |
| 357 | Log | `[Bootstrapper] === 装配完成 ===` |

---

## 3. BattleManager.cs — 23 条 (14 Log / 5 Warning / 4 Error)

| Line | Level | Message |
|------|-------|---------|
| 146 | Warning | `[BattleManager] 同时设置了 _isBuilding 和 _isBoss` |
| 172 | Log | `[BattleManager] 初始化完成` |
| 205 | Log | `[BattleManager] BOSS 已激活` |
| 220 | Warning | `[BattleManager] BOSS 缺少 BuildingAI 组件` |
| 225 | Warning | `[BattleManager] BOSS 缺少 RouteGroup 组件` |
| 229 | Log | `[BattleManager] BOSS 召唤师已注册` |
| 257 | Log | `[BattleManager] BOSS 已击败 + 敌方建筑已摧毁！胜利！` |
| 262 | Log | `[BattleManager] BOSS 已击败 + 所有敌方建筑摧毁！胜利！` |
| 300 | Warning | `[BattleManager] 尝试部署无效牌型` |
| 318 | **Error** | `[BattleManager] 未识别的牌型` |
| 531 | **Error** | `[BattleManager] 未设置英雄预制体` |
| 545 | Log | `[SpawnHero] 使用 HeroConfig 属性` |
| 551 | Warning | `[SpawnHero] HeroConfig 未配置，使用硬编码` |
| 577 | Log | `[SpawnHero] 使用预制体属性 + 觉醒倍率` |
| 581 | Log | `[SpawnHero] 使用预制体属性` |
| 721 | **Error** | `[BattleManager] 未找到 Rank 的预制体映射` |
| 860 | Log | `[BattleManager] 友方建筑被摧毁！` |
| 864 | Log | `[BattleManager] 敌方建筑被摧毁！` |
| 869 | Log | `[BattleManager] 敌方 BOSS 仍然存活，无法胜利！` |
| 874 | Log | `[BattleManager] 胜利！` |
| 876 | Log | `[BattleManager] 所有敌方建筑已被摧毁！胜利！` |

---

## 4. BuildingAI.cs — 13 条 (12 Log / 0 Warning / 1 Error)

| Line | Level | Message |
|------|-------|---------|
| 58 | Log | `[BuildingAI] 初始化完成` |
| 59 | Log | `[BuildingAI] FactionTag` |
| 68 | **Error** | `[BuildingAI] 需要 BaseController 或 CardUnit 组件` |
| 113 | Log | `[BuildingAI] 领域决策` |
| 121 | Log | `[AI] 地主标记要不起领域待激活` |
| 131 | Log | `[AI] 农民标记反击待激活` |
| 139 | Log | `[BuildingAI] 检查封印状态` |
| 147 | Log | `[BuildingAI] 检查牌` |
| 156 | Log | `[BuildingAI] 所有手牌被封印，无法出牌` |
| 169 | Log | `[BuildingAI] 跳过被封印的牌` |
| 172 | Log | `[BuildingAI] 可用牌数` |
| 213 | Log | `[AI] 地主无非单张可出，取消领域待激活` |

---

## 5. CardUnit.Combat.cs — 12 条 (5 Log / 3 Warning / 4 Error)

| Line | Level | Message |
|------|-------|---------|
| 24 | Log | `[索敌] 目标更新` |
| 122 | **Error** | `[严重] 自身出现在敌方列表中！` |
| 166 | Warning | `[自射拦截] 尝试攻击自身！` |
| 171 | **Error** | `[友军攻击拦截] 尝试攻击友军` |
| 209 | Log | `[弹丸生成]` |
| 218 | **Error** | `[友军误伤拦截] 尝试攻击友军` |
| 226 | Warning | `[弹丸位置重叠] 可能出生即命中` |
| 239 | Warning | `[弹丸无目标]` |
| 272 | Log | `[伤害]` |
| 334 | Log | `[扣血]` |
| 390 | Log | `[死亡]` |

---

## 6. Projectile.cs — 8 条 (7 Log / 0 Warning / 1 Error)

| Line | Level | Message |
|------|-------|---------|
| 72 | **Error** | `[弹丸自射拦截] target 设为 null` |
| 76 | Log | `[弹丸发射]` |
| 100 | Log | `[弹丸首帧]` |
| 112 | Log | `[弹丸位置]` |
| 227 | Log | `[弹丸命中]` |
| 263 | Log | `[爆炸]` |
| 275 | Log | `[爆炸溅射]` |
| 280 | Log | `[爆炸跳过]` |

---

## 7. DomainOverlay.cs — 8 条 (4 Log / 3 Warning / 1 Error)

| Line | Level | Message |
|------|-------|---------|
| 41 | Log | `[DomainOverlay] 初始化` |
| 50 | Log | `[DomainOverlay] 事件订阅成功` |
| 54 | **Error** | `[DomainOverlay] DomainSystem 为 null` |
| 182 | Log | `[DomainOverlay] ShowOverlay` |
| 192 | Warning | `[DomainOverlay] overlayGroup 为 null` |
| 198 | Log | `[DomainOverlay] statusText 已设置` |
| 202 | Warning | `[DomainOverlay] statusText 为 null` |

---

## 8. HandArea.cs — 6 条 (6 Log / 0 Warning / 0 Error)

| Line | Level | Message |
|------|-------|---------|
| 192 | Log | `[HandArea] 选中丢失` |
| 196 | Log | `[HandArea] 刷新后校验` |
| 283 | Log | `[回收] 3换1 成功！` |
| 321 | Log | `[部署] 按钮点击` |
| 328 | Log | `[部署] 牌型, 路线` |
| 334 | Log | `[部署] OnPlayRequest 已触发` |

---

## 9. CardCounterUI.cs — 5 条 (0 Log / 5 Warning / 0 Error)

| Line | Level | Message |
|------|-------|---------|
| 50 | Warning | `[CardCounter] _deck 为空` |
| 51 | Warning | `[CardCounter] cells 数组为空` |
| 85 | Warning | `[CardCounter] deckRemainingLabel 为空` |
| 98 | Warning | `[CardCounter] countText 未赋值` |
| 100 | Warning | `[CardCounter] cells 数组中重复！` |

---

## 10. UnitSelector.cs — 4 条 (4 Log / 0 Warning / 0 Error)

| Line | Level | Message |
|------|-------|---------|
| 43 | Log | `[Selector] 点击未检测到碰撞箱` |
| 50 | Log | `[Selector] 碰撞箱` |
| 55 | Log | `[Selector] 找到 CardUnit` |
| 64 | Log | `[Selector] 找到 Installation` |

---

## 11. BossController.cs — 3 条 (1 Log / 1 Warning / 1 Error)

| Line | Level | Message |
|------|-------|---------|
| 63 | **Error** | `[BossController] 缺少 CardUnit 组件` |
| 92 | Warning | `[BossController] _triggerBuilding 为空` |
| 111 | Log | `[BossController] BOSS 已激活` |

---

## 12. CardUnit.cs — 3 条 (2 Log / 0 Warning / 1 Error)

| Line | Level | Message |
|------|-------|---------|
| 428 | **Error** | `[严重] SetEnemyUnits 列表包含自身！` |
| 430 | Log | `[敌方列表] 收到敌方` |

---

## 13. EconomyManager.cs — 2 条 (1 Log / 0 Warning / 1 Error)

| Line | Level | Message |
|------|-------|---------|
| 64 | **Error** | `[经济系统恶性Bug严重警告] 金币不减反增！` |
| 69 | Log | `[正常扣费]` |

---

## 14. PauseMenu.cs — 2 条 (2 Log / 0 Warning / 0 Error)

| Line | Level | Message |
|------|-------|---------|
| 39 | Log | `[PauseMenu] pausePanel` |
| 65 | Log | `[PauseMenu] ESC 按下` |

---

## 15. GameStateMachine.cs — 1 条 (1 Log / 0 Warning / 0 Error)

| Line | Level | Message |
|------|-------|---------|
| 50 | Log | `[GameStateMachine] 相位切换` |

---

## 16. UnitPassives.cs — 1 条 (1 Log / 0 Warning / 0 Error)

| Line | Level | Message |
|------|-------|---------|
| 282 | Log | `[被动诊断]` |

---

## 17. CardUnit.Movement.cs — 1 条 (0 Log / 1 Warning / 0 Error)

| Line | Level | Message |
|------|-------|---------|
| 220 | Warning | `[PathDiag] gap 路径诊断` |

---

## 18. FloatingTextPool.cs — 1 条 (0 Log / 1 Warning / 0 Error)

| Line | Level | Message |
|------|-------|---------|
| 54 | Warning | `[FloatingTextPool] 未找到 BattleManager` |

---

## 19. SimpleAnimator.cs — 1 条 (0 Log / 0 Warning / 1 Error)

| Line | Level | Message |
|------|-------|---------|
| 64 | **Error** | `[SimpleAnimator] baseController 为 null！` |

---

## 20. UnitFactory.cs — 1 条 (0 Log / 0 Warning / 1 Error)

| Line | Level | Message |
|------|-------|---------|
| 30 | **Error** | `[UnitFactory] 预制体为空` |

---

## 汇总

| 文件 | Log | Warning | Error | 合计 |
|------|-----|---------|-------|------|
| DomainSystem.cs | 33 | 7 | 1 | **41** |
| GameBootstrapper.cs | 30 | 4 | 1 | **35** |
| BattleManager.cs | 14 | 5 | 4 | **23** |
| BuildingAI.cs | 12 | 0 | 1 | **13** |
| CardUnit.Combat.cs | 5 | 3 | 4 | **12** |
| Projectile.cs | 7 | 0 | 1 | **8** |
| DomainOverlay.cs | 4 | 3 | 1 | **8** |
| HandArea.cs | 6 | 0 | 0 | **6** |
| CardCounterUI.cs | 0 | 5 | 0 | **5** |
| UnitSelector.cs | 4 | 0 | 0 | **4** |
| BossController.cs | 1 | 1 | 1 | **3** |
| CardUnit.cs | 2 | 0 | 1 | **3** |
| EconomyManager.cs | 1 | 0 | 1 | **2** |
| PauseMenu.cs | 2 | 0 | 0 | **2** |
| GameStateMachine.cs | 1 | 0 | 0 | **1** |
| UnitPassives.cs | 1 | 0 | 0 | **1** |
| CardUnit.Movement.cs | 0 | 1 | 0 | **1** |
| FloatingTextPool.cs | 0 | 1 | 0 | **1** |
| SimpleAnimator.cs | 0 | 0 | 1 | **1** |
| UnitFactory.cs | 0 | 0 | 1 | **1** |
| **合计** | **106** | **31** | **16** | **143** |
