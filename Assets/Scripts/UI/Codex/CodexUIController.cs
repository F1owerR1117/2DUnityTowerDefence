using System.Collections.Generic;
using DoudizhuTower.Config;
using DoudizhuTower.Gameplay.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI.Codex
{
    public class CodexUIController : MonoBehaviour
    {
        [Header("数据")]
        [SerializeField] private CodexDatabase[] categoryDatabases;

        [Header("顶部栏")]
        [SerializeField] private Button backButton;
        [SerializeField] private Transform categoryTabsParent;
        [SerializeField] private GameObject categoryTabPrefab;

        [Header("左侧面板")]
        [SerializeField] private TMP_InputField searchInput;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Transform contentParent;
        [SerializeField] private GameObject entryButtonPrefab;

        [Header("右侧面板")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI extraInfoText;
        [SerializeField] private GameObject extraInfoContainer;
        [SerializeField] private TextMeshProUGUI unlockStatusText;
        [SerializeField] private Transform relatedEntriesParent;
        [SerializeField] private GameObject relatedEntryPrefab;

        [Header("进度显示")]
        [SerializeField] private TextMeshProUGUI progressText;

        private CodexCategory _currentCategory = CodexCategory.CardValue;
        private string _searchQuery = "";
        private List<GameObject> _spawnedEntries = new();
        private List<GameObject> _spawnedRelatedEntries = new();
        private CodexEntry _selectedEntry;

        private readonly string[] _categoryNames = { "卡牌数值", "牌型", "Boss", "建筑", "被动", "规则" };

        private void Start()
        {
            InitializeDatabases();
            CreateCategoryTabs();
            RefreshList();

            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);

            if (searchInput != null)
                searchInput.onValueChanged.AddListener(OnSearchChanged);
        }

        private void InitializeDatabases()
        {
            if (categoryDatabases == null) return;
            foreach (var db in categoryDatabases)
            {
                if (db != null) db.Initialize();
            }
        }

        private CodexDatabase GetCurrentDatabase()
        {
            if (categoryDatabases == null || categoryDatabases.Length == 0) return null;
            int index = (int)_currentCategory;
            return index < categoryDatabases.Length ? categoryDatabases[index] : null;
        }

        private void CreateCategoryTabs()
        {
            if (categoryTabsParent == null || categoryTabPrefab == null) return;

            var categories = (CodexCategory[])System.Enum.GetValues(typeof(CodexCategory));
            for (int i = 0; i < categories.Length; i++)
            {
                var tab = Instantiate(categoryTabPrefab, categoryTabsParent);
                var button = tab.GetComponent<Button>();
                var text = tab.GetComponentInChildren<TextMeshProUGUI>();

                if (text != null) text.text = _categoryNames[i];
                if (button != null)
                {
                    int categoryIndex = i;
                    button.onClick.AddListener(() => OnCategoryChanged(categories[categoryIndex]));
                }
            }
        }

        private void OnCategoryChanged(CodexCategory category)
        {
            _currentCategory = category;
            RefreshList();
            UpdateProgress();
        }

        private void OnSearchChanged(string query)
        {
            _searchQuery = query;
            RefreshList();
        }

        private void RefreshList()
        {
            // 清空旧条目
            if (contentParent != null)
            {
                for (int i = contentParent.childCount - 1; i >= 0; i--)
                {
                    Destroy(contentParent.GetChild(i).gameObject);
                }
            }
            _spawnedEntries.Clear();

            var db = GetCurrentDatabase();
            if (db == null) return;

            var entries = db.Search(_searchQuery);

            foreach (var entry in entries)
            {
                if (entryButtonPrefab == null || contentParent == null) continue;

                var buttonObj = Instantiate(entryButtonPrefab, contentParent);
                var button = buttonObj.GetComponent<Button>();

                // 设置文本
                var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null) text.text = entry.DisplayName;

                // 设置图标
                var icon = buttonObj.transform.Find("Icon")?.GetComponent<Image>();
                if (icon != null && entry.Icon != null) icon.sprite = entry.Icon;

                // 设置解锁状态
                var unlockIcon = buttonObj.transform.Find("UnlockIcon")?.GetComponent<Image>();
                if (unlockIcon != null)
                {
                    bool isUnlocked = SaveSystem.IsCodexEntryUnlocked(entry.Id);
                    unlockIcon.color = isUnlocked ? Color.green : Color.gray;
                    unlockIcon.gameObject.SetActive(true);
                }

                // 设置点击事件
                var entryRef = entry;
                if (button != null)
                    button.onClick.AddListener(() => OnEntryClicked(entryRef));

                _spawnedEntries.Add(buttonObj);
            }
        }

        private void OnEntryClicked(CodexEntry entry)
        {
            _selectedEntry = entry;
            UpdateDetailPanel(entry);
            UpdateRelatedEntries(entry);

            // 解锁条目
            if (entry != null)
                SaveSystem.UnlockCodexEntry(entry.Id);

            // 刷新列表以更新解锁状态
            RefreshList();
            UpdateProgress();
        }

        private void UpdateDetailPanel(CodexEntry entry)
        {
            if (entry == null) return;

            if (titleText != null) titleText.text = entry.DisplayName;
            if (iconImage != null)
            {
                iconImage.sprite = entry.Icon;
                iconImage.gameObject.SetActive(entry.Icon != null);
            }
            if (descriptionText != null) descriptionText.text = entry.Description;

            if (extraInfoText != null && extraInfoContainer != null)
            {
                bool hasExtra = !string.IsNullOrEmpty(entry.ExtraInfo);
                extraInfoContainer.SetActive(hasExtra);
                if (hasExtra) extraInfoText.text = entry.ExtraInfo;
            }

            // 显示解锁状态
            if (unlockStatusText != null)
            {
                bool isUnlocked = SaveSystem.IsCodexEntryUnlocked(entry.Id);
                unlockStatusText.text = isUnlocked ? "已解锁" : "未解锁";
                unlockStatusText.color = isUnlocked ? Color.green : Color.gray;
            }
        }

        private void UpdateRelatedEntries(CodexEntry entry)
        {
            // 清空旧的相关条目
            if (relatedEntriesParent != null)
            {
                for (int i = relatedEntriesParent.childCount - 1; i >= 0; i--)
                {
                    Destroy(relatedEntriesParent.GetChild(i).gameObject);
                }
            }
            _spawnedRelatedEntries.Clear();

            if (entry == null || entry.RelatedEntries == null || entry.RelatedEntries.Length == 0)
                return;

            // 查找相关条目
            var db = GetCurrentDatabase();
            if (db == null) return;

            foreach (var relatedId in entry.RelatedEntries)
            {
                var relatedEntry = db.GetById(relatedId);
                if (relatedEntry == null) continue;

                if (relatedEntryPrefab == null || relatedEntriesParent == null) continue;

                var buttonObj = Instantiate(relatedEntryPrefab, relatedEntriesParent);
                var button = buttonObj.GetComponent<Button>();
                var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();

                if (text != null) text.text = relatedEntry.DisplayName;

                var entryRef = relatedEntry;
                if (button != null)
                    button.onClick.AddListener(() => OnEntryClicked(entryRef));

                _spawnedRelatedEntries.Add(buttonObj);
            }
        }

        private void UpdateProgress()
        {
            if (progressText == null) return;

            var db = GetCurrentDatabase();
            if (db == null)
            {
                progressText.text = "";
                return;
            }

            int total = db.Entries.Count;
            int unlocked = 0;
            foreach (var entry in db.Entries)
            {
                if (entry != null && SaveSystem.IsCodexEntryUnlocked(entry.Id))
                    unlocked++;
            }

            progressText.text = $"解锁进度: {unlocked}/{total}";
        }

        private void OnBackClicked()
        {
            gameObject.SetActive(false);
            SceneLoader.LoadMainMenu();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            RefreshList();
            UpdateProgress();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
