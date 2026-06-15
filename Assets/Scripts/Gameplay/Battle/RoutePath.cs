using UnityEngine;

namespace DoudizhuTower.Gameplay.Battle
{
    /// <summary>
    /// 路线路径定义。
    /// 挂在场景中的一个空 GO 上，拖入路径点后自动绘制可视路线。
    /// </summary>
    public class RoutePath : MonoBehaviour
    {
        [Header("路径点（按顺序拖入）")]
        public Transform[] waypoints;

        [Header("路线锁定")]
        [Tooltip("初始是否锁定（锁定时玩家无法选择此路线）")]
        [SerializeField] private bool _locked;

        [Header("可视化")]
        public Color lineColor = Color.green;
        public float waypointSize = 0.3f;

        /// <summary>路线是否被锁定</summary>
        public bool IsLocked => _locked;

        /// <summary>解锁路线（由 BossController 等系统调用）</summary>
        public void Unlock() => _locked = false;

        /// <summary>锁定路线</summary>
        public void Lock() => _locked = true;

        [Tooltip("启用后路径点坐标在 Awake 时锁定，运行时不受父物体位移影响。若路径点需跟随移动物体（如 BOSS），请取消勾选。")]
        [SerializeField] private bool _cachePositions = true;

        // 运行时缓存的世界坐标，防止父物体移动导致路径漂移
        private Vector3[] _cachedPositions;

        private void Awake()
        {
            if (_cachePositions)
                CachePositions();
        }

        /// <summary>锁定当前路径点的世界坐标，运行时不受父物体位移影响</summary>
        public void CachePositions()
        {
            if (waypoints == null || waypoints.Length == 0) { _cachedPositions = null; return; }
            _cachedPositions = new Vector3[waypoints.Length];
            for (int i = 0; i < waypoints.Length; i++)
                _cachedPositions[i] = waypoints[i] != null ? waypoints[i].position : transform.position;
        }

        public Vector3 GetPoint(int i)
        {
            if (_cachedPositions != null && i < _cachedPositions.Length) return _cachedPositions[i];
            if (waypoints != null && i < waypoints.Length && waypoints[i] != null) return waypoints[i].position;
            return transform.position;
        }

        /// <summary>获取路径总长度</summary>
        public float TotalLength
        {
            get
            {
                if (waypoints == null || waypoints.Length < 2) return 0f;
                float len = 0f;
                for (int i = 0; i < waypoints.Length - 1; i++)
                    len += Vector3.Distance(GetPoint(i), GetPoint(i + 1));
                return len;
            }
        }

        /// <summary>
        /// 获取沿路径的总距离对应的位置
        /// </summary>
        public Vector3 GetPositionAtDistance(float dist)
        {
            if (waypoints == null || waypoints.Length == 0)
                return transform.position;
            if (waypoints.Length == 1)
                return GetPoint(0);

            float accumulated = 0f;
            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                float segment = Vector3.Distance(GetPoint(i), GetPoint(i + 1));
                if (dist <= accumulated + segment || i == waypoints.Length - 2)
                {
                    float t = (dist - accumulated) / segment;
                    return Vector3.Lerp(GetPoint(i), GetPoint(i + 1), Mathf.Clamp01(t));
                }
                accumulated += segment;
            }
            return GetPoint(waypoints.Length - 1);
        }

        /// <summary>
        /// 获取路径上距离 worldPos 最近点的路径距离（用于按沿途顺序排序目标）。
        /// </summary>
        public float GetClosestPathDistance(Vector3 worldPos)
        {
            if (waypoints == null || waypoints.Length < 2) return 0f;
            float bestDist = float.MaxValue;
            float bestPathDist = 0f;
            float accumulated = 0f;
            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                Vector3 a = GetPoint(i);
                Vector3 b = GetPoint(i + 1);
                Vector3 ab = b - a;
                float segLen = ab.magnitude;
                if (segLen < 0.0001f) { accumulated += segLen; continue; }
                float t = Mathf.Clamp01(Vector3.Dot(worldPos - a, ab) / (segLen * segLen));
                Vector3 closest = a + ab * t;
                float d = Vector3.Distance(worldPos, closest);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestPathDist = accumulated + t * segLen;
                }
                accumulated += segLen;
            }
            return bestPathDist;
        }

        /// <summary>
        /// 获取路径上一点的前进方向
        /// </summary>
        public Vector3 GetDirectionAtDistance(float dist)
        {
            if (waypoints == null || waypoints.Length < 2)
                return transform.forward;

            float accumulated = 0f;
            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                float segment = Vector3.Distance(GetPoint(i), GetPoint(i + 1));
                if (dist <= accumulated + segment || i == waypoints.Length - 2)
                {
                    return (GetPoint(i + 1) - GetPoint(i)).normalized;
                }
                accumulated += segment;
            }
            return (GetPoint(waypoints.Length - 1) - GetPoint(waypoints.Length - 2)).normalized;
        }

        // ─── Scene 视图绘制 ───
        private void OnDrawGizmos()
        {
            int len = waypoints != null ? waypoints.Length : 0;
            if (len < 2) return;

            // 编辑器下总是读取实时 waypoint 位置（不依赖_ cachedPositions）
            // 运行时用缓存坐标（防止父物体漂移）
            bool useCached = Application.isPlaying && _cachedPositions != null;

            Gizmos.color = lineColor;
            for (int i = 0; i < len - 1; i++)
            {
                Vector3 p0 = useCached ? GetPoint(i) : (waypoints[i] != null ? waypoints[i].position : transform.position);
                Vector3 p1 = useCached ? GetPoint(i + 1) : (waypoints[i + 1] != null ? waypoints[i + 1].position : transform.position);
                Gizmos.DrawLine(p0, p1);
                Vector3 dir = (p1 - p0).normalized;
                Vector3 mid = Vector3.Lerp(p0, p1, 0.5f);
                Gizmos.DrawRay(mid, dir * 0.3f);
            }

            Gizmos.color = Color.yellow;
            for (int i = 0; i < len; i++)
            {
                Vector3 p = useCached ? GetPoint(i) : (waypoints[i] != null ? waypoints[i].position : transform.position);
                Gizmos.DrawSphere(p, waypointSize);
            }
        }
    }
}
