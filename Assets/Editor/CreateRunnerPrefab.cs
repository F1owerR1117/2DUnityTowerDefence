using UnityEngine;
using UnityEditor;
using Fusion;

namespace DoudizhuTower.Editor
{
    public static class CreateRunnerPrefab
    {
        [MenuItem("Fusion/创建 Runner Prefab")]
        public static void Create()
        {
            var go = new GameObject("NetworkRunner");
            go.AddComponent<NetworkRunner>();

            string folder = "Assets/Prefabs";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            string path = $"{folder}/NetworkRunnerPrefab.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            Debug.Log($"[Fusion] Runner Prefab 已创建: {path}");
            Debug.Log("[Fusion] 将此预制体拖入 FusionBootstrap 的 RunnerPrefab 字段");

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
    }
}