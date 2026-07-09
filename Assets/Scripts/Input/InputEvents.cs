using UnityEngine;

namespace BokeGameJam.Input
{
    /// <summary>
    /// 集中定义所有由 InputManager 广播的事件名与 payload 结构。
    /// 订阅方（Player、Camera、LevelEditor 等）与 InputManager 之间只通过这些常量耦合。
    /// </summary>
    public static class InputEvents
    {
        // ---------- 输入上下文 ----------
        /// <summary>输入上下文切换：payload=当前 InputContext</summary>
        public const string ContextChanged = "Input.ContextChanged";

        // ---------- 玩家 ----------
        /// <summary>玩家水平输入：payload=float in [-1,1]（每帧更新一次）</summary>
        public const string PlayerMove = "Input.Player.Move";
        /// <summary>玩家跳跃按下</summary>
        public const string PlayerJumpPressed = "Input.Player.JumpPressed";
        /// <summary>玩家交互按下（E 键）</summary>
        public const string PlayerInteractPressed = "Input.Player.InteractPressed";

        // ---------- 双世界 ----------
        /// <summary>切换世界 A/B（Shift）</summary>
        public const string WorldToggle = "Input.World.Toggle";

        // ---------- 关卡编辑器 ----------
        /// <summary>切换编辑模式（M 键）</summary>
        public const string EditorToggle = "Input.Editor.Toggle";
        /// <summary>保存</summary>
        public const string EditorSave = "Input.Editor.Save";
        /// <summary>加载</summary>
        public const string EditorLoad = "Input.Editor.Load";
        /// <summary>清空</summary>
        public const string EditorClear = "Input.Editor.Clear";

        /// <summary>放置地块（左键按下时每帧广播一次，用于连续绘制）</summary>
        public const string EditorPaintHeld = "Input.Editor.PaintHeld";
        /// <summary>删除地块（右键按下时每帧广播一次）</summary>
        public const string EditorEraseHeld = "Input.Editor.EraseHeld";
        /// <summary>调色板选择：payload=索引</summary>
        public const string EditorSelectPalette = "Input.Editor.SelectPalette";

        // ---------- 相机 ----------
        /// <summary>相机移动方向：payload=已归一化的 Vector2（编辑模式下 WASD/方向键）</summary>
        public const string CameraMove = "Input.Camera.Move";
        /// <summary>相机缩放：payload=float（滚轮 delta，正=拉近，负=拉远）</summary>
        public const string CameraZoom = "Input.Camera.Zoom";
    }

    /// <summary>
    /// 输入上下文：不同上下文启用不同的事件广播集合。
    /// </summary>
    public enum InputContext
    {
        /// <summary>正常游玩，玩家可操控角色</summary>
        Gameplay,
        /// <summary>关卡编辑，玩家操控相机与地块</summary>
        LevelEditor,
        /// <summary>UI 独占，屏蔽游戏输入</summary>
        UI
    }
}
