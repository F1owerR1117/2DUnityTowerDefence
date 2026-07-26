using UnityEngine;

namespace DoudizhuTower.UI.Dialogue
{
    /// <summary>
    /// 场景进入对话触发器。
    /// 挂载到场景中，场景加载后自动触发对话。
    ///
    /// 使用方法：
    /// 1. 在场景中创建空 GameObject，挂载此组件
    /// 2. 拖入 DialogueBox 引用
    /// 3. 拖入要播放的 DialogueData
    /// 4. 场景加载后自动触发对话
    /// </summary>
    public class DialogueTrigger : MonoBehaviour
    {
        [Header("对话系统")]
        [Tooltip("对话框控制器（自动查找同场景中的）")]
        [SerializeField] private DialogueBox dialogueBox;

        [Tooltip("要播放的对话数据")]
        [SerializeField] private DialogueData dialogueData;

        [Header("触发设置")]
        [Tooltip("延迟触发时间（秒）")]
        [SerializeField] private float delay = 0.5f;

        [Tooltip("是否在 Start 时自动触发")]
        [SerializeField] private bool autoTrigger = true;

        [Tooltip("是否只触发一次（跨场景持久化）")]
        [SerializeField] private bool triggerOnce = true;

        private static string _lastTriggeredID;
        private string _triggerID;

        private void Start()
        {
            _triggerID = gameObject.scene.name + "_" + gameObject.name;

            if (triggerOnce && _lastTriggeredID == _triggerID)
            {
                Debug.Log($"[DialogueTrigger] 已触发过，跳过: {_triggerID}");
                return;
            }

            if (autoTrigger && dialogueData != null)
            {
                TriggerDialogue();
            }
        }

        /// <summary>
        /// 手动触发对话。
        /// </summary>
        public void TriggerDialogue()
        {
            if (dialogueBox == null)
            {
                dialogueBox = FindFirstObjectByType<DialogueBox>();
            }

            if (dialogueBox == null)
            {
                Debug.LogError("[DialogueTrigger] 未找到 DialogueBox");
                return;
            }

            if (dialogueData == null)
            {
                Debug.LogWarning("[DialogueTrigger] DialogueData 为空");
                return;
            }

            if (delay > 0f)
            {
                StartCoroutine(DelayedTrigger());
            }
            else
            {
                ExecuteTrigger();
            }
        }

        private System.Collections.IEnumerator DelayedTrigger()
        {
            yield return new WaitForSeconds(delay);
            ExecuteTrigger();
        }

        private void ExecuteTrigger()
        {
            _lastTriggeredID = _triggerID;
            dialogueBox.Show(dialogueData);
        }
    }
}
