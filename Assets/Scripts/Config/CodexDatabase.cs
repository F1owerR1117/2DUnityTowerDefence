using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DoudizhuTower.Config
{
    [CreateAssetMenu(fileName = "CodexDatabase", menuName = "DoudizhuTower/Codex Database")]
    public class CodexDatabase : ScriptableObject
    {
        public List<CodexEntry> Entries = new();

        private Dictionary<string, CodexEntry> _byId;
        private Dictionary<CodexCategory, List<CodexEntry>> _byCategory;

        public void Initialize()
        {
            _byId = new Dictionary<string, CodexEntry>();
            _byCategory = new Dictionary<CodexCategory, List<CodexEntry>>();

            foreach (var entry in Entries)
            {
                if (entry == null) continue;

                if (!string.IsNullOrEmpty(entry.Id))
                    _byId[entry.Id] = entry;

                if (!_byCategory.ContainsKey(entry.Category))
                    _byCategory[entry.Category] = new List<CodexEntry>();
                _byCategory[entry.Category].Add(entry);
            }
        }

        public CodexEntry GetById(string id)
        {
            if (_byId == null) Initialize();
            return _byId.TryGetValue(id, out var entry) ? entry : null;
        }

        public List<CodexEntry> GetByCategory(CodexCategory category)
        {
            if (_byCategory == null) Initialize();
            return _byCategory.TryGetValue(category, out var entries) ? entries : new List<CodexEntry>();
        }

        public List<CodexEntry> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<CodexEntry>(Entries);

            if (_byId == null) Initialize();

            string lowerQuery = query.ToLower();
            return Entries.Where(e =>
                e != null &&
                (e.DisplayName.ToLower().Contains(lowerQuery) ||
                 (e.Keywords != null && e.Keywords.Any(k => k.ToLower().Contains(lowerQuery))))
            ).ToList();
        }

        public List<CodexEntry> GetByCategoryAndSearch(CodexCategory category, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return GetByCategory(category);

            var categoryEntries = GetByCategory(category);
            string lowerQuery = query.ToLower();
            return categoryEntries.Where(e =>
                e.DisplayName.ToLower().Contains(lowerQuery) ||
                e.Id.ToLower().Contains(lowerQuery) ||
                (e.Keywords != null && e.Keywords.Any(k => k.ToLower().Contains(lowerQuery)))
            ).ToList();
        }

        /// <summary>从 CSV 批量导入图鉴条目（Editor 工具）</summary>
        public void ImportFromCsv(string csvPath)
        {
            if (!File.Exists(csvPath))
            {
                Debug.LogError($"[CodexDatabase] CSV 文件不存在: {csvPath}");
                return;
            }

            var lines = File.ReadAllLines(csvPath);
            if (lines.Length < 2)
            {
                Debug.LogError("[CodexDatabase] CSV 文件为空或只有表头");
                return;
            }

            int imported = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var fields = ParseCsvLine(line);
                if (fields.Length < 4)
                {
                    Debug.LogWarning($"[CodexDatabase] 行 {i + 1} 字段不足，跳过: {line}");
                    continue;
                }

                string id = fields[0].Trim();
                string displayName = fields[1].Trim();
                string categoryStr = fields[2].Trim();
                string description = fields.Length > 3 ? fields[3].Trim() : "";
                string extraInfo = fields.Length > 4 ? fields[4].Trim() : "";
                string keywordsStr = fields.Length > 5 ? fields[5].Trim() : "";

                if (!System.Enum.TryParse<CodexCategory>(categoryStr, out var category))
                {
                    Debug.LogWarning($"[CodexDatabase] 行 {i + 1} 无效分类: {categoryStr}，使用 CardValue");
                    category = CodexCategory.CardValue;
                }

                var keywords = string.IsNullOrEmpty(keywordsStr)
                    ? System.Array.Empty<string>()
                    : keywordsStr.Split('|').Select(k => k.Trim()).ToArray();

                var entry = ScriptableObject.CreateInstance<CodexEntry>();
                entry.name = $"CodexEntry_{id}";
                entry.Id = id;
                entry.DisplayName = displayName;
                entry.Category = category;
                entry.Description = description;
                entry.ExtraInfo = extraInfo;
                entry.Keywords = keywords;

                Entries.Add(entry);
                imported++;
            }

            Debug.Log($"[CodexDatabase] 从 CSV 导入 {imported} 个条目");
        }

        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            string current = "";

            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current);
                    current = "";
                }
                else
                {
                    current += c;
                }
            }
            result.Add(current);
            return result.ToArray();
        }
    }
}
