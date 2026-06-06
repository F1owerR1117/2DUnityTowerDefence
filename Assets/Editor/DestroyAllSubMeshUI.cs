using TMPro;
using UnityEditor;
using UnityEngine;

public static class DestroyAllSubMeshUI
{
    [MenuItem("Tools/销毁所有 TMP SubMeshUI")]
    private static void Execute()
    {
        int destroyed = 0;

        // 第一步：遍历所有父级 TMP，清空文本释放引用
        var parents = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var parent in parents)
        {
            string text = parent.text;
            parent.text = "";
            parent.ForceMeshUpdate();

            // 销毁子对象中的 SubMeshUI 组件
            foreach (var sub in parent.GetComponentsInChildren<TMP_SubMeshUI>(true))
            {
                // 标记为不可保存，防止 Unity 保留
                sub.gameObject.hideFlags = HideFlags.HideAndDontSave;
                Object.DestroyImmediate(sub);
                destroyed++;
            }

            // 销毁子对象中的 SubMesh 组件
            foreach (var sub in parent.GetComponentsInChildren<TMP_SubMesh>(true))
            {
                sub.gameObject.hideFlags = HideFlags.HideAndDontSave;
                Object.DestroyImmediate(sub);
                destroyed++;
            }

            // 恢复文本
            parent.text = text;
            parent.ForceMeshUpdate();
        }

        // 第二步：全局扫描残留（防止遗漏）
        var orphans = Object.FindObjectsByType<TMP_SubMeshUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var sub in orphans)
        {
            sub.gameObject.hideFlags = HideFlags.HideAndDontSave;
            Object.DestroyImmediate(sub);
            destroyed++;
        }

        var orphans3D = Object.FindObjectsByType<TMP_SubMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var sub in orphans3D)
        {
            sub.gameObject.hideFlags = HideFlags.HideAndDontSave;
            Object.DestroyImmediate(sub);
            destroyed++;
        }

        // 第三步：清理标记为 HideAndDontSave 的空 GameObject
        parents = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var parent in parents)
        {
            for (int i = parent.transform.childCount - 1; i >= 0; i--)
            {
                var child = parent.transform.GetChild(i).gameObject;
                if ((child.hideFlags & HideFlags.HideAndDontSave) != 0)
                    Object.DestroyImmediate(child);
            }
        }

        Debug.Log($"[DestroyAllSubMeshUI] 已销毁 {destroyed} 个 SubMeshUI/SubMesh 组件");
    }
}
