using UnityEngine;
using TMPro;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// Tick 显示器。
    /// 在 UI 上显示当前 Tick 和 Desync 状态。
    /// </summary>
    public class TickDisplay : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private TextMeshProUGUI tickText;
        [SerializeField] private TextMeshProUGUI desyncText;

        [Header("引用")]
        [SerializeField] private FusionGameManager gameManager;

        private void Update()
        {
            if (gameManager == null) return;

            if (tickText != null)
            {
                tickText.text = $"Tick: {gameManager.CurrentTick}";
            }

            if (desyncText != null)
            {
                desyncText.text = gameManager.HasStateAuthority ? "HOST" : "CLIENT";
            }
        }
    }
}