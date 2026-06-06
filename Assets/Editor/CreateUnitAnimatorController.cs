using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class CreateUnitAnimatorController
{
    [MenuItem("Tools/创建兵种 Animator Controller")]
    private static void Create()
    {
        string dir = "Assets/Animations";
        string ctrlPath = $"{dir}/UnitBaseController.controller";

        if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath) != null)
        {
            Debug.Log("[Animator] UnitBaseController 已存在，跳过创建");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(ctrlPath);
            return;
        }

        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Animations");

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);

        // 参数
        ctrl.AddParameter("State", AnimatorControllerParameterType.Int);
        string[] triggers = { "Death", "Shockwave", "Splash", "StunHit", "KingAura", "DeathExplosion", "Burn", "Summon" };
        foreach (var t in triggers)
            ctrl.AddParameter(t, AnimatorControllerParameterType.Trigger);
        string[] bools = { "Charge", "Taunt", "ShieldWall" };
        foreach (var b in bools)
            ctrl.AddParameter(b, AnimatorControllerParameterType.Bool);

        var sm = ctrl.layers[0].stateMachine;

        // 基础状态（每个状态独立命名 clip，供 SimpleAnimator Override 匹配）
        var idle = sm.AddState("Idle");
        idle.motion = CreateNamedClip(ctrlPath, "idle");
        var walk = sm.AddState("Walk");
        walk.motion = CreateNamedClip(ctrlPath, "walk");
        var attack = sm.AddState("Attack");
        attack.motion = CreateNamedClip(ctrlPath, "attack");
        sm.defaultState = idle;

        AddIntTransition(idle, walk, "State", 1);
        AddIntTransition(idle, attack, "State", 2);
        AddIntTransition(walk, idle, "State", 0);
        AddIntTransition(walk, attack, "State", 2);
        AddIntTransition(attack, idle, "State", 0);
        AddIntTransition(attack, walk, "State", 1);

        // Trigger 特效状态
        foreach (var triggerName in triggers)
        {
            var state = sm.AddState(triggerName);
            state.motion = CreateNamedClip(ctrlPath, triggerName.ToLower());

            var transition = sm.AddAnyStateTransition(state);
            transition.AddCondition(AnimatorConditionMode.If, 0, triggerName);
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.duration = 0.1f;
            transition.hasFixedDuration = true;

            var exitTransition = state.AddTransition(idle);
            exitTransition.hasExitTime = true;
            exitTransition.exitTime = 1f;
            exitTransition.duration = 0.1f;
            exitTransition.hasFixedDuration = true;
        }

        // Charge 状态（定向转换，避免 Any State 反复触发）
        var charge = sm.AddState("Charge");
        charge.motion = CreateNamedClip(ctrlPath, "charge");

        // Idle/Walk → Charge
        AddBoolTransition(idle, charge, "Charge", true);
        AddBoolTransition(walk, charge, "Charge", true);

        // Charge → Idle/Walk/Attack（duration=0 即时切换）
        AddBoolTransition(charge, idle, "Charge", false);
        AddBoolTransition(charge, walk, "Charge", false);
        AddIntTransition(charge, attack, "State", 2);

        // 其他 Bool 特效状态（Taunt、ShieldWall 用 Any State）
        foreach (var boolName in new[] { "Taunt", "ShieldWall" })
        {
            var state = sm.AddState(boolName);
            state.motion = CreateNamedClip(ctrlPath, boolName.ToLower());

            var enterTransition = sm.AddAnyStateTransition(state);
            enterTransition.AddCondition(AnimatorConditionMode.If, 0, boolName);
            enterTransition.hasExitTime = false;
            enterTransition.duration = 0.1f;
            enterTransition.hasFixedDuration = true;

            var exitTransition = state.AddTransition(idle);
            exitTransition.AddCondition(AnimatorConditionMode.IfNot, 0, boolName);
            exitTransition.hasExitTime = false;
            exitTransition.duration = 0.1f;
            exitTransition.hasFixedDuration = true;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Animator] 已创建 {ctrlPath}，含 3 个基础状态 + {triggers.Length} 个 Trigger 特效 + {bools.Length} 个 Bool 特效");
        Selection.activeObject = ctrl;
    }

    /// <summary>
    /// 创建命名的空占位 Clip，嵌入 Controller 资产。
    /// 名称包含状态关键字，供 SimpleAnimator 的 Override 匹配使用。
    /// </summary>
    private static AnimationClip CreateNamedClip(string ctrlPath, string stateName)
    {
        var clip = new AnimationClip { name = $"{stateName}_placeholder", frameRate = 1 };
        clip.SetCurve("", typeof(Transform), "localPosition.x", new AnimationCurve(new Keyframe(0, 0)));
        AssetDatabase.AddObjectToAsset(clip, ctrlPath);
        return clip;
    }

    private static void AddIntTransition(AnimatorState from, AnimatorState to, string param, int value)
    {
        var t = from.AddTransition(to);
        t.AddCondition(AnimatorConditionMode.Equals, value, param);
        t.hasExitTime = false;
        t.duration = 0;
        t.hasFixedDuration = false;
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, string param, bool value)
    {
        var t = from.AddTransition(to);
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, param);
        t.hasExitTime = false;
        t.duration = 0;
        t.hasFixedDuration = false;
    }
}
