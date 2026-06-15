using UnityEngine;

namespace DoudizhuTower.Gameplay.Battle
{
    /// <summary>
    /// 路线组。挂载到建筑上，管理多条行军路线。
    /// UI 通过 PrevRoute / NextRoute 切换当前选择。
    /// 锁定的路线会被跳过，玩家无法选中。
    /// </summary>
    public class RouteGroup : MonoBehaviour
    {
        [Header("行军路线列表")]
        [SerializeField] private RoutePath[] _routes = new RoutePath[1];
        private int _currentIndex;

        public RoutePath CurrentRoute => _routes.Length > 0 ? _routes[_currentIndex] : null;
        public int CurrentIndex => _currentIndex;
        public int RouteCount => _routes.Length;

        /// <summary>切换到下一条未锁定的路线</summary>
        public void NextRoute()
        {
            if (_routes.Length <= 1) return;
            for (int i = 0; i < _routes.Length; i++)
            {
                _currentIndex = (_currentIndex + 1) % _routes.Length;
                if (_routes[_currentIndex] == null || !_routes[_currentIndex].IsLocked)
                    return;
            }
        }

        /// <summary>切换到上一条未锁定的路线</summary>
        public void PrevRoute()
        {
            if (_routes.Length <= 1) return;
            for (int i = 0; i < _routes.Length; i++)
            {
                _currentIndex = (_currentIndex - 1 + _routes.Length) % _routes.Length;
                if (_routes[_currentIndex] == null || !_routes[_currentIndex].IsLocked)
                    return;
            }
        }

        /// <summary>设置路线索引（联机同步用）</summary>
        public void SetRouteIndex(int index)
        {
            if (index >= 0 && index < _routes.Length)
                _currentIndex = index;
        }

        /// <summary>按索引获取路线（供 BuildingAI 路线评估使用）</summary>
        public RoutePath GetRoute(int index)
        {
            if (index >= 0 && index < _routes.Length)
                return _routes[index];
            return null;
        }

        /// <summary>切换到第一条未锁定的路线。如果没有可用路线则不切换。</summary>
        public void SwitchToFirstUnlocked()
        {
            for (int i = 0; i < _routes.Length; i++)
            {
                if (_routes[i] != null && !_routes[i].IsLocked)
                {
                    _currentIndex = i;
                    return;
                }
            }
        }

        /// <summary>当前路线名称（用于 UI 显示）</summary>
        public string CurrentRouteName
        {
            get
            {
                if (_routes.Length == 0) return "无路线";
                var route = _routes[_currentIndex];
                return route != null ? $"{_currentIndex + 1}/{_routes.Length} {route.name}" : "未设置";
            }
        }
    }
}
