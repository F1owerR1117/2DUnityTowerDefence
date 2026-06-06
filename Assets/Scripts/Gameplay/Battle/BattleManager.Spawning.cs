using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DoudizhuTower.Core.Battle;
using DoudizhuTower.Core.Cards;
using DoudizhuTower.Gameplay.Entities;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Battle
{
    public partial class BattleManager
    {
        // ─── 牌型入口 ──────────────────────────────────

        public void DeployCards(Card[] cards, CardTypeResult result, RouteGroup routeGroup, Component sourceBase)
        {
            if (!result.IsValid) { Debug.LogWarning("[BattleManager] 尝试部署无效牌型"); return; }
            _cardsPlayedCount += cards.Length;
            Lane lane = Lane.None;

            switch (result.Type)
            {
                case CardType.Bomb: SpawnBomb(cards, result, lane, sourceBase); break;
                case CardType.Pair: SpawnPair(cards, lane, sourceBase); break;
                case CardType.Triple: SpawnTriple(cards, lane, sourceBase); break;
                case CardType.TripleWithOne: SpawnTripleWithOne(cards, result, lane, sourceBase); break;
                case CardType.TripleWithPair: SpawnTripleWithPair(cards, result, lane, sourceBase); break;
                case CardType.Straight:
                case CardType.Straight6Plus: SpawnStraight(cards, lane, sourceBase); break;
                case CardType.ConsecutivePair: SpawnConsecutivePair(cards, result, lane, sourceBase); break;
                case CardType.FourWithTwo: SpawnFourWithTwo(cards, result, lane, sourceBase); break;
                case CardType.Plane: SpawnPlane(cards, result, lane, sourceBase); break;
                case CardType.DoubleKingBomb: SpawnDoubleKingBomb(lane, sourceBase); break;
                case CardType.Single: SpawnSingles(cards, lane, sourceBase); break;
                default:
                    Debug.LogError($"[BattleManager] 未识别的牌型: {result.Type}，请检查 CardTypeDetector 和 DeployCards 是否同步");
                    break;
            }
        }

        // ─── 牌型生成 ──────────────────────────────────

        void SpawnBomb(Card[] cards, CardTypeResult result, Lane lane, Component sourceBase)
        {
            var pool = sourceBase?.GetComponent<SpawnPool>();
            var prefab = pool?.GetBombPrefab(result.MainRank);
            CreateUnitWithPrefab(prefab, result.MainRank, lane, sourceBase, CardType.Bomb);
        }

        void SpawnPair(Card[] cards, Lane lane, Component sourceBase)
        {
            if (cards.Length < 2) return;
            CreateUnit(cards[0].Rank, lane, sourceBase, CardType.Pair);
            var u2 = CreateUnit(cards[0].Rank, lane, sourceBase, CardType.Pair);
            // 合击：第二兵额外伤害
            if (u2 != null)
                u2.OnAttackEvent += (target) => { u2._bonusDamage += u2.Stats.ATK * _jointDamageBonus; };
        }

        void SpawnTriple(Card[] cards, Lane lane, Component sourceBase)
        {
            if (cards.Length < 3) return;
            for (int i = 0; i < 3; i++)
            {
                var u = CreateUnit(cards[0].Rank, lane, sourceBase, CardType.Triple);
                if (u != null)
                {
                    if (i > 0) u.transform.position += new Vector3(0, (i - 1) * 0.4f, 0);
                    u.OnTakeDamageEvent += (damage, type) => RedistributeDamage(u, damage, type);
                }
            }
        }

        // 防止分担伤害递归调用的标志
        private bool _isRedistributingDamage;

        private void RedistributeDamage(CardUnit damaged, float damage, DamageType type)
        {
            if (_isRedistributingDamage) return;

            var allies = new List<CardUnit>();
            Vector3 pos = damaged.transform.position;
            foreach (var unit in _allUnits)
            {
                if (unit == null || !unit.IsAlive || unit == damaged) continue;
                if (unit.IsLandlord != damaged.IsLandlord) continue;
                if (unit.SourceCardType != CardType.Triple) continue;
                if (Vector2.Distance(pos, unit.transform.position) <= _shareRange) allies.Add(unit);
            }
            if (allies.Count < 2) return;

            _isRedistributingDamage = true;
            damaged.ShareRedirected = true;
            allies.Add(damaged);
            var sorted = allies.OrderByDescending(a => a.CurrentHP).ToList();
            for (int i = 1; i < sorted.Count; i++)
                sorted[i].TakeDamage(damage * _shareOtherPct, type);
            sorted[0].TakeDamage(damage * _shareMainPct, type);
            _isRedistributingDamage = false;
        }

        void SpawnTripleWithOne(Card[] cards, CardTypeResult result, Lane lane, Component sourceBase)
        {
            for (int i = 0; i < 3; i++)
            {
                var u = CreateUnit(result.MainRank, lane, sourceBase, CardType.Triple);
                if (u != null) u.OnTakeDamageEvent += (damage, type) => RedistributeDamage(u, damage, type);
            }
            if (result.KickerRanks != null && result.KickerRanks.Length > 0)
            {
                var pool = sourceBase?.GetComponent<SpawnPool>();
                var prefab = pool?.GetBaitPrefab(result.KickerRanks[0]);
                CreateUnitWithPrefab(prefab, result.KickerRanks[0], lane, sourceBase);
            }
        }

        void SpawnTripleWithPair(Card[] cards, CardTypeResult result, Lane lane, Component sourceBase)
        {
            for (int i = 0; i < 3; i++)
            {
                var u = CreateUnit(result.MainRank, lane, sourceBase, CardType.Triple);
                if (u != null) u.OnTakeDamageEvent += (damage, type) => RedistributeDamage(u, damage, type);
            }
            if (result.KickerRanks != null && result.KickerRanks.Length > 0)
            {
                var pool = sourceBase?.GetComponent<SpawnPool>();
                var cavPrefab = pool?.GetCavalryPrefab(result.KickerRanks[0]);
                for (int i = 0; i < 2; i++)
                    CreateUnitWithPrefab(cavPrefab, result.KickerRanks[0], lane, sourceBase);
            }
        }

        void SpawnStraight(Card[] cards, Lane lane, Component sourceBase)
        {
            var units = new List<CardUnit>();
            CardType straightType = cards.Length >= 6 ? CardType.Straight6Plus : CardType.Straight;
            foreach (var card in cards)
            {
                var u = CreateUnit(card.Rank, lane, sourceBase, straightType);
                if (u != null)
                {
                    u.transform.position += new Vector3((Random.value - 0.5f) * 0.3f, 0, 0);
                    units.Add(u);
                }
            }

            if (units.Count >= 2)
            {
                bool is6Plus = cards.Length >= 6;
                float atkBoost = is6Plus ? _straight6SpeedBoost : _straightSpeedBoost;
                float moveBoost = is6Plus ? _straight6MoveSpeed : _straightMoveSpeed;
                foreach (var u in units)
                {
                    u.ApplyBuff("straight", new CardUnit.StatBuff(
                        atkInterval: 1f / atkBoost, moveSpeed: moveBoost));
                }
            }
        }

        void SpawnConsecutivePair(Card[] cards, CardTypeResult result, Lane lane, Component sourceBase)
        {
            var pool = sourceBase?.GetComponent<SpawnPool>();

            var seen = new HashSet<CardRank>();
            foreach (var card in cards)
            {
                if (!seen.Add(card.Rank)) continue;
                var prefab = pool?.GetConsecutivePairPrefab(card.Rank);
                CreateUnitWithPrefab(prefab, card.Rank, lane, sourceBase, CardType.ConsecutivePair);
            }
        }

        void SpawnFourWithTwo(Card[] cards, CardTypeResult result, Lane lane, Component sourceBase)
        {
            var pool = sourceBase?.GetComponent<SpawnPool>();
            var tPrefab = pool?.GetTankPrefab(result.MainRank);
            CreateUnitWithPrefab(tPrefab, result.MainRank, lane, sourceBase, CardType.FourWithTwo);

            if (result.KickerRanks != null)
            {
                foreach (var kr in result.KickerRanks)
                {
                    var dPrefab = pool?.GetDronePrefab(kr);
                    CreateUnitWithPrefab(dPrefab, kr, lane, sourceBase);
                }
            }
        }

        [Header("轰炸参数")]
        [SerializeField] private float _bombingDuration = 4f;
        [SerializeField] private float _bombingDamagePerSecond = 0.3f;
        [SerializeField] private float _bombingInterval = 0.8f;

        void SpawnPlane(Card[] cards, CardTypeResult result, Lane lane, Component sourceBase)
        {
            var pool = sourceBase?.GetComponent<SpawnPool>();
            var prefab = pool?.GetBomberPrefab(result.MainRank);
            var bomber = CreateUnitWithPrefab(prefab, result.MainRank, lane, sourceBase, CardType.Plane);
            if (bomber == null) return;

            bomber.ApplyBuff("bomber_range", new CardUnit.StatBuff(range: 5f / bomber.Stats.Range));
            bomber.StartCoroutine(BombingRunCoroutine(bomber, sourceBase));
        }

        private IEnumerator BombingRunCoroutine(CardUnit bomber, Component sourceBase)
        {
            float elapsed = 0f;
            Lane bomberLane = bomber.Lane;

            while (elapsed < _bombingDuration)
            {
                var filter = new ContactFilter2D().NoFilter();
                int count = Physics2D.OverlapCircle((Vector2)bomber.transform.position, bomber.Stats.Range, filter, _overlapCache);
                for (int i = 0; i < count; i++)
                {
                    var enemy = _overlapCache[i].GetComponentInParent<CardUnit>();
                    if (enemy == null || !enemy.IsAlive || enemy.IsLandlord == bomber.IsLandlord) continue;
                    if (enemy.Lane != bomberLane) continue;
                    if (!bomber.CanAttackHeight(enemy.UnitHeight)) continue;
                    enemy.TakeDamage(bomber.Stats.ATK * _bombingDamagePerSecond, DamageType.Physical);
                }

                elapsed += _bombingInterval;
                yield return new WaitForSeconds(_bombingInterval);
            }

            if (bomber != null && bomber.IsAlive)
                bomber.TakeDamage(bomber.CurrentHP, DamageType.True);
        }

        void SpawnDoubleKingBomb(Lane lane, Component sourceBase)
        {
            var unit = SpawnHero(lane, sourceBase, awakened: true);
            if (unit != null) unit.transform.localScale = Vector3.one * 2f;
        }

        void SpawnJokerHero(Lane lane, Component sourceBase)
        {
            var unit = SpawnHero(lane, sourceBase, awakened: false);
            if (unit != null) unit.transform.localScale = Vector3.one * 1.3f;
        }

        void SpawnSingles(Card[] cards, Lane lane, Component sourceBase)
        {
            foreach (var card in cards)
            {
                if (card.IsJoker) { SpawnJokerHero(lane, sourceBase); continue; }
                CreateUnit(card.Rank, lane, sourceBase);
            }
        }

        // ─── 通用生成 ──────────────────────────────────

        CardUnit CreateUnit(CardRank rank, Lane lane, Component sourceBase, CardType cardType = CardType.Single)
        {
            var pool = sourceBase?.GetComponent<SpawnPool>();
            var prefab = pool?.GetPrefab(rank);
            if (prefab == null) { Debug.LogError($"[BattleManager] 未找到 Rank {rank} 的预制体映射"); return null; }
            return CreateUnitWithPrefab(prefab, rank, lane, sourceBase, cardType);
        }

        CardUnit CreateUnitWithPrefab(CardUnit prefab, CardRank rank, Lane lane, Component sourceBase, CardType cardType = CardType.Single)
        {
            if (prefab == null || sourceBase == null) return null;

            bool isLandlord = IsLandlord(sourceBase);
            Vector2 spawnPos = GetSpawnPosition(lane, sourceBase);
            var unit = unitFactory.Spawn(prefab, rank, lane, spawnPos, isLandlord);
            if (unit == null) return null;

            unit.SourceCardType = cardType;

            RegisterUnit(unit);
            var rg = sourceBase.GetComponent<RouteGroup>();
            unit.FollowPath = rg?.CurrentRoute;
            SnapToPathStart(unit);
            unit.OnDied -= OnUnitDied; unit.OnDied += OnUnitDied;
            unit.SetEnemyUnits(GetEnemiesFor(unit));

            if (_enableTyrantAura && isLandlord)
            {
                unit.ApplyBuff("tyrant", new CardUnit.StatBuff(
                    hp: _tyrantHpMultiplier, atk: _tyrantAtkMultiplier));
            }

            unit.SyncBaseScale();
            OnUnitSpawned?.Invoke(unit);

            return unit;
        }

        public CardUnit SpawnSummonedUnit(CardUnit prefab, Vector3 position, CardUnit summoner)
        {
            if (prefab == null || summoner == null) return null;
            if (unitFactory == null) return null;

            bool isLandlord = summoner.IsLandlord;
            Lane lane = summoner.Lane;

            var unit = unitFactory.Spawn(prefab, CardRank.Three, lane, position, isLandlord);
            if (unit == null) return null;

            unit.Summoner = summoner;

            RegisterUnit(unit);

            // 继承召唤师所在基地的路线（而非从召唤师身上取）
            var summonerBase = FindBaseFor(summoner);
            var rg = summonerBase?.GetComponent<RouteGroup>();
            unit.FollowPath = rg?.CurrentRoute;
            // 从召唤师位置开始，而非路径起点
            unit.SetPositionSynced(new Vector3(position.x, position.y, unit.transform.position.z));
            unit.OnDied -= OnUnitDied; unit.OnDied += OnUnitDied;
            unit.SetEnemyUnits(GetEnemiesFor(unit));

            unit.SyncBaseScale();
            OnUnitSpawned?.Invoke(unit);

            return unit;
        }

        private Vector2 GetSpawnPosition(Lane lane, Component sourceBase)
        {
            var pool = sourceBase?.GetComponent<SpawnPool>();
            if (pool != null && pool.SpawnPoint != null) return pool.SpawnPoint.position;
            if (sourceBase != null) return sourceBase.transform.position;
            return MapController.GetSpawnPosition(lane, false);
        }

        /// <summary>根据兵种的阵营找到对应的基地</summary>
        private Component FindBaseFor(CardUnit unit)
        {
            if (baseBuildings == null || unit == null) return null;
            foreach (var b in baseBuildings)
            {
                if (b == null) continue;
                var cu = b.GetComponent<CardUnit>();
                if (cu != null && cu.IsLandlord == unit.IsLandlord)
                    return b;
            }
            return null;
        }

        private void SnapToPathStart(CardUnit unit)
        {
            if (unit.FollowPath != null && unit.FollowPath.waypoints.Length > 0 && unit.FollowPath.waypoints[0] != null)
            {
                Vector3 pathStart = unit.FollowPath.GetPoint(0);
                unit.SetPositionSynced(new Vector3(pathStart.x, pathStart.y, unit.transform.position.z));
            }
        }
    }
}
