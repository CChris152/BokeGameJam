using UnityEngine;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 关卡可放置物体基类。地形与 Interactable 均须继承此类，并声明所属层级。
    /// </summary>
    public abstract class LevelObject : MonoBehaviour
    {
        [Header("Level Layer")]
        [Tooltip("A/B：随世界切换；Shared：不随 A/B 切换")]
        [SerializeField] private LevelLayer levelLayer = LevelLayer.Shared;

        public LevelLayer LevelLayer => levelLayer;

        /// <summary>关卡编辑器写入所属层级。</summary>
        public void SetLevelLayer(LevelLayer layer)
        {
            levelLayer = layer;
        }
    }
}
