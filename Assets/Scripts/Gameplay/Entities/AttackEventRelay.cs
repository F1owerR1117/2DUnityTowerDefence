using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>
    /// 挂在 Animator 所在子物体（如 Photo）上，将 Animation Event 转发给父物体的 CardUnit。
    /// 解决 Animation Event 只能调用同 GameObject 方法的限制。
    /// </summary>
    public class AttackEventRelay : MonoBehaviour
    {
        private CardUnit _unit;

        private void Awake()
        {
            _unit = GetComponentInParent<CardUnit>();
        }

        /// <summary>由攻击动画的 Animation Event 在打击帧调用</summary>
        public void OnAttackHitFrame()
        {
            if (_unit != null)
                _unit.OnAttackHitFrame();
        }

        /// <summary>由召唤动画的 Animation Event 在召唤帧调用</summary>
        public void OnSummonFrameEvent()
        {
            if (_unit != null)
                _unit.OnSummonFrameEvent();
        }
    }
}
