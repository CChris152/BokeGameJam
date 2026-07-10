namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 地形地块。在 prefab 上设置 LevelLayer（通常为 A 或 B）。
    /// Prefab 结构：根节点挂 TerrainTile；
    /// 子物体 Image 挂 SpriteRenderer + Collider2D。
    /// </summary>
    public sealed class TerrainTile : LevelObject
    {
    }
}
