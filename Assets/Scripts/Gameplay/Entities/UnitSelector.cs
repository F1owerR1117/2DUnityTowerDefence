using DoudizhuTower.UI.Panels;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>
    /// 点选器。左键点击兵种或建筑 → 实例化信息面板跟随显示。
    /// 重复点击同一目标 → 关闭面板。点击空白处 → 关闭面板。
    /// </summary>
    public class UnitSelector : MonoBehaviour
    {
        [SerializeField] private UnitInfoPanel panelPrefab;
        [SerializeField] private Camera cam;

        private UnitInfoPanel _activePanel;
        private CardUnit _selectedUnit;

        private static readonly ContactFilter2D _clickFilter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = false
        };
        private readonly Collider2D[] _clickBuffer = new Collider2D[16];

        private void Update()
        {
            if (_selectedUnit != null && !_selectedUnit.IsAlive)
                Deselect();

            if (!Input.GetMouseButtonDown(0)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Vector2 worldPos = cam.ScreenToWorldPoint(Input.mousePosition);
            int count = Physics2D.OverlapPoint(worldPos, _clickFilter, _clickBuffer);

            for (int i = 0; i < count; i++)
            {
                var col = _clickBuffer[i];
                if (col == null) continue;

                var unit = col.GetComponentInParent<CardUnit>();
                if (unit != null && unit.IsAlive)
                {
                    if (unit == _selectedUnit) { Deselect(); return; }
                    SelectUnit(unit);
                    return;
                }
            }

            // 点击空白处 → 关闭面板
            Deselect();
        }

        private void SelectUnit(CardUnit unit)
        {
            Deselect();
            _selectedUnit = unit;
            _selectedUnit.SetHighlighted(true);
            _activePanel = Instantiate(panelPrefab);
            _activePanel.Bind(unit);
        }

        private void Deselect()
        {
            if (_activePanel != null)
            {
                _activePanel.Unbind();
                Destroy(_activePanel.gameObject);
                _activePanel = null;
            }
            if (_selectedUnit != null)
            {
                _selectedUnit.SetHighlighted(false);
                _selectedUnit = null;
            }
        }

        private void OnDestroy()
        {
            Deselect();
        }
    }
}
