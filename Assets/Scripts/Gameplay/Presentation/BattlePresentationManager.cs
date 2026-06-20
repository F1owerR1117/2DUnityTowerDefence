using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Presentation
{
    /// <summary>
    /// 战斗演出管理器：统一调度所有演出序列。
    /// 所有演出必须通过此管理器触发，禁止直接调用镜头/对话/广播。
    /// </summary>
    public class BattlePresentationManager : MonoBehaviour
    {
        public static BattlePresentationManager Instance { get; private set; }

        [Header("组件引用")]
        [Tooltip("镜头控制器，挂载在 Main Camera 上")]
        [SerializeField] private CameraDirector cameraDirector;
        [Tooltip("战场广播管理器，用于显示屏幕广播文字")]
        [SerializeField] private BattleAnnouncementManager announcementManager;

        private Queue<PresentationSequence> _sequenceQueue = new();
        private PresentationSequence _currentSequence;
        private Coroutine _playingCoroutine;
        private bool _isPlaying;

        /// <summary>是否有演出正在播放</summary>
        public bool IsPlaying => _isPlaying;

        /// <summary>演出开始事件（供 BattleManager 监听暂停）</summary>
        public event Action OnPresentationStart;

        /// <summary>演出结束事件（供 BattleManager 监听恢复）</summary>
        public event Action OnPresentationEnd;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>播放一个演出序列（自动排队）</summary>
        public void PlaySequence(PresentationSequence sequence)
        {
            if (sequence == null) return;

            Debug.Log($"[Presentation] PlaySequence {sequence.name} at {Time.time:F2}s, isPlaying={_isPlaying}");

            if (_isPlaying)
            {
                Debug.Log($"[Presentation] Queued {sequence.name} (previous still playing)");
                _sequenceQueue.Enqueue(sequence);
                return;
            }

            _currentSequence = sequence;
            _isPlaying = true;
            _playingCoroutine = StartCoroutine(PlaySequenceCoroutine(sequence));
        }

        /// <summary>跳过当前演出</summary>
        public void SkipCurrentSequence()
        {
            if (_currentSequence != null && _currentSequence.allowSkip && _playingCoroutine != null)
            {
                StopCoroutine(_playingCoroutine);
                _playingCoroutine = null;
                OnSequenceComplete();
            }
        }

        private IEnumerator PlaySequenceCoroutine(PresentationSequence sequence)
        {
            // 暂停战斗
            if (sequence.pauseBattle)
                OnPresentationStart?.Invoke();

            // 执行镜头动作
            if (sequence.cameraActions != null && cameraDirector != null)
            {
                foreach (var cam in sequence.cameraActions)
                {
                    if (cam.delay > 0f) yield return new WaitForSecondsRealtime(cam.delay);
                    ExecuteCameraAction(cam);
                }
            }

            // 执行对话动作
            if (sequence.dialogues != null)
            {
                foreach (var dialogue in sequence.dialogues)
                {
                    yield return PlayDialogue(dialogue);
                }
            }

            // 执行广播动作
            if (sequence.announcements != null && announcementManager != null)
            {
                foreach (var ann in sequence.announcements)
                {
                    if (ann.delay > 0f) yield return new WaitForSecondsRealtime(ann.delay);
                    announcementManager.ShowAnnouncement(ann.type, ann.content, ann.duration);
                }
            }

            // 执行特效动作
            if (sequence.effects != null)
            {
                foreach (var fx in sequence.effects)
                {
                    if (fx.delay > 0f) yield return new WaitForSecondsRealtime(fx.delay);
                    if (fx.vfxPrefab != null && fx.spawnPoint != null)
                        Instantiate(fx.vfxPrefab, fx.spawnPoint.position, Quaternion.identity);
                }
            }

            // 等待最后一句对话结束
            yield return new WaitForSecondsRealtime(0.5f);

            OnSequenceComplete();
        }

        private void ExecuteCameraAction(CameraAction cam)
        {
            if (cameraDirector == null) return;

            switch (cam.type)
            {
                case CameraActionType.FocusTarget:
                    cameraDirector.FocusTarget(cam.target, cam.duration);
                    break;
                case CameraActionType.FollowTarget:
                    cameraDirector.FollowTarget(cam.target, cam.duration);
                    break;
                case CameraActionType.Return:
                    cameraDirector.Return(cam.duration);
                    break;
                case CameraActionType.Shake:
                    cameraDirector.Shake(cam.duration, cam.shakeIntensity);
                    break;
                case CameraActionType.Zoom:
                    cameraDirector.Zoom(cam.zoomSize, cam.duration);
                    break;
            }
        }

        private IEnumerator PlayDialogue(DialogueAction dialogue)
        {
            if (dialogue.lines == null) yield break;

            foreach (var line in dialogue.lines)
            {
                // 由 BossDialogueBubble 显示（外部监听 OnDialogueStart 事件）
                OnDialogueStart?.Invoke(line);

                if (line.waitForClick)
                    yield return new WaitUntil(() => Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space));
                else
                    yield return new WaitForSecondsRealtime(line.duration);
            }

            OnDialogueEnd?.Invoke();
        }

        private void OnSequenceComplete()
        {
            _isPlaying = false;
            _currentSequence = null;
            _playingCoroutine = null;

            // 恢复战斗
            OnPresentationEnd?.Invoke();

            // 播放队列中的下一个序列
            if (_sequenceQueue.Count > 0)
            {
                var next = _sequenceQueue.Dequeue();
                PlaySequence(next);
            }
        }

        /// <summary>对话开始事件（参数：DialogueLine）</summary>
        public event Action<DialogueLine> OnDialogueStart;

        /// <summary>对话结束事件</summary>
        public event Action OnDialogueEnd;
    }
}
