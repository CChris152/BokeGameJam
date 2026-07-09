using System.Collections.Generic;

namespace BokeGameJam.Gameplay
{
    /// <summary>
    /// 各房间当前关灯状态缓存，供晚订阅的背景等读取初始值。
    /// </summary>
    public static class RoomLightsState
    {
        private static readonly Dictionary<string, bool> lightsOffByRoom = new();

        public static void Set(string roomId, bool lightsOff)
        {
            string id = Normalize(roomId);
            if (string.IsNullOrEmpty(id))
                return;

            lightsOffByRoom[id] = lightsOff;
        }

        public static bool TryGet(string roomId, out bool lightsOff)
        {
            string id = Normalize(roomId);
            if (string.IsNullOrEmpty(id))
            {
                lightsOff = false;
                return false;
            }

            return lightsOffByRoom.TryGetValue(id, out lightsOff);
        }

        private static string Normalize(string roomId)
        {
            return string.IsNullOrWhiteSpace(roomId) ? null : roomId.Trim();
        }
    }
}
