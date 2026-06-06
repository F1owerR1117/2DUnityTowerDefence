using System.Collections.Generic;
using System.Reflection;
using DoudizhuTower.Gameplay.Entities;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 兵种被动技能调试窗口。
/// 菜单入口：Tools/技能可视化/被动技能调试窗口
///
/// 功能：
/// - 选择 Prefab 或运行时实例，查看/编辑所有被动技能参数
/// - 左侧面板：被动开关 + 范围滑条
/// - 右侧面板：属性预览 + VFX 缩放映射表
/// - Scene View 实时范围叠加（逻辑范围实线 + VFX 范围虚线）
/// - 编辑模式下修改 Prefab 参数直接保存
/// </summary>
public class UnitPassivesEditorWindow : EditorWindow
{
    // ─── Prefab 选择 ───
    private GameObject _selectedPrefab;
    private SerializedObject _soPassives;
    private SerializedObject _soUnit;

    // ─── 运行时实例 ───
    private UnitPassives _runtimePassives;
    private CardUnit _runtimeUnit;

    // ─── UI 缓存 ───
    private Vector2 _leftScroll;
    private Vector2 _rightScroll;
    private GUIStyle _headerStyle;
    private GUIStyle _subHeaderStyle;
    private bool _stylesInit;

    // ─── VFX 基准尺寸映射 ───
    private static readonly (string label, string field, float vfxBase, Color color)[] VfxMapping =
    {
        ("Splash",        "splashRadius",        2f, new Color(1f, 0.6f, 0f)),
        ("Shockwave",     "shockwaveRadius",     3f, new Color(1f, 0.2f, 0f)),
        ("KingAura",      "kingRadius",          3f, new Color(1f, 0.8f, 0f)),
        ("DeathExplosion","explosionRadius",      2f, new Color(1f, 0f, 0f)),
    };

    [MenuItem("Tools/技能可视化/被动技能调试窗口")]
    public static void ShowWindow()
    {
        var w = GetWindow<UnitPassivesEditorWindow>("被动技能调试");
        w.minSize = new Vector2(700, 500);
    }

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        SceneView.duringSceneGui += OnSceneGUI;
        OnSelectionChanged();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    // ─── 选择同步 ───

    private void OnSelectionChanged()
    {
        _runtimePassives = null;
        _runtimeUnit = null;
        _soPassives = null;
        _soUnit = null;
        _selectedPrefab = null;

        var go = Selection.activeGameObject;
        if (go == null) return;

        var p = go.GetComponent<UnitPassives>();
        var u = go.GetComponent<CardUnit>();
        if (p == null || u == null) return;

        if (Application.isPlaying)
        {
            _runtimePassives = p;
            _runtimeUnit = u;
        }
        else
        {
            _selectedPrefab = go;
            _soPassives = new SerializedObject(p);
            _soUnit = new SerializedObject(u);
        }

        Repaint();
        SceneView.RepaintAll();
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        OnSelectionChanged();
    }

    // ─── GUI ───

    private void InitStyles()
    {
        if (_stylesInit) return;
        _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        _subHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
        _stylesInit = true;
    }

    private void OnGUI()
    {
        InitStyles();

        // 无选择时显示提示
        if (!HasTarget())
        {
            EditorGUILayout.Space(40);
            EditorGUILayout.LabelField("请选择一个带有 UnitPassives + CardUnit 的 GameObject", _headerStyle);
            EditorGUILayout.LabelField("（Prefab 或运行时实例均可）", EditorStyles.miniLabel);
            return;
        }

        // 验证 SerializedObject 有效性（目标可能已被销毁）
        if (_soPassives != null && _soPassives.targetObject == null)
        { _soPassives = null; _soUnit = null; _selectedPrefab = null; return; }

        // 更新 SerializedObject
        _soPassives?.Update();
        _soUnit?.Update();

        // 整体包裹在 try-finally 中，确保所有 Begin/End 配对正确关闭
        EditorGUILayout.BeginHorizontal();
        try
        {
            // ── 左侧面板：被动配置 ──
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.55f));
            DrawLeftPanel();
            EditorGUILayout.EndVertical();

            // ── 分隔线 ──
            GUILayout.Box("", GUILayout.Width(1), GUILayout.ExpandHeight(true));

            // ── 右侧面板：属性 + VFX 映射 ──
            EditorGUILayout.BeginVertical();
            DrawRightPanel();
            EditorGUILayout.EndVertical();
        }
        finally
        {
            EditorGUILayout.EndHorizontal();
        }

        // 应用修改
        if (_soPassives != null && _soPassives.ApplyModifiedProperties())
            SceneView.RepaintAll();
        if (_soUnit != null && _soUnit.ApplyModifiedProperties())
            SceneView.RepaintAll();
    }

    private bool HasTarget() => _soPassives != null || _runtimePassives != null;

    // ─── 左侧：被动技能面板 ───

    private void DrawLeftPanel()
    {
        var go = _selectedPrefab ?? _runtimePassives?.gameObject;
        EditorGUILayout.LabelField(go?.name ?? "Unknown", _headerStyle);
        EditorGUILayout.Space(4);

        _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
        try
        {

        // 攻击范围
        EditorGUILayout.LabelField("─── 基础属性 ───", _subHeaderStyle);
        if (_soUnit != null)
        {
            var sp = _soUnit.FindProperty("_range");
            if (sp != null) EditorGUILayout.Slider(sp, 0.1f, 30f, new GUIContent("攻击范围 (Range)"));
        }
        else if (_runtimeUnit != null)
        {
            float r = _runtimeUnit.Stats.Range;
            float nr = EditorGUILayout.Slider("攻击范围 (Range)", r, 0.1f, 30f);
            if (!Mathf.Approximately(nr, r))
            {
                var s = _runtimeUnit.Stats;
                s.Range = nr;
                // Stats has protected set, use reflection to bypass access
                var prop = typeof(CardUnit).GetProperty("Stats", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                prop?.SetValue(_runtimeUnit, s);
            }
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("─── 被动技能 ───", _subHeaderStyle);

        // 通用被动
        DrawPassiveToggleAndSliders("点杀 (Sniper)", "enableSniper",
            ("sniperRangeBonus", "点杀搜索范围 (0=用索敌范围)", 0f, 50f),
            ("sniperHpThreshold", "血量阈值", 0f, 1f));

        DrawPassiveToggleAndSliders("人海连击 (Swarm)", "enableSwarm",
            ("swarmRadius", "感知半径", 0.5f, 10f),
            ("swarmDamagePct", "每友军伤害%", 0f, 2f));

        DrawPassiveToggleAndSliders("冲锋一击 (Charge)", "enableCharge",
            ("chargeMultiplier", "伤害倍率", 1f, 10f),
            ("chargeCooldown", "冷却(秒)", 1f, 30f),
            ("chargeSpeedMultiplier", "移速倍率", 1f, 3f));

        DrawPassiveToggleAndSliders("君王光环 (KingAura)", "enableKingAura",
            ("kingInterval", "间隔(秒)", 1f, 20f),
            ("kingRadius", "影响半径", 0.5f, 15f),
            ("kingPushDistance", "震退距离", 0.1f, 5f));

        DrawPassiveToggleAndSliders("盾墙 (ShieldWall)", "enableShieldWall",
            ("shieldRange", "影响半径", 0.5f, 15f),
            ("shieldDamageReduction", "减伤%", 0f, 1f));

        DrawPassiveToggleAndSliders("嘲讽 (Taunt)", "enableTaunt");

        DrawPassiveToggleAndSliders("死亡爆炸 (DeathExplosion)", "enableDeathExplosion",
            ("explosionRadius", "爆炸半径", 0.5f, 10f),
            ("explosionDamagePct", "伤害%", 0f, 3f));

        DrawPassiveToggleAndSliders("护盾吸收 (ShieldAbsorb)", "enableShieldAbsorb",
            ("shieldAmount", "护盾值", 0f, 2000f));

        DrawPassiveToggleAndSliders("减速光环 (SlowAura)", "enableSlowAura",
            ("slowRadius", "光环半径", 0.5f, 15f),
            ("slowPercent", "减速%", 0f, 1f),
            ("slowDuration", "持续(秒)", 0.5f, 10f));

        DrawPassiveToggleAndSliders("攻击眩晕 (StunOnHit)", "enableStunOnHit",
            ("stunDuration", "眩晕(秒)", 0.1f, 5f));

        DrawPassiveToggleAndSliders("撕裂 (Tear)", "enableTear",
            ("tearDamagePerStack", "每层增伤%", 0f, 0.5f),
            ("tearMaxStacks", "最大层数", 1, 20),
            ("tearDuration", "持续(秒)", 1f, 15f));

        DrawPassiveToggleAndSliders("出场震波 (Shockwave)", "enableShockwave",
            ("shockwaveRadius", "震波半径", 0.5f, 10f),
            ("shockwaveDamagePct", "伤害%", 0f, 2f));

        DrawPassiveToggleAndSliders("死亡燃烧 (BurnOnDeath)", "enableBurnOnDeath",
            ("burnRadius", "火海半径", 0.5f, 10f),
            ("burnDamagePct", "每秒伤害%", 0f, 1f),
            ("burnDuration", "持续(秒)", 1f, 15f));

        DrawPassiveToggleAndSliders("溅射攻击 (Splash)", "enableSplash",
            ("splashRadius", "溅射半径", 0.5f, 10f),
            ("splashDamagePct", "溅射伤害%", 0f, 2f));

        DrawPassiveToggleAndSliders("骑兵追远程 (CavalryChase)", "enableCavalryChase",
            ("cavalryChaseRangeThreshold", "远程阈值", 1f, 20f));
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    // ─── 被动技能行绘制 ───

    private void DrawPassiveToggleAndSliders(string title, string toggleField, params object[] sliders)
    {
        EditorGUILayout.Space(4);

        // Toggle
        bool enabled = false;
        if (_soPassives != null)
        {
            var sp = _soPassives.FindProperty(toggleField);
            if (sp != null)
            {
                sp.boolValue = EditorGUILayout.ToggleLeft(title, sp.boolValue);
                enabled = sp.boolValue;
            }
        }
        else if (_runtimePassives != null)
        {
            var fi = typeof(UnitPassives).GetField(toggleField);
            if (fi != null)
            {
                bool val = (bool)fi.GetValue(_runtimePassives);
                bool newVal = EditorGUILayout.ToggleLeft(title, val);
                if (newVal != val) fi.SetValue(_runtimePassives, newVal);
                enabled = newVal;
            }
        }

        if (!enabled) return;

        // Sliders
        EditorGUI.indentLevel++;
        foreach (var raw in sliders)
        {
            var (field, label, min, max, isInt) = ParseSliderDef(raw);
            if (_soPassives != null)
            {
                var sp = _soPassives.FindProperty(field);
                if (sp == null) continue;
                if (isInt)
                    EditorGUILayout.IntSlider(sp, (int)min, (int)max, new GUIContent(label));
                else
                    EditorGUILayout.Slider(sp, min, max, new GUIContent(label));
            }
            else if (_runtimePassives != null)
            {
                var fi = typeof(UnitPassives).GetField(field);
                if (fi == null) continue;
                if (isInt || fi.FieldType == typeof(int))
                {
                    int val = (int)fi.GetValue(_runtimePassives);
                    int nv = EditorGUILayout.IntSlider(label, val, (int)min, (int)max);
                    if (nv != val) fi.SetValue(_runtimePassives, nv);
                }
                else
                {
                    float val = (float)fi.GetValue(_runtimePassives);
                    float nv = EditorGUILayout.Slider(label, val, min, max);
                    if (nv != val) fi.SetValue(_runtimePassives, nv);
                }
            }
        }
        EditorGUI.indentLevel--;
    }

    private static (string field, string label, float min, float max, bool isInt) ParseSliderDef(object raw)
    {
        // 支持 (string, string, float, float) 和 (string, string, int, int) 元组
        if (raw is (string f, string l, float mn, float mx))
            return (f, l, mn, mx, false);
        if (raw is (string fi, string la, int mi, int ma))
            return (fi, la, mi, ma, true);
        return ("", "", 0, 0, false);
    }

    // ─── 右侧：属性预览 + VFX 映射 ───

    private void DrawRightPanel()
    {
        _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
        try
        {

        // 属性预览
        EditorGUILayout.LabelField("─── 属性预览 ───", _subHeaderStyle);
        if (_runtimeUnit != null)
        {
            var s = _runtimeUnit.Stats;
            EditorGUILayout.LabelField($"HP: {s.HP:F0}  |  ATK: {s.ATK:F1}  |  Range: {s.Range:F1}");
            EditorGUILayout.LabelField($"MoveSpeed: {s.MoveSpeed:F1}  |  AtkInterval: {s.AttackInterval:F2}s");
            EditorGUILayout.LabelField($"Height: {_runtimeUnit.UnitHeight}");
        }
        else if (_soUnit != null)
        {
            var hp = _soUnit.FindProperty("_hp")?.floatValue ?? 0;
            var atk = _soUnit.FindProperty("_atk")?.floatValue ?? 0;
            var range = _soUnit.FindProperty("_range")?.floatValue ?? 0;
            var speed = _soUnit.FindProperty("_moveSpeed")?.floatValue ?? 0;
            var interval = _soUnit.FindProperty("_attackInterval")?.floatValue ?? 0;
            EditorGUILayout.LabelField($"HP: {hp:F0}  |  ATK: {atk:F1}  |  Range: {range:F1}");
            EditorGUILayout.LabelField($"MoveSpeed: {speed:F1}  |  AtkInterval: {interval:F2}s");
        }

        EditorGUILayout.Space(12);

        // VFX 缩放映射表
        EditorGUILayout.LabelField("─── VFX 缩放映射 ───", _subHeaderStyle);
        EditorGUILayout.LabelField("公式: VFX Scale = Radius / BaseSize", EditorStyles.miniLabel);
        EditorGUILayout.Space(4);

        // 表头
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("被动", EditorStyles.miniLabel, GUILayout.Width(100));
        GUILayout.Label("逻辑半径", EditorStyles.miniLabel, GUILayout.Width(70));
        GUILayout.Label("VFX Base", EditorStyles.miniLabel, GUILayout.Width(65));
        GUILayout.Label("VFX Scale", EditorStyles.miniLabel, GUILayout.Width(65));
        GUILayout.Label("匹配?", EditorStyles.miniLabel, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();

        foreach (var (label, field, vfxBase, color) in VfxMapping)
        {
            float logicRadius = GetFloatField(field);
            if (logicRadius <= 0f) continue;

            float vfxScale = logicRadius / vfxBase;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(100));
            GUILayout.Label($"{logicRadius:F2}", GUILayout.Width(70));
            GUILayout.Label($"{vfxBase:F0}", GUILayout.Width(65));
            GUILayout.Label($"{vfxScale:F2}x", GUILayout.Width(65));

            // 匹配状态（当前公式下始终匹配，但提示用户检查预制体）
            var oldColor = GUI.color;
            GUI.color = new Color(0.5f, 1f, 0.5f);
            GUILayout.Label("OK", GUILayout.Width(50));
            GUI.color = oldColor;

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "VFX 实际视觉大小取决于预制体粒子系统的基础尺寸。\n" +
            "如果 VFX 看起来比逻辑范围大/小，请调整 UnitVFX 中的缩放除数（如 radius/2f 改为 radius/3f）。",
            MessageType.Info);

        EditorGUILayout.Space(12);

        // 场景预览控制
        EditorGUILayout.LabelField("─── 场景预览 ───", _subHeaderStyle);
        EditorGUILayout.LabelField("选中 GameObject 后，Scene View 自动显示范围叠加。", EditorStyles.miniLabel);

        if (!Application.isPlaying && _selectedPrefab != null)
        {
            if (GUILayout.Button("在场景中预览实例 (临时)"))
                SpawnPreviewInstance();
        }
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    private float GetFloatField(string fieldName)
    {
        if (_soPassives != null)
        {
            var sp = _soPassives.FindProperty(fieldName);
            return sp?.floatValue ?? 0f;
        }
        if (_runtimePassives != null)
        {
            var fi = typeof(UnitPassives).GetField(fieldName);
            return fi != null ? (float)fi.GetValue(_runtimePassives) : 0f;
        }
        return 0f;
    }

    // ─── 预览实例 ───

    private GameObject _previewInstance;

    private void SpawnPreviewInstance()
    {
        DestroyPreviewInstance();
        if (_selectedPrefab == null) return;

        _previewInstance = (GameObject)PrefabUtility.InstantiatePrefab(_selectedPrefab);
        _previewInstance.name = "[Preview] " + _selectedPrefab.name;
        _previewInstance.transform.position = Vector3.zero;
        // 禁用碰撞和物理
        foreach (var col in _previewInstance.GetComponentsInChildren<Collider2D>())
            col.enabled = false;
        // 标记为编辑器临时对象
        _previewInstance.hideFlags = HideFlags.HideAndDontSave;
        // 禁用所有 MonoBehaviour 的 Update（仅保留 Gizmos 绘制）
        foreach (var mb in _previewInstance.GetComponentsInChildren<MonoBehaviour>())
        {
            if (mb != null) mb.enabled = false;
        }
        // 启用 UnitPassives 的 OnDrawGizmos（Gizmos 始终绘制，不受 enabled 影响）

        Selection.activeGameObject = _previewInstance;
        SceneView.RepaintAll();
    }

    private void DestroyPreviewInstance()
    {
        if (_previewInstance != null)
        {
            var go = _previewInstance;
            _previewInstance = null;
            DestroyImmediate(go);
        }
    }

    // ─── Scene View 叠加 ───

    private void OnSceneGUI(SceneView sv)
    {
        // 叠加由 UnitPassivesGizmosOverlay 自动处理
        // 这里额外绘制预览实例的范围
        if (_previewInstance == null) return;

        var unit = _previewInstance.GetComponent<CardUnit>();
        var passives = _previewInstance.GetComponent<UnitPassives>();
        if (unit == null || passives == null) return;

        // 预览实例的位置
        var pos = _previewInstance.transform.position;

        // 绘制标签
        var style = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = Color.yellow },
            fontSize = 13
        };
        Handles.Label(pos + Vector3.up * 3f, "[Preview] " + _previewInstance.name, style);
    }

    private void OnDestroy()
    {
        DestroyPreviewInstance();
    }
}
