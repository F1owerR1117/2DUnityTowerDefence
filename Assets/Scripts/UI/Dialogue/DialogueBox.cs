using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoudizhuTower.UI.Dialogue
{
    /// <summary>
    /// 对话框 UI 控制器。
    /// 支持打字机效果、立绘展示、全区域点击继续、键盘快捷键。
    ///
    /// 使用方法：
    /// 1. 挂载到场景 Canvas 下的 GameObject
    /// 2. 在 Inspector 中配置所有 UI 引用
    /// 3. 调用 Show(DialogueData) 开始播放对话
    /// </summary>
    public class DialogueBox : MonoBehaviour
    {
        #region Inspector 字段

        [Header("UI 引用")]
        [SerializeField] private GameObject dialogueRoot;
        [SerializeField] private TextMeshProUGUI speakerNameText;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private Image portraitImage;
        [SerializeField] private GameObject portraitPlaceholder;
        [SerializeField] private TextMeshProUGUI placeholderText;
        [SerializeField] private Button skipButton;

        [Header("点击区域")]
        [Tooltip("挂载到整个对话框覆盖层，用于接收点击事件")]
        [SerializeField] private GraphicRaycaster clickArea;

        [Header("配置")]
        [Tooltip("默认打字速度（秒/字）")]
        [SerializeField] private float defaultTypeSpeed = 0.03f;

        [Tooltip("打字完成后自动等待时间（秒），0 = 等待手动点击")]
        [SerializeField] private float autoWaitTime = 0f;

        [Header("立绘默认值")]
        [Tooltip("默认立绘宽度")]
        [SerializeField] private float defaultPortraitWidth = 4f;
        [Tooltip("默认立绘高度")]
        [SerializeField] private float defaultPortraitHeight = 6f;

        #endregion

        #region 私有字段

        private DialogueData _currentData;
        private int _currentLineIndex;
        private Coroutine _typewriterCoroutine;
        private bool _isTyping;
        private bool _isActive;
        private string _fullText;
        private Action _onComplete;
        private float _lastClickTime;
        private const float CLICK_COOLDOWN = 0.2f; // 防止连续点击

        #endregion

        #region 公开属性

        /// <summary>对话框是否正在显示</summary>
        public bool IsActive => _isActive;

        /// <summary>当前是否正在打字</summary>
        public bool IsTyping => _isTyping;

        #endregion

        #region 事件

        /// <summary>对话开始播放</summary>
        public event Action OnDialogueStarted;

        /// <summary>单行对话开始</summary>
        public event Action<int, DialogueLine> OnLineStarted;

        /// <summary>单行对话完成（打字结束）</summary>
        public event Action<int, DialogueLine> OnLineCompleted;

        /// <summary>整个对话序列播放完毕</summary>
        public event Action OnDialogueCompleted;

        #endregion

        #region 生命周期

        private void Awake()
        {
            if (dialogueRoot != null)
                dialogueRoot.SetActive(false);

            if (skipButton != null)
                skipButton.onClick.AddListener(OnSkipClicked);
        }

        private void Update()
        {
            if (!_isActive) return;

            // 键盘输入
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                AdvanceDialogue();
            }

            // 鼠标点击（覆盖层区域）
            if (Input.GetMouseButtonDown(0))
            {
                // 检查是否点击了 UI 元素（按钮等）
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    // 如果点击的是按钮，不处理（按钮有自己的 onClick）
                    // 如果点击的是对话框区域，继续对话
                    if (clickArea != null)
                    {
                        var pointerData = new PointerEventData(EventSystem.current)
                        {
                            position = Input.mousePosition
                        };
                        var results = new System.Collections.Generic.List<RaycastResult>();
                        EventSystem.current.RaycastAll(pointerData, results);

                        bool clickedButton = false;
                        foreach (var result in results)
                        {
                            if (result.gameObject.GetComponent<Button>() != null)
                            {
                                clickedButton = true;
                                break;
                            }
                        }

                        if (!clickedButton)
                        {
                            AdvanceDialogue();
                        }
                    }
                    else
                    {
                        // 没有配置 clickArea，直接继续
                        AdvanceDialogue();
                    }
                }
                else
                {
                    // 点击了非 UI 区域，继续对话
                    AdvanceDialogue();
                }
            }
        }

        #endregion

        #region 公开方法

        /// <summary>
        /// 开始播放对话序列。
        /// </summary>
        /// <param name="data">对话数据</param>
        /// <param name="onComplete">播放完毕回调</param>
        public void Show(DialogueData data, Action onComplete = null)
        {
            if (data == null || data.lines == null || data.lines.Length == 0)
            {
                Debug.LogWarning("[DialogueBox] 对话数据为空，跳过");
                onComplete?.Invoke();
                return;
            }

            _currentData = data;
            _currentLineIndex = 0;
            _onComplete = onComplete;
            _isActive = true;

            dialogueRoot.SetActive(true);
            OnDialogueStarted?.Invoke();

            ShowLine(_currentLineIndex);
        }

        /// <summary>
        /// 跳过当前打字，直接显示完整文本。
        /// 如果已经显示完整，则跳到下一行。
        /// </summary>
        public void SkipOrAdvance()
        {
            AdvanceDialogue();
        }

        /// <summary>
        /// 强制结束对话。
        /// </summary>
        public void ForceClose()
        {
            StopTypewriter();
            _isActive = false;
            dialogueRoot.SetActive(false);
            OnDialogueCompleted?.Invoke();
            _onComplete?.Invoke();
        }

        #endregion

        #region 内部逻辑

        private void AdvanceDialogue()
        {
            // 防止连续点击
            if (Time.time - _lastClickTime < CLICK_COOLDOWN) return;
            _lastClickTime = Time.time;

            if (_isTyping)
            {
                // 正在打字 → 跳过显示全部
                StopTypewriter();
                SetFullText();
            }
            else
            {
                // 打字已完成 → 播放下一行
                _currentLineIndex++;
                if (_currentLineIndex < _currentData.lines.Length)
                {
                    ShowLine(_currentLineIndex);
                }
                else
                {
                    // 对话结束
                    Close();
                }
            }
        }

        private void ShowLine(int index)
        {
            var line = _currentData.lines[index];

            // 设置说话人名称
            if (speakerNameText != null)
            {
                speakerNameText.text = line.speakerName;
                speakerNameText.color = line.speakerColor;
            }

            // 设置立绘
            UpdatePortrait(line);

            // 开始打字
            float speed = line.GetTypeSpeed();
            if (speed <= 0f) speed = defaultTypeSpeed;

            _fullText = line.content;
            OnLineStarted?.Invoke(index, line);

            _typewriterCoroutine = StartCoroutine(TypewriterRoutine(line.content, speed, index));
        }

        private void UpdatePortrait(DialogueLine line)
        {
            if (portraitImage == null) return;

            if (line.portrait != null)
            {
                // 有立绘图片
                portraitImage.sprite = line.portrait;
                portraitImage.gameObject.SetActive(true);
                if (portraitPlaceholder != null)
                    portraitPlaceholder.SetActive(false);

                // 设置立绘尺寸
                float w = line.portraitWidth > 0f ? line.portraitWidth : defaultPortraitWidth;
                float h = line.portraitHeight > 0f ? line.portraitHeight : defaultPortraitHeight;

                var rectTransform = portraitImage.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.sizeDelta = new Vector2(w, h);
                }
            }
            else
            {
                // 无立绘，显示占位符
                portraitImage.gameObject.SetActive(false);
                if (portraitPlaceholder != null)
                {
                    portraitPlaceholder.SetActive(true);
                    if (placeholderText != null)
                        placeholderText.text = line.speakerName.Length > 0
                            ? line.speakerName[0].ToString()
                            : "?";
                }
            }
        }

        private IEnumerator TypewriterRoutine(string text, float speed, int lineIndex)
        {
            _isTyping = true;
            dialogueText.text = "";

            for (int i = 0; i < text.Length; i++)
            {
                dialogueText.text = text.Substring(0, i + 1);
                yield return new WaitForSeconds(speed);
            }

            _isTyping = false;
            OnLineCompleted?.Invoke(lineIndex, _currentData.lines[lineIndex]);

            // 自动等待
            if (autoWaitTime > 0f)
            {
                yield return new WaitForSeconds(autoWaitTime);
                _currentLineIndex++;
                if (_currentLineIndex < _currentData.lines.Length)
                {
                    ShowLine(_currentLineIndex);
                }
                else
                {
                    Close();
                }
            }
        }

        private void StopTypewriter()
        {
            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
                _typewriterCoroutine = null;
            }
            _isTyping = false;
        }

        private void SetFullText()
        {
            dialogueText.text = _fullText;
            OnLineCompleted?.Invoke(_currentLineIndex, _currentData.lines[_currentLineIndex]);
        }

        private void Close()
        {
            _isActive = false;
            dialogueRoot.SetActive(false);
            OnDialogueCompleted?.Invoke();
            _onComplete?.Invoke();
        }

        private void OnSkipClicked()
        {
            AdvanceDialogue();
        }

        #endregion
    }
}
