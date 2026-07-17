using System.Collections;
using TMPro;
using UnityEngine;
using DoudizhuTower.Gameplay.Entities;

namespace DoudizhuTower.Gameplay.Presentation
{
    /// <summary>
    /// Boss 对话气泡：世界空间 UI，跟随 Boss 头顶。
    /// 支持打字机效果、跳过、自动隐藏。
    /// </summary>
    public class BossDialogueBubble : MonoBehaviour
    {
        [Header("UI 引用")]
        [Tooltip("控制对话框淡入淡出的 CanvasGroup")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("说话人名字文本")]
        [SerializeField] private TextMeshProUGUI speakerText;
        [Tooltip("对话内容文本")]
        [SerializeField] private TextMeshProUGUI contentText;

        [Header("配置")]
        [Tooltip("对话框相对 Boss 头顶的偏移量")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 2f, 0f);
        [Tooltip("打字机效果：每个字符显示间隔（秒）")]
        [SerializeField] private float typewriterSpeed = 0.03f;
        [Tooltip("淡入淡出动画时长（秒）")]
        [SerializeField] private float fadeDuration = 0.2f;

        private Camera _cam;
        private Coroutine _currentRoutine;
        private bool _skipRequested;

        private void Awake()
        {
            _cam = Camera.main;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        /// <summary>订阅演出管理器事件（由 GameBootstrapper 调用）</summary>
        public void Initialize()
        {
            var mgr = BattlePresentationManager.Instance;
            if (mgr != null)
            {
                mgr.OnDialogueStart += HandleDialogueStart;
                mgr.OnDialogueEnd += HandleDialogueEnd;
            }

            // 监听 Boss 技能对话
            var skillSystem = GetComponentInParent<BossSkillSystem>();
            if (skillSystem != null)
                skillSystem.OnSkillActivated += HandleSkillDialogue;
        }

        private void HandleDialogueStart(DialogueLine line)
        {
            Show(line.speaker, line.content, line.duration, line.waitForClick);
        }

        private void HandleDialogueEnd()
        {
            Hide();
        }

        private void HandleSkillDialogue(string speaker, string text)
        {
            Show(speaker, text, 2f, false, showSpeaker: false);
        }

        private void OnDestroy()
        {
            var mgr = BattlePresentationManager.Instance;
            if (mgr != null)
            {
                mgr.OnDialogueStart -= HandleDialogueStart;
                mgr.OnDialogueEnd -= HandleDialogueEnd;
            }

            var skillSystem = GetComponentInParent<BossSkillSystem>();
            if (skillSystem != null)
                skillSystem.OnSkillActivated -= HandleSkillDialogue;
        }

        private void LateUpdate()
        {
            // 始终面向摄像机
            if (_cam != null && canvasGroup != null && canvasGroup.alpha > 0f)
            {
                transform.LookAt(transform.position + _cam.transform.forward);
            }
        }

        /// <summary>显示对话（逐字打字机效果）</summary>
        public void Show(string speaker, string content, float duration, bool waitForClick, bool showSpeaker = true)
        {
            // 强制中断上一个对话，重置状态
            if (_currentRoutine != null) StopCoroutine(_currentRoutine);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (contentText != null) contentText.text = "";

            if (speakerText != null)
            {
                speakerText.gameObject.SetActive(showSpeaker);
                speakerText.text = speaker;
            }
            _skipRequested = false;
            _currentRoutine = StartCoroutine(ShowCoroutine(content, duration, waitForClick));
        }

        /// <summary>跳过当前对话</summary>
        public void Skip()
        {
            _skipRequested = true;
        }

        /// <summary>立即隐藏</summary>
        public void Hide()
        {
            if (_currentRoutine != null) StopCoroutine(_currentRoutine);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        private IEnumerator ShowCoroutine(string content, float duration, bool waitForClick)
        {
            // 淡入
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                float fadeElapsed = 0f;
                while (fadeElapsed < fadeDuration)
                {
                    fadeElapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = fadeElapsed / fadeDuration;
                    yield return null;
                }
                canvasGroup.alpha = 1f;
            }

            // 打字机效果
            if (contentText != null)
            {
                contentText.text = "";
                for (int i = 0; i < content.Length; i++)
                {
                    if (_skipRequested) { contentText.text = content; break; }
                    contentText.text += content[i];
                    yield return new WaitForSecondsRealtime(typewriterSpeed);
                }
            }

            // 等待点击或自动消失
            if (waitForClick)
            {
                yield return new WaitUntil(() => _skipRequested || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space));
            }
            else
            {
                float waitElapsed = 0f;
                while (waitElapsed < duration && !_skipRequested)
                {
                    waitElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            // 淡出
            if (canvasGroup != null)
            {
                float fadeElapsed = 0f;
                while (fadeElapsed < fadeDuration)
                {
                    fadeElapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = 1f - (fadeElapsed / fadeDuration);
                    yield return null;
                }
                canvasGroup.alpha = 0f;
            }
        }
    }
}
