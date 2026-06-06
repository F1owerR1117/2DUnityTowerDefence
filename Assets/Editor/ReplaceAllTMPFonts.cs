using TMPro;
using UnityEditor;
using UnityEngine;

public static class ReplaceAllTMPFonts
{
    private static int _pendingCleanup;

    [MenuItem("Tools/替换场景所有 TMP 字体")]
    private static void Replace()
    {
        var font = Selection.activeObject as TMP_FontAsset;
        if (font == null)
        {
            Debug.LogError("请先选中一个 TMP_FontAsset 再运行此工具");
            return;
        }

        // 清空字体 Fallback，从源头阻止 SubMeshUI 生成
        if (font.fallbackFontAssetTable != null && font.fallbackFontAssetTable.Count > 0)
        {
            font.fallbackFontAssetTable.Clear();
            EditorUtility.SetDirty(font);
        }

        var allTexts = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var allMeshTexts = Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int total = allTexts.Length + allMeshTexts.Length;
        int count = 0;

        try
        {
            int index = 0;
            foreach (var t in allTexts)
            {
                EditorUtility.DisplayProgressBar("替换 TMP 字体", $"UGUI: {t.name}", (float)index / total);
                Undo.RecordObject(t, "Replace TMP Font");
                t.font = font;
                t.fontSharedMaterial = font.material;
                t.ForceMeshUpdate();
                EditorUtility.SetDirty(t);
                count++;
                index++;
            }

            foreach (var t in allMeshTexts)
            {
                EditorUtility.DisplayProgressBar("替换 TMP 字体", $"3D: {t.name}", (float)index / total);
                Undo.RecordObject(t, "Replace TMP Font");
                t.font = font;
                t.fontSharedMaterial = font.material;
                t.ForceMeshUpdate();
                EditorUtility.SetDirty(t);
                count++;
                index++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"已替换 {count} 个 TextMeshPro 字体为 {font.name}");

        // 延迟清理：TMP 重建完成后销毁残留 SubMeshUI
        _pendingCleanup = 3;
        EditorApplication.delayCall += DelayedCleanup;
    }

    private static void DelayedCleanup()
    {
        int destroyed = 0;

        // 先销毁脚本组件，再销毁 GameObject
        var subs = Object.FindObjectsByType<TMP_SubMeshUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var sub in subs)
        {
            var go = sub.gameObject;
            Object.DestroyImmediate(sub);      // 先移除脚本
            Object.DestroyImmediate(go);        // 再销毁对象
            destroyed++;
        }

        var meshSubs = Object.FindObjectsByType<TMP_SubMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var sub in meshSubs)
        {
            var go = sub.gameObject;
            Object.DestroyImmediate(sub);
            Object.DestroyImmediate(go);
            destroyed++;
        }

        _pendingCleanup--;
        if (destroyed > 0 && _pendingCleanup > 0)
        {
            // 还有残留，继续清理
            EditorApplication.delayCall += DelayedCleanup;
            Debug.Log($"[替换字体] 延迟清理: 销毁 {destroyed} 个 SubMeshUI，继续清理...");
        }
        else
        {
            Debug.Log($"[替换字体] 延迟清理完成，共销毁 {destroyed} 个 SubMeshUI");
        }
    }
}
