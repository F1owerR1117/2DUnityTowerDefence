using System.Collections;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Presentation
{
    /// <summary>
    /// 镜头导演：控制所有镜头演出。
    /// 只接受 Transform 引用，不依赖任何战斗系统。
    /// </summary>
    public class CameraDirector : MonoBehaviour
    {
        private Camera _cam;
        private Vector3 _originalPosition;
        private float _originalSize;
        private Coroutine _activeCoroutine;

        /// <summary>演出期间为 true，CameraController 应跳过 Update</summary>
        public bool IsBusy { get; set; }

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = Camera.main;
            if (_cam != null)
            {
                _originalPosition = _cam.transform.position;
                _originalSize = _cam.orthographic ? _cam.orthographicSize : _cam.fieldOfView;
            }
        }

        /// <summary>每次演出开始前由 CameraController 调用，同步原始位置</summary>
        public void SyncOriginalPosition()
        {
            if (_cam != null)
                _originalPosition = _cam.transform.position;
        }

        /// <summary>聚焦到目标位置</summary>
        public void FocusTarget(Transform target, float duration)
        {
            if (target == null) return;
            if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
            IsBusy = true;
            _activeCoroutine = StartCoroutine(FocusTargetCoroutine(target, duration));
        }

        /// <summary>跟随目标移动</summary>
        public void FollowTarget(Transform target, float duration)
        {
            if (target == null) return;
            if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
            IsBusy = true;
            _activeCoroutine = StartCoroutine(FollowTargetCoroutine(target, duration));
        }

        /// <summary>返回原始位置</summary>
        public void Return(float duration)
        {
            if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
            IsBusy = true;
            _activeCoroutine = StartCoroutine(ReturnCoroutine(duration));
        }

        /// <summary>镜头震动</summary>
        public void Shake(float duration, float intensity)
        {
            if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
            IsBusy = true;
            _activeCoroutine = StartCoroutine(ShakeCoroutine(duration, intensity));
        }

        /// <summary>缩放镜头</summary>
        public void Zoom(float targetSize, float duration)
        {
            if (_cam == null) return;
            if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
            IsBusy = true;
            _activeCoroutine = StartCoroutine(ZoomCoroutine(targetSize, duration));
        }

        /// <summary>强制停止所有镜头动作并恢复原始位置</summary>
        public void StopAll()
        {
            if (_activeCoroutine != null)
            {
                StopCoroutine(_activeCoroutine);
                _activeCoroutine = null;
            }
            if (_cam != null)
                _cam.transform.position = _originalPosition;
            IsBusy = false;
        }

        private void OnPresentationEnd()
        {
            _activeCoroutine = null;
            IsBusy = false;
        }

        private IEnumerator FocusTargetCoroutine(Transform target, float duration)
        {
            if (_cam == null) yield break;
            Vector3 targetPos = new Vector3(target.position.x, target.position.y, _cam.transform.position.z);
            Vector3 startPos = _cam.transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                _cam.transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }
            _cam.transform.position = targetPos;
        }

        private IEnumerator FollowTargetCoroutine(Transform target, float duration)
        {
            if (_cam == null) yield break;
            float elapsed = 0f;

            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                Vector3 targetPos = new Vector3(target.position.x, target.position.y, _cam.transform.position.z);
                _cam.transform.position = Vector3.Lerp(_cam.transform.position, targetPos, Time.unscaledDeltaTime * 5f);
                yield return null;
            }
        }

        private IEnumerator ReturnCoroutine(float duration)
        {
            if (_cam == null) yield break;
            Vector3 startPos = _cam.transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                _cam.transform.position = Vector3.Lerp(startPos, _originalPosition, t);
                yield return null;
            }
            _cam.transform.position = _originalPosition;
        }

        private IEnumerator ShakeCoroutine(float duration, float intensity)
        {
            if (_cam == null) yield break;
            Vector3 originalPos = _cam.transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float x = Random.Range(-1f, 1f) * intensity;
                float y = Random.Range(-1f, 1f) * intensity;
                _cam.transform.position = originalPos + new Vector3(x, y, 0f);
                yield return null;
            }
            _cam.transform.position = originalPos;
        }

        private IEnumerator ZoomCoroutine(float targetSize, float duration)
        {
            if (_cam == null) yield break;
            float startSize = _cam.orthographic ? _cam.orthographicSize : _cam.fieldOfView;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                float size = Mathf.Lerp(startSize, targetSize, t);
                if (_cam.orthographic) _cam.orthographicSize = size;
                else _cam.fieldOfView = size;
                yield return null;
            }

            if (_cam.orthographic) _cam.orthographicSize = targetSize;
            else _cam.fieldOfView = targetSize;
        }
    }
}
