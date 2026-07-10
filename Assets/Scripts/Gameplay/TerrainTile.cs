namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 地形地块。在 prefab 上设置 LevelLayer（通常为 A 或 B）。
    /// Prefab 结构与墙壁A一致：根节点挂 Collider2D + TerrainTile，
    /// 视觉放在子物体 Image 的 SpriteRenderer 上。
    /// </summary>
    public sealed class TerrainTile : LevelObject
    {
    }
}
