using UnityEngine;
using DoudizhuTower.Core.Battle;

namespace DoudizhuTower.Gameplay.Battle
{
    /// <summary>
    /// 地图静态常量与工具方法（§2.2/§5）。
    /// 所有坐标系常量集中在此，避免散落各处。
    /// </summary>
    public static class MapController
    {
        // ─── 路线 Y 坐标 ──────────────────────────────
        public const float TopLaneY = 2.0f;
        public const float BottomLaneY = -2.0f;

        // ─── 隘口区域（X 范围） ───────────────────────
        public const float PassXStart = 5.0f;
        public const float PassXEnd = 8.0f;

        // ─── 基地 X 坐标 ──────────────────────────────
        public const float FarmerBaseX = -10.0f;
        public const float LandlordBaseX = 10.0f;

        // ─── 兵种生成偏移 ─────────────────────────────
        public const float FarmerSpawnX = -9.0f;
        public const float LandlordSpawnX = 9.0f;
        public const float SpawnStepBack = 0.5f;

        // ─── 判定常量 ─────────────────────────────────
        public const float LaneTolerance = 0.5f;
        public const float DefaultMeleeRange = 1.8f;
        public const float DefaultRangedRange = 5.0f;

        /// <summary>获取指定路线的 Y 坐标</summary>
        public static float GetLaneY(Lane lane)
        {
            return lane == Lane.Top ? TopLaneY : BottomLaneY;
        }

        /// <summary>判断两个位置是否在同一路线</summary>
        public static bool IsInSameLane(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.y - b.y) < LaneTolerance;
        }

        /// <summary>判断位置是否在隘口区域内</summary>
        public static bool IsInPass(Vector2 position)
        {
            return position.x >= PassXStart && position.x <= PassXEnd;
        }

        /// <summary>从 Y 坐标推断路线</summary>
        public static Lane GetLane(float y)
        {
            return y >= 0 ? Lane.Top : Lane.Bottom;
        }

        /// <summary>获取兵种朝向目标的方向（1=向右，-1=向左）</summary>
        public static int GetDirectionToTarget(Vector2 from, Vector2 to)
        {
            return to.x > from.x ? 1 : -1;
        }

        /// <summary>获取生成坐标</summary>
        public static Vector2 GetSpawnPosition(Lane lane, bool isLandlord)
        {
            float x = isLandlord ? LandlordSpawnX : FarmerSpawnX;
            float y = GetLaneY(lane);
            return new Vector2(x, y);
        }
    }
}
