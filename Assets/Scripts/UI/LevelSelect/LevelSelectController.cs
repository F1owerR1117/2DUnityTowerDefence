using System.Collections.Generic;
using DoudizhuTower.Config;
using DoudizhuTower.Gameplay.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI.LevelSelect
{
    /// <summary>
    /// 关卡选择控制器（轮播式）。
    /// 中心卡片最大，两侧依次缩小，支持拖拽滑动和吸附。
    /// </summary>
    public class LevelSelectController : MonoBehaviour
    {
        [Header("关卡数据")]
        [SerializeField] private LevelConfig[] levelConfigs;

        [Header("UI 引用")]
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private RectTransform container;
        [SerializeField] private Button backButton;

        [Header("布局配置")]
        [Tooltip("卡片之间的间距")]
        [SerializeField] private float spacing = 300f;
        [Tooltip("中心卡片缩放")]
        [SerializeField] private float maxScale = 1.2f;
        [Tooltip("边缘卡片缩放")]
        [SerializeField] private float minScale = 0.5f;
        [Tooltip("最大可见距离（超出此距离的卡片隐藏）")]
        [SerializeField] private float visibleRange = 800f;
        [Tooltip("吸附速度")]
        [SerializeField] private float snapSpeed = 8f;

        private List<LevelCard> _cards = new();
        private float _scrollPosition;
        private bool _isDragging;
        private float _dragStartX;
        private float _scrollStart;
        private bool _hasDragged;

        private void Start()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => SceneLoader.LoadMainMenu());

            SpawnCards();

            // 初始定位到第一关
            if (_cards.Count > 0)
                _scrollPosition = 0;
        }

        private void Update()
        {
            HandleInput();
            HandleSnap();
            UpdateCards();
        }

        #region 卡片生成

        private void SpawnCards()
        {
            if (levelConfigs == null || cardPrefab == null || container == null) return;

            var sorted = new List<LevelConfig>(levelConfigs);
            sorted.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));

            // 注册关卡场景名列表供 SceneLoader 使用
            SceneLoader.LevelSceneNames = sorted.ConvertAll(c => c.sceneName).ToArray();

            foreach (var config in sorted)
            {
                var go = Instantiate(cardPrefab, container);
                go.name = $"Card_{config.levelName}";
                var card = go.GetComponent<LevelCard>();
                if (card != null)
                {
                    card.Setup(config);
                    _cards.Add(card);
                }
            }
        }

        #endregion

        #region 输入处理

        private void HandleInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _isDragging = true;
                _hasDragged = false;
                _dragStartX = Input.mousePosition.x;
                _scrollStart = _scrollPosition;
            }
            else if (Input.GetMouseButton(0) && _isDragging)
            {
                float delta = Input.mousePosition.x - _dragStartX;
                if (Mathf.Abs(delta) > 5f)
                    _hasDragged = true;
                _scrollPosition = _scrollStart - delta;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                if (!_hasDragged)
                {
                    // 没有拖动 → 视为点击
                    HandleClick();
                }
                _isDragging = false;
                _hasDragged = false;
            }

            // 鼠标滚轮
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _scrollPosition -= scroll * 500f;
            }
        }

        #endregion

        #region 吸附效果

        private void HandleSnap()
        {
            if (_isDragging || _cards.Count == 0) return;

            // 找到最近的卡片索引
            int nearest = Mathf.RoundToInt(_scrollPosition / spacing);
            nearest = Mathf.Clamp(nearest, 0, _cards.Count - 1);

            float targetPos = nearest * spacing;
            _scrollPosition = Mathf.Lerp(_scrollPosition, targetPos, Time.deltaTime * snapSpeed);

            // 到达目标后停止
            if (Mathf.Abs(_scrollPosition - targetPos) < 0.5f)
                _scrollPosition = targetPos;
        }

        #endregion

        #region 点击处理

        private void HandleClick()
        {
            if (_cards.Count == 0) return;

            // 找到当前居中的卡片
            int nearest = Mathf.RoundToInt(_scrollPosition / spacing);
            nearest = Mathf.Clamp(nearest, 0, _cards.Count - 1);

            var card = _cards[nearest];
            if (card == null || !card.IsUnlocked) return;

            // 检查鼠标是否在卡片范围内
            var rt = card.GetComponent<RectTransform>();
            if (rt == null) return;

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, Input.mousePosition, null, out localPoint);

            if (rt.rect.Contains(localPoint))
            {
                EnterLevel(card.Config);
            }
        }

        private void EnterLevel(LevelConfig config)
        {
            if (config == null) return;
            int index = _cards.FindIndex(c => c.Config == config);
            SceneLoader.SetCurrentLevel(index);
            Debug.Log($"[LevelSelect] 进入关卡: {config.levelName} (索引 {index})");
            SceneLoader.LoadScene(config.sceneName);
        }

        #endregion

        #region 卡片更新

        private void UpdateCards()
        {
            if (_cards.Count == 0 || container == null) return;

            float containerCenter = container.rect.width / 2f;

            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (card == null) continue;

                // 计算卡片在容器中的 X 位置
                float cardX = containerCenter + i * spacing - _scrollPosition;

                // 距离中心的偏移
                float distFromCenter = Mathf.Abs(cardX - containerCenter);

                // 归一化距离（0=中心，1=边缘）
                float normalizedDist = Mathf.Clamp01(distFromCenter / visibleRange);

                // 动态缩放
                float scale = Mathf.Lerp(maxScale, minScale, normalizedDist);
                card.transform.localScale = Vector3.one * scale;

                // 设置位置
                var rt = card.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0, 0.5f);
                    rt.anchorMax = new Vector2(0, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(cardX, 0);
                }

                // 超出可见范围则隐藏
                card.gameObject.SetActive(distFromCenter <= visibleRange);
            }
        }

        #endregion
    }
}
