using UnityEngine;
using BokeGameJam.Core;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 收到 <see cref="GameEvents.CandyReceived"/> 后隐藏自身（用于可消失墙壁等）。
    /// </summary>
    public sealed class CandyReceivedDespawn : MonoBehaviour
    {
        private void OnEnable()
        {
            EventManager.On(GameEvents.CandyReceived, OnCandyReceived);
        }

        private void OnDisable()
        {
            EventManager.Off(GameEvents.CandyReceived, OnCandyReceived);
        }

        private void OnCandyReceived()
        {
            gameObject.SetActive(false);
        }
    }
}
