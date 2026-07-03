using UnityEngine;
using DoudizhuTower.Gameplay.Presentation;

namespace DoudizhuTower.UI
{
    /// <summary>
    /// 摄像机控制。WASD/方向键 + 鼠标边缘滚动移动，
    /// 滚轮缩放 orthographicSize。
    /// 演出期间由 CameraDirector 接管，本脚本跳过 Update。
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("移动")]
        public float moveSpeed = 30f;
        public float edgeMargin = 30f;

        [Header("缩放")]
        public float zoomSpeed = 10f;
        public float minZoom = 10f;
        public float maxZoom = 80f;

        [Header("边界")]
        public float minX = -300f;
        public float maxX = 900f;
        public float minY = 100f;
        public float maxY = 450f;

        private Camera _cam;
        private CameraDirector _director;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = Camera.main;
        }

        private void Start()
        {
            _director = FindFirstObjectByType<CameraDirector>();
        }

        private void Update()
        {
            // 演出期间跳过，由 CameraDirector 控制
            if (_director != null && _director.IsBusy) return;

            Vector3 move = Vector3.zero;

            // 键盘 WASD / 方向键
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.y -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1f;

            // 鼠标边缘滚动
            Vector3 mouse = Input.mousePosition;
            if (mouse.x >= 0 && mouse.x < edgeMargin) move.x -= 1f;
            if (mouse.x > Screen.width - edgeMargin && mouse.x <= Screen.width) move.x += 1f;
            if (mouse.y >= 0 && mouse.y < edgeMargin) move.y -= 1f;
            if (mouse.y > Screen.height - edgeMargin && mouse.y <= Screen.height) move.y += 1f;

            // 应用移动
            if (move != Vector3.zero)
                transform.Translate(move.normalized * moveSpeed * Time.deltaTime, Space.World);

            // 滚轮缩放
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f && _cam != null)
            {
                _cam.orthographicSize = Mathf.Clamp(
                    _cam.orthographicSize - scroll * zoomSpeed,
                    minZoom, maxZoom);
            }

            // 限制边界
            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            transform.position = pos;
        }
    }
}
