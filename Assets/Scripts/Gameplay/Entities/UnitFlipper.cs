using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>
    /// 兵种朝向翻转组件。挂在 Visual 子物体上，
    /// 翻转 localScale.x 使精灵图、特效、开火点同步翻转。
    /// 血条（同级 HealthBar）不受影响。
    /// </summary>
    public class UnitFlipper : MonoBehaviour
    {
        public void SetFacingRight(bool facingRight)
        {
            var s = transform.localScale;
            transform.localScale = new Vector3(facingRight ? 1f : -1f, s.y, s.z);
        }
    }
}
