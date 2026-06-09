using UnityEngine;

namespace DoudizhuTower.Gameplay.Battle
{
    /// <summary>
    /// 路线组。挂载到建筑上，管理多条行军路线。
    /// UI 通过 PrevRoute / NextRoute 切换当前选择。
    /// </summary>
    public class RouteGroup : MonoBehaviour
    {
        [Header("行军路线列表")]
        [SerializeField] private RoutePath[] _routes = new RoutePath[1];
        private int _currentIndex;

        public RoutePath CurrentRoute => _routes.Length > 0 ? _routes[_currentIndex] : null;
        public int CurrentIndex => _currentIndex;
        public int RouteCount => _routes.Length;

        /// <summary>切换到下一条路线</summary>
        public void NextRoute()
        {
            if (_routes.Length <= 1) return;
            _currentIndex = (_currentIndex + 1) % _routes.Length;
        }

        /// <summary>切换到上一条路线</summary>
        public void PrevRoute()
        {
            if (_routes.Length <= 1) return;
            _currentIndex = (_currentIndex - 1 + _routes.Length) % _routes.Length;
        }

        /// <summary>设置路线索引（联机同步用）</summary>
        public void SetRouteIndex(int index)
        {
            if (index >= 0 && index < _routes.Length)
                _currentIndex = index;
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
