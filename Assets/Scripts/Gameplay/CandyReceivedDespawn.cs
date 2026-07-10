using UnityEngine;
using BokeGameJam.Core;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 收到 <see cref="GameEvents.CandyReceived"/> 后隐藏自身（用于可消失墙壁等）。
    /// 使用 Awake/OnDestroy 订阅，避免挂在 A/B 层时因切世界 SetActive 而漏掉事件。
    /// </summary>
    public sealed class CandyReceivedDespawn : LevelObject
    {
        private void Awake()
        {
            EventManager.On(GameEvents.CandyReceived, OnCandyReceived);
        }

        private void OnDestroy()
        {
            EventManager.Off(GameEvents.CandyReceived, OnCandyReceived);
        }

        private void OnCandyReceived()
        {
            gameObject.SetActive(false);
        }
    }
}
