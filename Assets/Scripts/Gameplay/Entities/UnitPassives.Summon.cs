using DoudizhuTower.Gameplay.Battle;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    public partial class UnitPassives
    {
        // ══════════════════════════════════════════
        //  召唤师
        // ══════════════════════════════════════════

        private void UpdateSummoner()
        {
            if (!_owner.IsAlive || summonPrefab == null) return;

            // 眩晕期间不召唤
            if (_owner.StunTimer > 0f)
            {
                if (_isSummoning) CancelSummon();
                return;
            }

            // 正在播放召唤动画 → 等待 Animation Event 触发
            if (_isSummoning) return;

            // 清理已死亡的召唤物
            _summons.RemoveAll(u => u == null || !u.IsAlive);

            // 定时召唤
            _summonTimer += Time.deltaTime;
            if (_summonTimer >= summonInterval && _summons.Count < maxSummons)
            {
                _summonTimer = 0f;
                StartSummon(_owner.transform.position);
            }
        }

        private void OnSummonerKill(CardUnit victim)
        {
            if (victim == null || summonPrefab == null) return;
            if (_summons.Count >= maxSummons) return;
            if (_owner.StunTimer > 0f) return;
            // 击杀召唤物不会触发重复召唤，防止无限循环
            if (victim.Summoner != null) return;
            // 击杀召唤：立刻从尸体位置生成，不播放动画
            SpawnSummon(victim.transform.position);
        }

        /// <summary>开始召唤：打断当前攻击，播放召唤动画，调整速度对齐间隔</summary>
        private void StartSummon(Vector3 position)
        {
            if (_isSummoning) return;

            _isSummoning = true;
            _summonPosition = position;

            // 订阅召唤帧事件（Animation Event 触发）
            _owner.OnSummonFrame += OnSummonFrameHandler;

            // 打断当前攻击
            _owner.InterruptAttack();

            // 调整召唤动画速度，使动画长度对齐召唤间隔
            float clipLen = 0f;
            if (_owner.TryGetComponent<SimpleAnimator>(out var sa) && sa.summonClip != null)
                clipLen = sa.summonClip.length;
            float speed = clipLen > 0f ? Mathf.Min(clipLen / summonInterval, 4f) : 1f;
            _owner.SetAnimSpeedPublic(speed);

            // 播放召唤动画
            _owner.TriggerAnim("Summon");
        }

        /// <summary>Animation Event 回调：召唤帧触发，生成召唤物</summary>
        private void OnSummonFrameHandler()
        {
            _owner.OnSummonFrame -= OnSummonFrameHandler;
            _isSummoning = false;
            _owner.SetAnimSpeedPublic(1f);
            SpawnSummon(_summonPosition);
        }

        /// <summary>取消召唤（被眩晕打断时）</summary>
        private void CancelSummon()
        {
            _owner.OnSummonFrame -= OnSummonFrameHandler;
            _isSummoning = false;
            _owner.SetAnimSpeedPublic(1f);
        }

        private void SpawnSummon(Vector3 position)
        {
            if (summonPrefab == null || _owner == null) return;

            // 召唤音效 + 特效
            _unitAudio?.PlaySummon();
            _unitVFX?.PlaySummon(position);

            var prefabUnit = summonPrefab.GetComponent<CardUnit>();
            if (prefabUnit == null)
            {
                Debug.LogWarning("[Summoner] summonPrefab 上没有 CardUnit 组件");
                return;
            }

            var unit = BattleManager.Instance?.SpawnSummonedUnit(prefabUnit, position, _owner);
            if (unit != null)
                _summons.Add(unit);
        }
    }
}
