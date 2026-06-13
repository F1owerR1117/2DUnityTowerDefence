using System.Collections.Generic;

namespace DoudizhuTower.Gameplay.Network
{
    /// <summary>
    /// 本地联机模拟的消息路由中心。
    /// 所有 LocalNetworkService 实例共享，消息直接方法调用，零网络延迟。
    /// </summary>
    public static class LocalNetworkHub
    {
        private static readonly List<LocalNetworkService> _players = new();
        private static int _nextActorNumber = 1;

        public static int Register(LocalNetworkService player)
        {
            int actorNumber = _nextActorNumber++;
            _players.Add(player);
            return actorNumber;
        }

        public static void Unregister(LocalNetworkService player)
        {
            _players.Remove(player);
        }

        public static void Clear()
        {
            _players.Clear();
            _nextActorNumber = 1;
        }

        public static int PlayerCount => _players.Count;

        public static void SendToAll(string key, object value, int senderActor)
        {
            for (int i = 0; i < _players.Count; i++)
                _players[i]?.ReceiveEvent(key, value, senderActor);
        }

        public static void SendToMaster(string key, object value, int senderActor)
        {
            if (_players.Count > 0)
                _players[0]?.ReceiveEvent(key, value, senderActor);
        }

        public static void SendToPlayer(int targetActor, string key, object value, int senderActor)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i] != null && _players[i].LocalActorNumber == targetActor)
                {
                    _players[i].ReceiveEvent(key, value, senderActor);
                    return;
                }
            }
        }

        public static int[] GetAllActorNumbers()
        {
            var nums = new int[_players.Count];
            for (int i = 0; i < _players.Count; i++)
                nums[i] = _players[i]?.LocalActorNumber ?? 0;
            return nums;
        }

        public static string[] GetAllPlayerNames()
        {
            var names = new string[_players.Count];
            for (int i = 0; i < _players.Count; i++)
                names[i] = _players[i]?.LocalPlayerName ?? $"Player{i}";
            return names;
        }
    }
}
