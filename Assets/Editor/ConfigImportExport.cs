using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DoudizhuTower.Config;
using DoudizhuTower.Gameplay.Entities;
using UnityEditor;
using UnityEngine;

namespace DoudizhuTower.Editor
{
    /// <summary>
    /// CSV 配置数据导入导出 Editor 窗口。
    /// 菜单入口：Tools → 配置数据管理
    /// </summary>
    public class ConfigImportExport : EditorWindow
    {
        private Vector2 _scrollPos;
        private string _log = "";
        private const string CsvDir = "Assets/StreamingData/Config";

        [MenuItem("Tools/配置数据管理")]
        public static void ShowWindow()
        {
            GetWindow<ConfigImportExport>("配置数据管理");
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            GUILayout.Label("CSV 数据管线", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // ─── 全部操作 ───
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Label("快捷操作", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全部导出", GUILayout.Height(30)))
            {
                _log = "";
                ExportUnits(); ExportHeroes(); ExportEconomy(); ExportBidding(); ExportLevels();
                _log += "\n✓ 全部导出完成";
            }
            if (GUILayout.Button("全部导入", GUILayout.Height(30)))
            {
                _log = "";
                ImportUnits(); ImportHeroes(); ImportEconomy(); ImportBidding(); ImportLevels();
                _log += "\n✓ 全部导入完成";
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // ─── 兵种数值 ───
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Label("兵种数值 (Units.csv)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("导出 → CSV")) { _log = ""; ExportUnits(); }
            if (GUILayout.Button("CSV → 导入")) { _log = ""; ImportUnits(); }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // ─── 英雄配置 ───
            GUILayout.Label("英雄配置 (Heroes.csv)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("导出 → CSV")) { _log = ""; ExportHeroes(); }
            if (GUILayout.Button("CSV → 导入")) { _log = ""; ImportHeroes(); }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // ─── 经济配置 ───
            GUILayout.Label("经济配置 (Economy.csv)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("导出 → CSV")) { _log = ""; ExportEconomy(); }
            if (GUILayout.Button("CSV → 导入")) { _log = ""; ImportEconomy(); }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // ─── 叫分配置 ───
            GUILayout.Label("叫分配置 (Bidding.csv)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("导出 → CSV")) { _log = ""; ExportBidding(); }
            if (GUILayout.Button("CSV → 导入")) { _log = ""; ImportBidding(); }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // ─── 关卡配置 ───
            GUILayout.Label("关卡配置 (Levels.csv)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("导出 → CSV")) { _log = ""; ExportLevels(); }
            if (GUILayout.Button("CSV → 导入")) { _log = ""; ImportLevels(); }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // ─── 日志 ───
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Label("操作日志", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(string.IsNullOrEmpty(_log) ? "等待操作..." : _log, MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        // ═══════════════════════════════════════════════════
        //  兵种数值
        // ═══════════════════════════════════════════════════

        private void ExportUnits()
        {
            var prefabs = FindAllCardUnitPrefabs();
            var headers = new List<string>
            {
                "PrefabPath", "DisplayName", "HP", "ATK", "AttackInterval",
                "MoveSpeed", "Range", "HitCount", "IsRanged", "UnitHeight",
                "DetectionRange", "IsBuilding", "RegenPerSecond"
            };

            var rows = new List<Dictionary<string, string>>();
            foreach (var prefab in prefabs)
            {
                var so = new SerializedObject(prefab);
                var row = new Dictionary<string, string>
                {
                    ["PrefabPath"] = AssetDatabase.GetAssetPath(prefab).Replace(".prefab", "").Replace("Assets/", ""),
                    ["DisplayName"] = prefab.gameObject.name,
                    ["HP"] = GetFloat(so, "_hp"),
                    ["ATK"] = GetFloat(so, "_atk"),
                    ["AttackInterval"] = GetFloat(so, "_attackInterval"),
                    ["MoveSpeed"] = GetFloat(so, "_moveSpeed"),
                    ["Range"] = GetFloat(so, "_range"),
                    ["HitCount"] = GetInt(so, "_hitCount"),
                    ["IsRanged"] = GetBool(so, "_isRanged"),
                    ["UnitHeight"] = GetEnumFlags(so, "_unitHeight", typeof(UnitHeight)),
                    ["DetectionRange"] = GetFloat(so, "_detectionRange"),
                    ["IsBuilding"] = GetBool(so, "_isBuilding"),
                    ["RegenPerSecond"] = GetFloat(so, "_regenPerSecond"),
                };
                rows.Add(row);
            }

            var path = Path.Combine(CsvDir, "Units.csv");
            CsvIO.WriteCsv(path, headers, rows);
            AssetDatabase.Refresh();
            _log += $"✓ 兵种数值已导出: {path} ({rows.Count} 条)\n";
        }

        private void ImportUnits()
        {
            var path = Path.Combine(CsvDir, "Units.csv");
            if (!File.Exists(path)) { _log += $"✗ 文件不存在: {path}\n"; return; }

            var rows = CsvIO.ReadCsv(path);
            int count = 0;
            foreach (var row in rows)
            {
                if (!row.TryGetValue("PrefabPath", out var prefabPath)) continue;
                var fullPath = "Assets/" + prefabPath + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<CardUnit>(fullPath);
                if (prefab == null) { _log += $"  ⚠ 找不到预制体: {fullPath}\n"; continue; }

                var so = new SerializedObject(prefab);
                SetFloat(so, "_hp", row, "HP");
                SetFloat(so, "_atk", row, "ATK");
                SetFloat(so, "_attackInterval", row, "AttackInterval");
                SetFloat(so, "_moveSpeed", row, "MoveSpeed");
                SetFloat(so, "_range", row, "Range");
                SetInt(so, "_hitCount", row, "HitCount");
                SetBool(so, "_isRanged", row, "IsRanged");
                SetEnumFlags(so, "_unitHeight", row, "UnitHeight", typeof(UnitHeight));
                SetFloat(so, "_detectionRange", row, "DetectionRange");
                SetBool(so, "_isBuilding", row, "IsBuilding");
                SetFloat(so, "_regenPerSecond", row, "RegenPerSecond");

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(prefab);
                count++;
            }

            AssetDatabase.SaveAssets();
            _log += $"✓ 兵种数值已导入: {count} 个预制体\n";
        }

        // ═══════════════════════════════════════════════════
        //  英雄配置
        // ═══════════════════════════════════════════════════

        private void ExportHeroes()
        {
            var assets = FindAssets<HeroConfig>();
            var headers = new List<string>
            {
                "HeroType", "HeroName", "HP", "ATK", "AttackInterval", "MoveSpeed", "Range",
                "AwakenHP", "AwakenATK", "AwakenMoveSpeed", "AwakenRange",
                "BlademasterProcChance", "BlademasterDamageMultiplier",
                "GuardianDamageReduction",
                "WarlockSplashRadius", "WarlockSplashDamageMultiplier",
                "SpiritRiderAuraRadius", "SpiritRiderAttackSpeedBonus", "SpiritRiderMoveSpeedBonus"
            };

            var rows = new List<Dictionary<string, string>>();
            foreach (var asset in assets)
            {
                var so = new SerializedObject(asset);
                var row = new Dictionary<string, string>
                {
                    ["HeroType"] = asset.heroType.ToString(),
                    ["HeroName"] = asset.heroName,
                    ["HP"] = F(asset.hp),
                    ["ATK"] = F(asset.atk),
                    ["AttackInterval"] = F(asset.attackInterval),
                    ["MoveSpeed"] = F(asset.moveSpeed),
                    ["Range"] = F(asset.range),
                    ["AwakenHP"] = F(asset.awakenHpMultiplier),
                    ["AwakenATK"] = F(asset.awakenAtkMultiplier),
                    ["AwakenMoveSpeed"] = F(asset.awakenMoveSpeedMultiplier),
                    ["AwakenRange"] = F(asset.awakenRangeMultiplier),
                    ["BlademasterProcChance"] = F(asset.blademasterProcChance),
                    ["BlademasterDamageMultiplier"] = F(asset.blademasterDamageMultiplier),
                    ["GuardianDamageReduction"] = F(asset.guardianDamageReduction),
                    ["WarlockSplashRadius"] = F(asset.warlockSplashRadius),
                    ["WarlockSplashDamageMultiplier"] = F(asset.warlockSplashDamageMultiplier),
                    ["SpiritRiderAuraRadius"] = F(asset.spiritRiderAuraRadius),
                    ["SpiritRiderAttackSpeedBonus"] = F(asset.spiritRiderAttackSpeedBonus),
                    ["SpiritRiderMoveSpeedBonus"] = F(asset.spiritRiderMoveSpeedBonus),
                };
                rows.Add(row);
            }

            var path = Path.Combine(CsvDir, "Heroes.csv");
            CsvIO.WriteCsv(path, headers, rows);
            AssetDatabase.Refresh();
            _log += $"✓ 英雄配置已导出: {path} ({rows.Count} 条)\n";
        }

        private void ImportHeroes()
        {
            var path = Path.Combine(CsvDir, "Heroes.csv");
            if (!File.Exists(path)) { _log += $"✗ 文件不存在: {path}\n"; return; }

            var rows = CsvIO.ReadCsv(path);
            var assets = FindAssets<HeroConfig>();
            var lookup = assets.ToDictionary(a => a.heroType.ToString(), a => a);

            int count = 0;
            foreach (var row in rows)
            {
                if (!row.TryGetValue("HeroType", out var heroType)) continue;
                if (!lookup.TryGetValue(heroType, out var asset)) { _log += $"  ⚠ 找不到英雄配置: {heroType}\n"; continue; }

                var so = new SerializedObject(asset);
                SetStr(so, "heroName", row, "HeroName");
                SetFloat(so, "hp", row, "HP");
                SetFloat(so, "atk", row, "ATK");
                SetFloat(so, "attackInterval", row, "AttackInterval");
                SetFloat(so, "moveSpeed", row, "MoveSpeed");
                SetFloat(so, "range", row, "Range");
                SetFloat(so, "awakenHpMultiplier", row, "AwakenHP");
                SetFloat(so, "awakenAtkMultiplier", row, "AwakenATK");
                SetFloat(so, "awakenMoveSpeedMultiplier", row, "AwakenMoveSpeed");
                SetFloat(so, "awakenRangeMultiplier", row, "AwakenRange");
                SetFloat(so, "blademasterProcChance", row, "BlademasterProcChance");
                SetFloat(so, "blademasterDamageMultiplier", row, "BlademasterDamageMultiplier");
                SetFloat(so, "guardianDamageReduction", row, "GuardianDamageReduction");
                SetFloat(so, "warlockSplashRadius", row, "WarlockSplashRadius");
                SetFloat(so, "warlockSplashDamageMultiplier", row, "WarlockSplashDamageMultiplier");
                SetFloat(so, "spiritRiderAuraRadius", row, "SpiritRiderAuraRadius");
                SetFloat(so, "spiritRiderAttackSpeedBonus", row, "SpiritRiderAttackSpeedBonus");
                SetFloat(so, "spiritRiderMoveSpeedBonus", row, "SpiritRiderMoveSpeedBonus");

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
                count++;
            }

            AssetDatabase.SaveAssets();
            _log += $"✓ 英雄配置已导入: {count} 个\n";
        }

        // ═══════════════════════════════════════════════════
        //  经济配置
        // ═══════════════════════════════════════════════════

        private void ExportEconomy()
        {
            var asset = FindFirstAsset<EconomyConfig>();
            if (asset == null) { _log += "✗ 找不到 EconomyConfig\n"; return; }

            var headers = new List<string> { "Parameter", "Value", "Description" };
            var rows = new List<Dictionary<string, string>>
            {
                ParamRow("initialGold", asset.initialGold, "初始金币"),
                ParamRow("farmerBaseIncome", asset.farmerBaseIncome, "农民基础回金速度"),
                ParamRow("landlordBonusIncome", asset.landlordBonusIncome, "地主额外回金"),
                ParamRow("incomeStepPerMinute", asset.incomeStepPerMinute, "每分钟回金增长"),
                ParamRow("suddenDeathMultiplier", asset.suddenDeathMultiplier, "骤死期金币倍率"),
                ParamRow("baseCostC3", asset.baseCostC3, "3点基础费用"),
                ParamRow("costGrowthRate", asset.costGrowthRate, "费用增长率"),
            };

            var path = Path.Combine(CsvDir, "Economy.csv");
            CsvIO.WriteCsv(path, headers, rows);
            AssetDatabase.Refresh();
            _log += $"✓ 经济配置已导出: {path}\n";
        }

        private void ImportEconomy()
        {
            var path = Path.Combine(CsvDir, "Economy.csv");
            if (!File.Exists(path)) { _log += $"✗ 文件不存在: {path}\n"; return; }

            var asset = FindFirstAsset<EconomyConfig>();
            if (asset == null) { _log += "✗ 找不到 EconomyConfig\n"; return; }

            var rows = CsvIO.ReadCsv(path);
            var lookup = rows.ToDictionary(r => r["Parameter"], r => r["Value"]);
            var so = new SerializedObject(asset);

            SetParamFloat(so, "initialGold", lookup);
            SetParamFloat(so, "farmerBaseIncome", lookup);
            SetParamFloat(so, "landlordBonusIncome", lookup);
            SetParamFloat(so, "incomeStepPerMinute", lookup);
            SetParamFloat(so, "suddenDeathMultiplier", lookup);
            SetParamFloat(so, "baseCostC3", lookup);
            SetParamFloat(so, "costGrowthRate", lookup);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            _log += "✓ 经济配置已导入\n";
        }

        // ═══════════════════════════════════════════════════
        //  叫分配置
        // ═══════════════════════════════════════════════════

        private void ExportBidding()
        {
            var asset = FindFirstAsset<BiddingConfig>();
            if (asset == null) { _log += "✗ 找不到 BiddingConfig\n"; return; }

            var headers = new List<string> { "Parameter", "Value", "Description" };
            var rows = new List<Dictionary<string, string>>
            {
                ParamRow("biddingDuration", asset.biddingDuration, "叫分总时长（秒）"),
                ParamRow("maxBid", asset.maxBid, "最高叫分"),
                ParamRow("aiPassChance", asset.aiPassChance, "AI不叫概率"),
                ParamRow("aiBid1Weight", asset.aiBid1Weight, "AI叫1分权重"),
                ParamRow("aiBid2Weight", asset.aiBid2Weight, "AI叫2分权重"),
                ParamRow("aiBid3Weight", asset.aiBid3Weight, "AI叫3分权重"),
                ParamRow("randomAssignOnTimeout", asset.randomAssignOnTimeout ? 1f : 0f, "超时随机分配(1=是/0=否)"),
            };

            var path = Path.Combine(CsvDir, "Bidding.csv");
            CsvIO.WriteCsv(path, headers, rows);
            AssetDatabase.Refresh();
            _log += $"✓ 叫分配置已导出: {path}\n";
        }

        private void ImportBidding()
        {
            var path = Path.Combine(CsvDir, "Bidding.csv");
            if (!File.Exists(path)) { _log += $"✗ 文件不存在: {path}\n"; return; }

            var asset = FindFirstAsset<BiddingConfig>();
            if (asset == null) { _log += "✗ 找不到 BiddingConfig\n"; return; }

            var rows = CsvIO.ReadCsv(path);
            var lookup = rows.ToDictionary(r => r["Parameter"], r => r["Value"]);
            var so = new SerializedObject(asset);

            SetParamFloat(so, "biddingDuration", lookup);
            if (lookup.TryGetValue("maxBid", out var maxBidStr) && int.TryParse(maxBidStr, out var maxBid))
                so.FindProperty("maxBid").intValue = maxBid;
            SetParamFloat(so, "aiPassChance", lookup);
            SetParamFloat(so, "aiBid1Weight", lookup);
            SetParamFloat(so, "aiBid2Weight", lookup);
            SetParamFloat(so, "aiBid3Weight", lookup);
            if (lookup.TryGetValue("randomAssignOnTimeout", out var randStr) && float.TryParse(randStr, out var randVal))
                so.FindProperty("randomAssignOnTimeout").boolValue = randVal > 0.5f;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            _log += "✓ 叫分配置已导入\n";
        }

        // ═══════════════════════════════════════════════════
        //  关卡配置
        // ═══════════════════════════════════════════════════

        private void ExportLevels()
        {
            var assets = FindAssets<LevelConfig>();
            var headers = new List<string>
            {
                "LevelID", "DisplayName", "Description", "SceneName", "Difficulty", "SortOrder", "IsUnlocked"
            };

            var rows = new List<Dictionary<string, string>>();
            foreach (var asset in assets)
            {
                var row = new Dictionary<string, string>
                {
                    ["LevelID"] = asset.name,
                    ["DisplayName"] = asset.levelName,
                    ["Description"] = asset.description ?? "",
                    ["SceneName"] = asset.sceneName,
                    ["Difficulty"] = asset.difficulty.ToString(),
                    ["SortOrder"] = asset.sortOrder.ToString(),
                    ["IsUnlocked"] = asset.isUnlocked ? "1" : "0",
                };
                rows.Add(row);
            }

            var path = Path.Combine(CsvDir, "Levels.csv");
            CsvIO.WriteCsv(path, headers, rows);
            AssetDatabase.Refresh();
            _log += $"✓ 关卡配置已导出: {path} ({rows.Count} 条)\n";
        }

        private void ImportLevels()
        {
            var path = Path.Combine(CsvDir, "Levels.csv");
            if (!File.Exists(path)) { _log += $"✗ 文件不存在: {path}\n"; return; }

            var rows = CsvIO.ReadCsv(path);
            var assets = FindAssets<LevelConfig>();
            var lookup = assets.ToDictionary(a => a.name, a => a);

            int count = 0;
            foreach (var row in rows)
            {
                if (!row.TryGetValue("LevelID", out var levelId)) continue;
                if (!lookup.TryGetValue(levelId, out var asset)) { _log += $"  ⚠ 找不到关卡: {levelId}\n"; continue; }

                var so = new SerializedObject(asset);
                SetStr(so, "levelName", row, "DisplayName");
                SetStr(so, "description", row, "Description");
                SetStr(so, "sceneName", row, "SceneName");
                if (row.TryGetValue("Difficulty", out var diffStr) && int.TryParse(diffStr, out var diff))
                    so.FindProperty("difficulty").intValue = diff;
                if (row.TryGetValue("SortOrder", out var sortStr) && int.TryParse(sortStr, out var sort))
                    so.FindProperty("sortOrder").intValue = sort;
                if (row.TryGetValue("IsUnlocked", out var unlockStr))
                    so.FindProperty("isUnlocked").boolValue = unlockStr == "1";

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
                count++;
            }

            AssetDatabase.SaveAssets();
            _log += $"✓ 关卡配置已导入: {count} 个\n";
        }

        // ═══════════════════════════════════════════════════
        //  工具方法
        // ═══════════════════════════════════════════════════

        private static List<CardUnit> FindAllCardUnitPrefabs()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Army/ArmyPrefabs", "Assets/Prefabs/Buildings/TowerEntities" });
            var result = new List<CardUnit>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<CardUnit>(path);
                if (prefab != null) result.Add(prefab);
            }
            return result;
        }

        private static T[] FindAssets<T>() where T : UnityEngine.Object
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            var result = new List<T>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) result.Add(asset);
            }
            return result.ToArray();
        }

        private static T FindFirstAsset<T>() where T : UnityEngine.Object
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        // SerializedProperty 读写
        private static string GetFloat(SerializedObject so, string name)
        {
            var p = so.FindProperty(name);
            return p != null ? F(p.floatValue) : "0";
        }

        private static string GetInt(SerializedObject so, string name)
        {
            var p = so.FindProperty(name);
            return p != null ? p.intValue.ToString() : "0";
        }

        private static string GetBool(SerializedObject so, string name)
        {
            var p = so.FindProperty(name);
            return p != null && p.boolValue ? "1" : "0";
        }

        private static string GetEnumFlags(SerializedObject so, string name, Type enumType)
        {
            var p = so.FindProperty(name);
            if (p == null) return "";
            int val = p.intValue;
            if (val == 0) return Enum.GetName(enumType, 0) ?? "0";
            var names = new List<string>();
            foreach (Enum flag in Enum.GetValues(enumType))
            {
                if (Convert.ToInt32(flag) != 0 && ((Enum)Enum.ToObject(enumType, val)).HasFlag(flag))
                    names.Add(flag.ToString());
            }
            return names.Count > 0 ? string.Join(",", names) : val.ToString();
        }

        private static void SetFloat(SerializedObject so, string name, Dictionary<string, string> row, string key)
        {
            if (row.TryGetValue(key, out var str) && float.TryParse(str, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var val))
                so.FindProperty(name).floatValue = val;
        }

        private static void SetInt(SerializedObject so, string name, Dictionary<string, string> row, string key)
        {
            if (row.TryGetValue(key, out var str) && int.TryParse(str, out var val))
                so.FindProperty(name).intValue = val;
        }

        private static void SetBool(SerializedObject so, string name, Dictionary<string, string> row, string key)
        {
            if (row.TryGetValue(key, out var str))
                so.FindProperty(name).boolValue = str == "1" || str.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static void SetStr(SerializedObject so, string name, Dictionary<string, string> row, string key)
        {
            if (row.TryGetValue(key, out var str))
                so.FindProperty(name).stringValue = str;
        }

        private static void SetEnumFlags(SerializedObject so, string name, Dictionary<string, string> row, string key, Type enumType)
        {
            if (!row.TryGetValue(key, out var str)) return;
            int val = 0;
            foreach (var part in str.Split(','))
            {
                var trimmed = part.Trim();
                if (Enum.TryParse(enumType, trimmed, true, out var parsed))
                    val |= Convert.ToInt32(parsed);
            }
            so.FindProperty(name).intValue = val;
        }

        private static void SetParamFloat(SerializedObject so, string name, Dictionary<string, string> lookup)
        {
            if (lookup.TryGetValue(name, out var str) && float.TryParse(str, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var val))
                so.FindProperty(name).floatValue = val;
        }

        private static Dictionary<string, string> ParamRow(string name, float value, string desc)
        {
            return new Dictionary<string, string>
            {
                ["Parameter"] = name,
                ["Value"] = F(value),
                ["Description"] = desc,
            };
        }

        private static string F(float v) => v.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
    }
}
