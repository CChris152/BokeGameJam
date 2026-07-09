namespace BokeGameJam.Core
{
    /// <summary>
    /// 集中定义与关卡/UI 生命周期相关的 EventManager 事件名。
    /// </summary>
    public static class GameEvents
    {
        /// <summary>玩家在选关界面选择了某个关卡，payload = <see cref="LevelSelection"/></summary>
        public const string LevelSelected = "Game.LevelSelected";

        /// <summary>关卡加载完毕，可以开始游玩，payload = string levelId</summary>
        public const string LevelStarted = "Game.LevelStarted";

        /// <summary>玩家通关，payload = string levelId</summary>
        public const string LevelCompleted = "Game.LevelCompleted";
    }

    /// <summary>关卡选择事件 payload。</summary>
    public readonly struct LevelSelection
    {
        public readonly string LevelId;
        public readonly int Index;
        public readonly string SceneName;
        public readonly string LevelFile;

        public LevelSelection(string levelId, int index, string sceneName, string levelFile)
        {
            LevelId = levelId;
            Index = index;
            SceneName = sceneName;
            LevelFile = levelFile;
        }
    }
}
