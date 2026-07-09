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

        /// <summary>当前活跃世界切换，payload = WorldId（Gameplay）</summary>
        public const string ActiveWorldChanged = "Game.ActiveWorldChanged";

        /// <summary>玩家持有物变化，payload = HeldItemInfo（可能为空）</summary>
        public const string HeldItemChanged = "Game.HeldItemChanged";

        /// <summary>全局游戏状态切换，payload = GameState</summary>
        public const string GameStateChanged = "Game.GameStateChanged";
    }

    /// <summary>全局游戏流程阶段。GameManager 单例广播 <see cref="GameEvents.GameStateChanged"/>。</summary>
    public enum GameState
    {
        /// <summary>启动阶段，尚未进入任何界面。</summary>
        Boot,
        /// <summary>主菜单 / 选关界面。</summary>
        MainMenu,
        /// <summary>关卡加载中（场景切换过程）。</summary>
        LevelLoading,
        /// <summary>关卡进行中，HUD 应当显示。</summary>
        LevelPlaying,
        /// <summary>关卡结算，展示通关 UI。</summary>
        LevelCompleted
    }

    /// <summary>当前持有物展示信息（UI 用）。</summary>
    public readonly struct HeldItemInfo
    {
        public readonly bool HasItem;
        public readonly UnityEngine.Sprite Icon;
        public readonly string DisplayName;

        public HeldItemInfo(bool hasItem, UnityEngine.Sprite icon, string displayName)
        {
            HasItem = hasItem;
            Icon = icon;
            DisplayName = displayName ?? string.Empty;
        }

        public static HeldItemInfo Empty => new(false, null, string.Empty);
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
