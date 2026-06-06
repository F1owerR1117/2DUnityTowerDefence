using System.Collections.Generic;
using DoudizhuTower.Core.Cards;
using DoudizhuTower.Gameplay.Systems;
using DoudizhuTower.UI.Hand;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Battle
{
    public partial class DomainSystem
    {
        // ─── 公共方法 - 出牌触发 ───────────────────────

        /// <summary>
        /// 出牌时调用，检查并激活待处理的领域/护盾。
        /// 只有出牌成功后才调用此方法。
        /// </summary>
        public bool OnCardPlayed(CardTypeResult playResult, bool isPlayer)
        {
            return OnCardPlayed(playResult, isPlayer, false);
        }

        /// <summary>
        /// 出牌时调用，检查并激活待处理的领域/护盾。
        /// 只有出牌成功后才调用此方法。
        /// </summary>
        public bool OnCardPlayed(CardTypeResult playResult, bool isPlayer, bool isLandlord)
        {
            // 有待激活领域（玩家或 AI 地主）
            if (_isDomainPending)
            {
                bool isLandlordPlaying = isPlayer ? _isPlayerLandlord : isLandlord;

                if (isLandlordPlaying != _domainPendingByLandlord)
                {
                    // 不清除待激活状态，等待正确的人出牌
                }
                else
                {
                    // 单张牌型无法开启领域，保持待激活状态
                    if (playResult.Type == CardType.Single)
                    {
                        Debug.LogWarning("[DomainSystem] 单张牌型无法开启要不起领域，保持待激活状态");
                        return false;
                    }

                    _isDomainPending = false;
                    _domainPendingByLandlord = false;

                    ActivateDomainInternal(playResult, isLandlordPlaying);
                    return true;
                }
            }

            // 炸弹/火箭自动破封领域（不触发反制护盾）
            if (_isDomainActive && !_isCounterPending)
            {
                bool isBombOrRocket = playResult.Type == CardType.Bomb
                                   || playResult.Type == CardType.DoubleKingBomb;

                if (isBombOrRocket)
                {
                    bool isDomainOwner = isPlayer ? _isPlayerLandlord == _domainByLandlord : isLandlord == _domainByLandlord;
                    if (!isDomainOwner)
                    {
                        if (CardTypeCompare.CanCounter(_currentDomainType, playResult))
                        {
                            DeactivateDomain();
                            AudioManager.Instance?.PlayDomainBroken();
                            return true;
                        }
                        return false;
                    }
                }
            }

            // 炸弹/火箭击破反制护盾
            if (_isCounterShieldActive)
            {
                bool isBombOrRocket = playResult.Type == CardType.Bomb
                                   || playResult.Type == CardType.DoubleKingBomb;

                if (isBombOrRocket)
                {
                    bool isSealedByShield = isPlayer
                        ? _isPlayerLandlord == _domainByLandlord
                        : isLandlord == _domainByLandlord;

                    if (isSealedByShield && CardTypeCompare.CanCounter(_currentCounterType, playResult))
                    {
                        DeactivateCounterShield();
                        AudioManager.Instance?.PlayCounterShieldBroken();
                        return true;
                    }
                }
            }

            // 有待激活反制护盾（玩家或 AI 农民）
            if (_isCounterPending)
            {
                bool isFarmerPlaying = isPlayer ? !_isPlayerLandlord : !isLandlord;

                if (isFarmerPlaying != _counterPendingByFarmer)
                {
                    // 不清除待激活状态，等待正确的人出牌
                }
                else
                {
                    _isCounterPending = false;
                    _counterPendingByFarmer = false;

                    if (!_isDomainActive || !CardTypeCompare.CanCounter(_currentDomainType, playResult))
                    {
                        Debug.LogWarning("[DomainSystem] 牌型无法管上当前领域，反制失败");
                        _playerClickedCounter = false;
                        return false;
                    }

                    if (isPlayer && !_playerClickedCounter)
                    {
                        _playerClickedCounter = false;
                        DeactivateDomain();
                        AudioManager.Instance?.PlayDomainBroken();
                        return true;
                    }

                    _playerClickedCounter = false;
                    ActivateCounterShieldInternal(playResult);
                    return true;
                }
            }

            return false;
        }

        // ─── 计时器 ──────────────────────────────────

        private void UpdateDomainTimer()
        {
            if (!_isDomainActive) return;

            _domainTimer -= Time.deltaTime;
            if (_domainTimer <= 0f)
                DeactivateDomain();
        }

        private void UpdateCounterShieldTimer()
        {
            if (!_isCounterShieldActive) return;

            _counterShieldTimer -= Time.deltaTime;
            if (_counterShieldTimer <= 0f)
                DeactivateCounterShield();
        }

        private void UpdateCooldowns()
        {
            if (_domainCooldownTimer > 0f)
            {
                _domainCooldownTimer -= Time.deltaTime;
                if (_domainCooldownTimer < 0f) _domainCooldownTimer = 0f;
            }

            if (_counterShieldCooldownTimer > 0f)
            {
                _counterShieldCooldownTimer -= Time.deltaTime;
                if (_counterShieldCooldownTimer < 0f) _counterShieldCooldownTimer = 0f;
            }
        }

        // ─── 激活/关闭 ────────────────────────────────

        private void ActivateDomainInternal(CardTypeResult domainType, bool isLandlordPlaying = true)
        {
            if (_isDomainActive)
                DeactivateDomain();

            _isDomainActive = true;
            _currentDomainType = domainType;
            _domainByLandlord = isLandlordPlaying;
            _domainTimer = domainDuration;
            _domainCooldownTimer = domainCooldown;

            // 封印对方手牌
            if (isLandlordPlaying)
            {
                if (!_isPlayerLandlord)
                {
                    UpdateHandSealState(_playerHandArea, domainType);
                    UpdateCardHandSealState(_playerCardHand, domainType);
                    foreach (var hand in _allFriendlyCardHands)
                        UpdateCardHandSealState(hand, domainType);
                }
                else
                {
                    foreach (var hand in _allEnemyCardHands)
                        UpdateCardHandSealState(hand, domainType);
                }
            }
            else
            {
                if (_isPlayerLandlord)
                {
                    UpdateHandSealState(_playerHandArea, domainType);
                    UpdateCardHandSealState(_playerCardHand, domainType);
                }
                else
                {
                    foreach (var hand in _allEnemyCardHands)
                        UpdateCardHandSealState(hand, domainType);
                }
            }

            OnDomainActivated?.Invoke(domainType, domainDuration);
            AudioManager.Instance?.PlayDomainActivate();
        }

        private void ActivateCounterShieldInternal(CardTypeResult counterType)
        {
            DeactivateDomain();
            AudioManager.Instance?.PlayDomainBroken();

            _isCounterShieldActive = true;
            _currentCounterType = counterType;
            _counterShieldTimer = counterShieldDuration;
            _counterShieldCooldownTimer = counterShieldCooldown;

            if (_isPlayerLandlord)
            {
                UpdateHandSealState(_playerHandArea, counterType);
                UpdateCardHandSealState(_playerCardHand, counterType);
            }
            else
            {
                UpdateHandSealState(_enemyHandArea, counterType);
                foreach (var hand in _allEnemyCardHands)
                    UpdateCardHandSealState(hand, counterType);
            }

            OnCounterShieldActivated?.Invoke(counterType, counterShieldDuration);
            AudioManager.Instance?.PlayCounterShield();
        }

        private void DeactivateDomain()
        {
            if (!_isDomainActive) return;

            _isDomainActive = false;
            _domainTimer = 0f;
            _playerClickedCounter = false;

            ClearHandSealState(_playerHandArea);
            ClearHandSealState(_enemyHandArea);
            ClearCardHandSealState(_playerCardHand);
            foreach (var hand in _allEnemyCardHands)
                ClearCardHandSealState(hand);
            foreach (var hand in _allFriendlyCardHands)
                ClearCardHandSealState(hand);

            OnDomainDeactivated?.Invoke();
            AudioManager.Instance?.PlayDomainDeactivate();
        }

        private void DeactivateCounterShield()
        {
            if (!_isCounterShieldActive) return;

            _isCounterShieldActive = false;
            _counterShieldTimer = 0f;

            ClearHandSealState(_playerHandArea);
            ClearHandSealState(_enemyHandArea);
            ClearCardHandSealState(_playerCardHand);
            foreach (var hand in _allEnemyCardHands)
                ClearCardHandSealState(hand);
            foreach (var hand in _allFriendlyCardHands)
                ClearCardHandSealState(hand);

            OnCounterShieldDeactivated?.Invoke();
        }

        // ─── 手牌封印 ────────────────────────────────

        private void UpdateHandSealState(HandArea handArea, CardTypeResult sealType)
        {
            if (handArea == null) return;

            var allCards = handArea.GetAllCards();
            var unsealedCards = SealRuleEngine.GetUnsealedCards(allCards, sealType);

            foreach (var card in allCards)
            {
                bool isSealed = !unsealedCards.Contains(card);
                handArea.SetCardSealed(card, isSealed);
            }
        }

        private void ClearHandSealState(HandArea handArea)
        {
            if (handArea == null) return;

            var allCards = handArea.GetAllCards();
            foreach (var card in allCards)
                handArea.SetCardSealed(card, false);
        }

        private void ClearCardHandSealState(CardHand cardHand)
        {
            if (cardHand == null) return;
            cardHand.ClearAllSeals();
        }

        private void UpdateCardHandSealState(CardHand cardHand, CardTypeResult sealType)
        {
            if (cardHand == null)
            {
                Debug.LogError("[DomainSystem] UpdateCardHandSealState: cardHand 为 null");
                return;
            }

            var allCards = cardHand.CardsList;
            var unsealedCards = SealRuleEngine.GetUnsealedCards(allCards, sealType);

            foreach (var card in allCards)
            {
                bool isSealed = !unsealedCards.Contains(card);
                cardHand.SetCardSealed(card, isSealed);
            }
        }
    }
}
