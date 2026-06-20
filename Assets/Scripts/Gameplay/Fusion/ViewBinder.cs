using System.Collections.Generic;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// View 绑定器。
    /// 管理 UnitState 和 CardUnitView 的映射关系。
    /// </summary>
    public class ViewBinder : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private FusionGameManager gameManager;

        [Header("View 预制体")]
        [SerializeField] private CardUnitView unitViewPrefab;

        // UnitId -> CardUnitView 映射
        private Dictionary<int, CardUnitView> _views = new Dictionary<int, CardUnitView>();

        // 已生成的 View 列表
        private List<CardUnitView> _spawnedViews = new List<CardUnitView>();

        /// <summary>
        /// 生成 View（当新单位生成时调用）
        /// </summary>
        public CardUnitView SpawnView(UnitState unit)
        {
            if (unitViewPrefab == null) return null;

            var view = Instantiate(unitViewPrefab);
            view.UnitId = unit.UnitId;
            view.Bind(unit);

            _views[unit.UnitId] = view;
            _spawnedViews.Add(view);

            return view;
        }

        /// <summary>
        /// 移除 View（当单位死亡时调用）
        /// </summary>
        public void RemoveView(int unitId)
        {
            if (_views.TryGetValue(unitId, out var view))
            {
                _views.Remove(unitId);
                _spawnedViews.Remove(view);

                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }
        }

        /// <summary>
        /// 同步所有 View（每帧调用）。
        /// Phase 4: Client 跳过（UnitBuffer 不同步）。
        /// </summary>
        public void SyncAll(UnitBuffer units, bool isHost)
        {
            if (!isHost) return;  // Phase 4: Client 不同步 UnitView

            for (int i = 0; i < units.Count; i++)
            {
                var unit = units.Get(i);

                // 检查是否已有 View
                if (_views.TryGetValue(unit.UnitId, out var view))
                {
                    // 更新绑定状态
                    view.Bind(unit);
                    view.UpdateView();
                }
                else
                {
                    // 生成新 View
                    SpawnView(unit);
                }
            }

            // 清理已死亡单位的 View
            CleanupDeadViews(units);
        }

        /// <summary>
        /// 清理已死亡单位的 View
        /// </summary>
        private void CleanupDeadViews(UnitBuffer units)
        {
            var deadIds = new List<int>();

            foreach (var kvp in _views)
            {
                bool found = false;
                for (int i = 0; i < units.Count; i++)
                {
                    if (units.Get(i).UnitId == kvp.Key)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    deadIds.Add(kvp.Key);
                }
            }

            foreach (var id in deadIds)
            {
                RemoveView(id);
            }
        }

        /// <summary>
        /// 获取所有 View
        /// </summary>
        public IReadOnlyList<CardUnitView> GetAllViews()
        {
            return _spawnedViews;
        }

        /// <summary>
        /// 获取指定 UnitId 的 View
        /// </summary>
        public CardUnitView GetView(int unitId)
        {
            _views.TryGetValue(unitId, out var view);
            return view;
        }

        private void OnDestroy()
        {
            // 清理所有 View
            foreach (var view in _spawnedViews)
            {
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }
            _views.Clear();
            _spawnedViews.Clear();
        }
    }
}