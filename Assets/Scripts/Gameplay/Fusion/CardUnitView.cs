using UnityEngine;
using DoudizhuTower.Gameplay.Fusion;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// CardUnit 纯 View 层。
    /// 只负责显示 UnitState，不包含任何游戏逻辑。
    /// </summary>
    public class CardUnitView : MonoBehaviour
    {
        [Header("绑定")]
        public int UnitId;
        public UnitConfig Config;

        [Header("引用")]
        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        // 运行时引用（由 FusionGameManager 设置）
        private UnitState _boundState;
        private bool _isInitialized;

        /// <summary>
        /// 绑定 UnitState（由 FusionGameManager 调用）
        /// </summary>
        public void Bind(UnitState state)
        {
            _boundState = state;
            _isInitialized = true;
        }

        /// <summary>
        /// 更新显示（每帧调用）
        /// </summary>
        public void UpdateView()
        {
            if (!_isInitialized) return;

            // 死亡处理
            if (_boundState.State == UnitStateConstants.Dead)
            {
                PlayDieAnimation();
                return;
            }

            // 同步位置
            SyncPosition();

            // 同步动画
            SyncAnimation();

            // 同步血条（如果有）
            SyncHPBar();
        }

        private void SyncPosition()
        {
            transform.position = new Vector3(
                _boundState.PosX,
                _boundState.PosY,
                0f
            );
        }

        private void SyncAnimation()
        {
            if (_animator == null) return;

            // 根据状态播放动画
            switch (_boundState.State)
            {
                case UnitStateConstants.Idle:
                    _animator.SetInteger("State", 0);
                    break;
                case UnitStateConstants.Move:
                    _animator.SetInteger("State", 1);
                    break;
                case UnitStateConstants.Attack:
                    _animator.SetInteger("State", 2);
                    break;
            }
        }

        private void SyncHPBar()
        {
            // Phase 3: 血条 UI 显示
        }

        private void PlayDieAnimation()
        {
            if (_animator != null)
            {
                _animator.SetInteger("State", 3);
            }

            // 延迟销毁或回收
            Destroy(gameObject, 1f);
        }

        // =========================
        // 事件响应（由 EventBuffer 触发）
        // =========================

        /// <summary>
        /// 播放受击效果（闪白 + 飘字）。
        /// 由 FusionGameManager.ProcessEvents() 调用。
        /// </summary>
        public void PlayHitEffect()
        {
            // 受击闪白
            if (_spriteRenderer != null)
            {
                StartCoroutine(HitFlashCoroutine());
            }
        }

        private System.Collections.IEnumerator HitFlashCoroutine()
        {
            _spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            _spriteRenderer.color = Color.white;
        }

        /// <summary>
        /// 播放死亡效果（淡出 + 缩小）。
        /// 由 FusionGameManager.ProcessEvents() 调用。
        /// </summary>
        public void PlayDeathEffect()
        {
            if (_animator != null)
            {
                _animator.SetInteger("State", 3);
            }

            // 延迟销毁
            Destroy(gameObject, 1f);
        }
    }
}