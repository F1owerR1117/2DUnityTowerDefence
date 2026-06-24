using UnityEngine;

namespace DoudizhuTower.UI
{
    /// <summary>
    /// 摄像机控制。WASD/方向键 + 鼠标边缘滚动移动，
    /// 滚轮缩放 orthographicSize。
    /// 动态边界：根据 orthographicSize 和 aspect 计算允许移动范围，
    /// 确保任何缩放级别下相机视野不超出地图边界。
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

        [Header("地图绝对边界（整个场景的四个尽头坐标）")]
        public float mapMinX = -300f;
        public float mapMaxX = 900f;
        public float mapMinY = 100f;
        public float mapMaxY = 450f;

        private Camera _cam;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = Camera.main;
        }

        private void Update()
        {
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

            // 动态限制边界：根据相机视口大小计算允许移动范围
            if (_cam != null)
            {
                float camHalfHeight = _cam.orthographicSize;
                float camHalfWidth = _cam.orthographicSize * _cam.aspect;

                float clampedMinX = mapMinX + camHalfWidth;
                float clampedMaxX = mapMaxX - camHalfWidth;
                float clampedMinY = mapMinY + camHalfHeight;
                float clampedMaxY = mapMaxY - camHalfHeight;

                // 相机视野大于地图时，固定在地图中心
                if (clampedMinX > clampedMaxX) clampedMinX = clampedMaxX = (mapMinX + mapMaxX) / 2f;
                if (clampedMinY > clampedMaxY) clampedMinY = clampedMaxY = (mapMinY + mapMaxY) / 2f;

                Vector3 pos = transform.position;
                pos.x = Mathf.Clamp(pos.x, clampedMinX, clampedMaxX);
                pos.y = Mathf.Clamp(pos.y, clampedMinY, clampedMaxY);
                transform.position = pos;
            }
        }
    }
}
