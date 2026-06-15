using UnityEngine;

namespace DoudizhuTower.Gameplay.Network
{
    /// <summary>
    /// 游戏内网络状态面板（左上角半透明叠加层）。
    /// 挂载到 NetworkGameManager 同一 GameObject 上。
    /// </summary>
    public class NetworkDebugPanel : MonoBehaviour
    {
        private int _mySlot = -1;
        private bool _isMaster;
        private int _handCount;
        private float _gold;
        private int _deckRemaining;
        private int _unitCount;
        private string _lastEvent = "";
        private float _lastEventTime;

        private GUIStyle _style;
        private GUIStyle _evtStyle;

        public void Initialize(int slot, bool isMaster)
        {
            _mySlot = slot;
            _isMaster = isMaster;
        }

        public void UpdateState(int handCount, float gold, int deckRemaining)
        {
            _handCount = handCount;
            _gold = gold;
            _deckRemaining = deckRemaining;
        }

        public void LogEvent(string evt)
        {
            _lastEvent = evt;
            _lastEventTime = Time.time;
        }

        void Update()
        {
            _unitCount = FindObjectsByType<Gameplay.Entities.CardUnit>(FindObjectsSortMode.None).Length;
        }

        private void EnsureStyles()
        {
            if (_style != null) return;
            _style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 12,
            };
            _style.normal.textColor = Color.white;
            _evtStyle = new GUIStyle(_style);
        }

        void OnGUI()
        {
            EnsureStyles();

            float x = 10, y = 10, w = 260, h = 20;

            GUI.Box(new Rect(x, y, w, h * 5), "");
            GUI.Label(new Rect(x + 5, y, w, h), $"Slot: {_mySlot} | {(_isMaster ? "Master" : "Client")}", _style);
            GUI.Label(new Rect(x + 5, y + h, w, h), $"Hand: {_handCount} | Gold: {_gold:F0}", _style);
            GUI.Label(new Rect(x + 5, y + h * 2, w, h), $"Deck: {_deckRemaining} | Units: {_unitCount}", _style);

            float age = Time.time - _lastEventTime;
            _evtStyle.normal.textColor = age < 3f ? Color.yellow : Color.gray;
            GUI.Label(new Rect(x + 5, y + h * 4, w, h), $"Last: {_lastEvent}", _evtStyle);
        }
    }
}
