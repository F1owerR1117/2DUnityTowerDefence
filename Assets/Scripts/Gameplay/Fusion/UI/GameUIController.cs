using UnityEngine;
using DoudizhuTower.Gameplay.Fusion;

namespace DoudizhuTower.Gameplay.Fusion.UI
{
    /// <summary>
    /// 游戏 UI 控制器。
    /// 从 FusionGameManager.World 读取状态，驱动所有 UI 只读视图。
    /// </summary>
    public class GameUIController : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private FusionGameManager gameManager;

        [Header("UI 视图")]
        [SerializeField] private HandView[] handViews;
        [SerializeField] private GoldView[] goldViews;

        [Header("状态显示")]
        [SerializeField] private TMPro.TextMeshProUGUI turnText;
        [SerializeField] private TMPro.TextMeshProUGUI phaseText;
        [SerializeField] private TMPro.TextMeshProUGUI deckCountText;

        private void Update()
        {
            if (gameManager == null) return;

            var world = gameManager.World;

            // 更新每个玩家的 UI
            var player0 = world.Player0;
            var player1 = world.Player1;
            var player2 = world.Player2;

            if (handViews.Length > 0 && handViews[0] != null)
                handViews[0].Render(player0);
            if (goldViews.Length > 0 && goldViews[0] != null)
                goldViews[0].Render(player0);

            if (handViews.Length > 1 && handViews[1] != null)
                handViews[1].Render(player1);
            if (goldViews.Length > 1 && goldViews[1] != null)
                goldViews[1].Render(player1);

            if (handViews.Length > 2 && handViews[2] != null)
                handViews[2].Render(player2);
            if (goldViews.Length > 2 && goldViews[2] != null)
                goldViews[2].Render(player2);

            // 更新全局状态显示
            if (turnText != null)
                turnText.text = $"当前回合: Slot {world.Game.TurnSlot}";

            if (phaseText != null)
            {
                string phaseName = world.Game.Phase switch
                {
                    0 => "叫分",
                    1 => "出牌",
                    2 => "结算",
                    _ => "未知"
                };
                phaseText.text = $"阶段: {phaseName}";
            }

            if (deckCountText != null)
                deckCountText.text = $"牌堆: {world.Game.DeckCount}";
        }
    }
}