using System.Collections.Generic;
using DoudizhuTower.Gameplay.Entities;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 在 Scene View 中增强显示兵种被动技能的逻辑范围与 VFX 覆盖范围。
/// 选中带有 UnitPassives 的 GameObject 时自动绘制。
///
/// - 实线圆 = 逻辑范围（Physics2D.OverlapCircle 使用的半径）
/// - 虚线圆 = VFX 视觉覆盖范围（根据 UnitVFX 缩放公式反推）
/// - 文字标注 = 范围数值
///
/// 可通过 Tools 菜单开关。
/// </summary>
[InitializeOnLoad]
public static class UnitPassivesGizmosOverlay
{
    private static bool _enabled = true;

    // VFX 预制体基准尺寸（与 UnitVFX 中的缩放除数一致）
    private const float VfxBaseSplash = 2f;
    private const float VfxBaseShockwave = 3f;
    private const float VfxBaseKingAura = 3f;
    private const float VfxBaseDeathExplosion = 2f;

    static UnitPassivesGizmosOverlay()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    [MenuItem("Tools/技能可视化/范围叠加 (Scene View)")]
    private static void ToggleOverlay()
    {
        _enabled = !_enabled;
        Menu.SetChecked("Tools/技能可视化/范围叠加 (Scene View)", _enabled);
        SceneView.RepaintAll();
    }

    [MenuItem("Tools/技能可视化/范围叠加 (Scene View)", true)]
    private static bool ToggleOverlayValidate()
    {
        Menu.SetChecked("Tools/技能可视化/范围叠加 (Scene View)", _enabled);
        return true;
    }

    private static void OnSceneGUI(SceneView sv)
    {
        if (!_enabled) return;

        foreach (var go in Selection.gameObjects)
        {
            var unit = go.GetComponent<CardUnit>();
            var passives = go.GetComponent<UnitPassives>();
            if (unit == null || passives == null) continue;

            DrawPassiveOverlay(unit, passives);
        }
    }

    private static void DrawPassiveOverlay(CardUnit unit, UnitPassives p)
    {
        Vector3 center = unit.VisualCenter;

        // 攻击范围（CardUnit._range，边缘到边缘）
        float attackRange = unit.Stats.Range;
        if (attackRange > 0f)
        {
            Handles.color = new Color(1f, 0.2f, 0.2f, 0.6f);
            Handles.DrawWireDisc(center, Vector3.forward, attackRange);
            DrawLabel(center, Vector3.up * (attackRange + 0.3f), $"ATK Range: {attackRange:F1}", new Color(1f, 0.4f, 0.4f));
        }

        // 被动范围列表
        var entries = new List<PassiveEntry>();

        if (p.enableSwarm)
            entries.Add(new PassiveEntry("Swarm", p.swarmRadius, VfxBaseSplash, new Color(0f, 1f, 1f, 0.5f)));
        if (p.enableKingAura)
            entries.Add(new PassiveEntry("KingAura", p.kingRadius, VfxBaseKingAura, new Color(1f, 0.8f, 0f, 0.5f)));
        if (p.enableShieldWall)
            entries.Add(new PassiveEntry("ShieldWall", p.shieldRange, 0f, new Color(0.2f, 0.5f, 1f, 0.5f)));
        if (p.enableSlowAura)
            entries.Add(new PassiveEntry("SlowAura", p.slowRadius, 0f, new Color(0f, 0.5f, 1f, 0.5f)));
        if (p.enableShockwave)
            entries.Add(new PassiveEntry("Shockwave", p.shockwaveRadius, VfxBaseShockwave, new Color(1f, 0.2f, 0f, 0.5f)));
        if (p.enableBurnOnDeath)
            entries.Add(new PassiveEntry("BurnZone", p.burnRadius, 0f, new Color(1f, 0.5f, 0f, 0.5f)));
        if (p.enableSplash)
            entries.Add(new PassiveEntry("Splash", p.splashRadius, VfxBaseSplash, new Color(1f, 0.6f, 0f, 0.5f)));
        if (p.enableDeathExplosion)
            entries.Add(new PassiveEntry("DeathExplosion", p.explosionRadius, VfxBaseDeathExplosion, new Color(1f, 0f, 0f, 0.4f)));
        if (p.enableSniper)
        {
            float sniperR = p.sniperRangeBonus > 0f ? p.sniperRangeBonus : (unit != null ? unit.DetectionRange : attackRange);
            entries.Add(new PassiveEntry("Sniper", sniperR, 0f, new Color(1f, 0f, 0f, 0.25f)));
        }

        // 按半径排序，大的画在后面
        entries.Sort((a, b) => a.Radius.CompareTo(b.Radius));

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            // 逻辑范围（实线）
            Handles.color = e.Color;
            Handles.DrawWireDisc(center, Vector3.forward, e.Radius);

            // VFX 覆盖范围（虚线）
            if (e.VfxBaseSize > 0f)
            {
                float vfxRadius = e.Radius; // 缩放后 VFX 实际覆盖半径
                Handles.color = new Color(e.Color.r, e.Color.g, e.Color.b, 0.25f);
                DrawDashedCircle(center, vfxRadius, 20);

                // 如果 VFX 与逻辑不匹配，用警告色标注
                // （当前公式下理论上一致，但预制体粒子大小可能不同）
            }

            // 标签（偏移避免重叠）
            Vector3 offset = Vector3.up * (e.Radius + 0.25f + i * 0.35f);
            string label = $"{e.Name}: {e.Radius:F1}";
            if (e.VfxBaseSize > 0f)
                label += $" (VFX base={e.VfxBaseSize:F0})";
            DrawLabel(center, offset, label, e.Color);
        }
    }

    private static void DrawLabel(Vector3 center, Vector3 offset, string text, Color color)
    {
        var style = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = color },
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter
        };
        Handles.Label(center + offset, text, style);
    }

    private static void DrawDashedCircle(Vector3 center, float radius, int segments)
    {
        float step = 360f / segments;
        for (int i = 0; i < segments; i += 2)
        {
            float a1 = step * i * Mathf.Deg2Rad;
            float a2 = step * (i + 1) * Mathf.Deg2Rad;
            Vector3 p1 = center + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * radius;
            Vector3 p2 = center + new Vector3(Mathf.Cos(a2), Mathf.Sin(a2), 0) * radius;
            Handles.DrawDottedLine(p1, p2, 4f);
        }
    }

    private readonly struct PassiveEntry
    {
        public readonly string Name;
        public readonly float Radius;
        public readonly float VfxBaseSize;
        public readonly Color Color;

        public PassiveEntry(string name, float radius, float vfxBaseSize, Color color)
        {
            Name = name;
            Radius = radius;
            VfxBaseSize = vfxBaseSize;
            Color = color;
        }
    }
}
