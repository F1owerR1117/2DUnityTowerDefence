using UnityEngine;
using Fusion;

namespace DoudizhuTower.Gameplay.Network
{
    /// <summary>
    /// NetworkRunner 预制体设置工具。
    /// 用于创建 FusionBootstrap 所需的 RunnerPrefab。
    /// </summary>
    [RequireComponent(typeof(NetworkRunner))]
    public class NetworkRunnerSetup : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private bool autoConfigure = true;

        private NetworkRunner _runner;

        private void Awake()
        {
            if (!autoConfigure) return;

            _runner = GetComponent<NetworkRunner>();
            if (_runner == null)
            {
                _runner = gameObject.AddComponent<NetworkRunner>();
            }

            // 基础配置
            _runner.ProvideInput = true;

            Debug.Log("[NetworkRunnerSetup] Runner 预制体已配置");
        }

        /// <summary>
        /// 创建 Runner 预制体（在 Editor 中使用）
        /// </summary>
        [ContextMenu("创建 Runner 预制体")]
        public void CreatePrefab()
        {
            #if UNITY_EDITOR
            // 创建临时 GameObject
            var tempGo = new GameObject("NetworkRunnerPrefab");
            var runner = tempGo.AddComponent<NetworkRunner>();
            runner.ProvideInput = true;

            // 保存为预制体
            string prefabPath = "Assets/Prefabs/NetworkRunnerPrefab.prefab";
            var prefab = UnityEditor.PrefabUtility.SaveAsPrefabAsset(tempGo, prefabPath);
            
            // 删除临时对象
            DestroyImmediate(tempGo);

            Debug.Log($"[NetworkRunnerSetup] 预制体已创建: {prefabPath}");
            Debug.Log("[NetworkRunnerSetup] 将此预制体拖入 FusionBootstrap 的 RunnerPrefab 字段");
            #endif
        }
    }
}