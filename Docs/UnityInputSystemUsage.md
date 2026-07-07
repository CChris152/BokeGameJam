# Unity Input System 使用指南

本文基于本项目当前环境整理：

- Unity 版本：`2022.3.62f3c1`
- Input System 包：`com.unity.inputsystem@1.14.2`
- 当前项目设置：`ProjectSettings/ProjectSettings.asset` 中 `activeInputHandler: 2`，通常对应 `Both`，适合在迁移期同时保留旧 `UnityEngine.Input` 和新 Input System。
- 当前代码状态：业务脚本里还存在旧输入 API，例如 `Assets/Scripts/Core/Test.cs` 使用 `Input.GetMouseButtonDown(0)`。
- 当前资源状态：项目里还没有 `.inputactions` 资源文件，需要在 Unity Editor 中创建。

## 1. 核心概念

Input System 推荐把“玩家想做什么”和“具体按了哪个设备按键”分开：

- `InputActionAsset`：一个 `.inputactions` 资源，保存所有输入配置。
- `Action Map`：一组相关动作，例如 `Gameplay`、`UI`、`Debug`。
- `Action`：具体意图，例如 `Move`、`Jump`、`Interact`、`Pause`、`Click`。
- `Binding`：动作对应的设备输入，例如 `WASD`、方向键、手柄左摇杆、鼠标左键。
- `Control Scheme`：设备方案，例如 `Keyboard&Mouse`、`Gamepad`、`Touch`。

推荐优先使用 `Actions + Bindings`，这样脚本只关心 `Move`、`Click` 这样的动作，不直接依赖 `W` 键或鼠标左键。

## 2. 在本项目创建输入资源

在 Unity Editor 中：

1. 打开 `Edit > Project Settings > Input System Package`。
2. 如果还没有 Project-Wide Actions，点击 `Create a new project-wide Action Asset`。
3. 建议把生成的资源放在 `Assets/Settings/InputSystem_Actions.inputactions`。
4. 在资源中建立这些 Action Map：

| Action Map | 用途 |
| --- | --- |
| `Gameplay` | 角色移动、交互、攻击、暂停等游戏内操作 |
| `UI` | 菜单导航、确认、取消、鼠标点击、滚轮等 UI 操作 |

`UI` 这组建议保留 Unity 默认生成的名字和类型，因为 `InputSystemUIInputModule` 会按这些动作驱动 UI。

## 3. 推荐的 Gameplay Action 配置

可以先按 2D 项目常用需求配置：

| Action | Action Type | Control Type | 建议 Binding |
| --- | --- | --- | --- |
| `Move` | `Value` | `Vector2` | `WASD`、方向键、`<Gamepad>/leftStick` |
| `Interact` | `Button` | `Button` | `<Keyboard>/e`、`<Gamepad>/buttonSouth` |
| `Primary` | `Button` | `Button` | `<Mouse>/leftButton`、`<Gamepad>/rightTrigger` |
| `Pause` | `Button` | `Button` | `<Keyboard>/escape`、`<Gamepad>/start` |
| `PointerPosition` | `Pass Through` | `Vector2` | `<Mouse>/position`、`<Touchscreen>/primaryTouch/position` |

`Move` 的键盘绑定可以使用 `2D Vector Composite`：

- `Up`: `<Keyboard>/w` 和 `<Keyboard>/upArrow`
- `Down`: `<Keyboard>/s` 和 `<Keyboard>/downArrow`
- `Left`: `<Keyboard>/a` 和 `<Keyboard>/leftArrow`
- `Right`: `<Keyboard>/d` 和 `<Keyboard>/rightArrow`

## 4. 代码使用方式一：Project-Wide Actions 轮询

适合普通单人玩法脚本。重点是：在 `Start` 或 `Awake` 缓存 `InputAction`，不要在 `Update` 里反复 `FindAction`。

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace BokeGameJam.Gameplay
{
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;

        private InputAction moveAction;
        private InputAction primaryAction;

        private void Start()
        {
            moveAction = InputSystem.actions.FindAction("Move", throwIfNotFound: true);
            primaryAction = InputSystem.actions.FindAction("Primary", throwIfNotFound: true);
        }

        private void Update()
        {
            Vector2 move = moveAction.ReadValue<Vector2>();
            transform.position += (Vector3)(move * moveSpeed * Time.deltaTime);

            if (primaryAction.WasPressedThisFrame())
            {
                Debug.Log("Primary action pressed");
            }
        }
    }
}
```

如果多个 Action Map 里有同名 Action，建议改为序列化引用或使用生成的 C# 包装类，避免字符串查找歧义。

## 5. 代码使用方式二：InputActionReference

适合希望在 Inspector 里明确指定动作的组件。优点是少写字符串，动作重命名时更安全。

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace BokeGameJam.Gameplay
{
    public class ClickDetector : MonoBehaviour
    {
        [SerializeField] private InputActionReference clickAction;

        private void OnEnable()
        {
            clickAction.action.Enable();
            clickAction.action.performed += OnClick;
        }

        private void OnDisable()
        {
            clickAction.action.performed -= OnClick;
            clickAction.action.Disable();
        }

        private void OnClick(InputAction.CallbackContext context)
        {
            Debug.Log("Clicked");
        }
    }
}
```

如果这个 Action 属于 Project-Wide Actions，它可能已经默认启用；但组件自己负责 `Enable/Disable` 的写法更清晰，也便于把同一个脚本用于非 Project-Wide 的 Action Asset。

## 6. 代码使用方式三：PlayerInput 组件

`PlayerInput` 适合玩家对象、多人本地联机、或者希望在 Inspector 中连事件的场景。

使用步骤：

1. 在玩家 Prefab 或玩家 GameObject 上添加 `PlayerInput`。
2. `Actions` 指向 `InputSystem_Actions.inputactions`。
3. `Default Action Map` 选择 `Gameplay`。
4. `Behavior` 推荐先用 `Invoke Unity Events` 或 `Invoke CSharp Events`。
5. 在事件里连接脚本方法。

示例：

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace BokeGameJam.Gameplay
{
    public class PlayerInputReceiver : MonoBehaviour
    {
        private Vector2 move;

        public void OnMove(InputAction.CallbackContext context)
        {
            move = context.ReadValue<Vector2>();
        }

        public void OnPrimary(InputAction.CallbackContext context)
        {
            if (!context.performed)
                return;

            Debug.Log("Primary performed");
        }

        private void Update()
        {
            // 在这里使用 move 做移动逻辑。
        }
    }
}
```

注意：使用 `PlayerInput` 时，不要从 `InputSystem.actions` 读取玩家输入。`PlayerInput` 会为每个玩家创建自己的 actions 副本并做设备配对，直接读全局 actions 会绕过这层逻辑。

## 7. 直接读设备状态

适合快速原型或临时调试，不适合长期玩法输入架构。

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

public class RawMouseClickExample : MonoBehaviour
{
    private void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Debug.Log("Mouse left button pressed");
        }
    }
}
```

旧写法：

```csharp
if (Input.GetMouseButtonDown(0))
{
    // ...
}
```

新写法：

```csharp
if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
{
    // ...
}
```

更推荐把点击配置成 `Primary` 或 `Click` Action，再让脚本读取 Action。

## 8. UI 输入配置

如果使用 uGUI，也就是 `Canvas`、`Button`、`EventSystem` 这一套：

1. 场景里需要 `EventSystem`。
2. `EventSystem` 上不要继续使用旧的 `StandaloneInputModule`。
3. 添加或替换为 `InputSystemUIInputModule`。
4. `Actions Asset` 指向你的 `InputSystem_Actions.inputactions`。
5. 确认 `UI` Action Map 里存在默认 UI 动作，例如：
   - `Navigate`
   - `Submit`
   - `Cancel`
   - `Point`
   - `Click`
   - `RightClick`
   - `MiddleClick`
   - `ScrollWheel`

本项目是 Unity 2022.3，如果使用 UI Toolkit 的运行时 UI，也需要通过 UI Input Module 把 Input System 的动作传给 UI。

## 9. 本地多人

如果后续需要本地多人：

1. 玩家 Prefab 上添加 `PlayerInput`。
2. 场景里添加 `PlayerInputManager`。
3. `PlayerInputManager.Player Prefab` 指向玩家 Prefab。
4. 设置 `Join Behavior`：
   - `Join Players When Button Is Pressed`：任意未配对设备按键加入。
   - `Join Players When Join Action Is Triggered`：只有触发指定 Join Action 才加入。
   - `Join Players Manually`：代码手动加入。
5. 如果要分屏，启用 `Enable Split-Screen`，并给玩家 Prefab 的 `PlayerInput.Camera` 指定玩家相机。

## 10. 迁移本项目旧输入代码

当前 `Assets/Scripts/Core/Test.cs` 的逻辑是左键点击切换场景：

```csharp
if (Input.GetMouseButtonDown(0))
{
    SwitchToTargetScene();
}
```

短期可替换成直接设备读取：

```csharp
using UnityEngine.InputSystem;

if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
{
    SwitchToTargetScene();
}
```

长期建议创建 `Gameplay/Primary` 或 `Gameplay/Click` Action，然后脚本读取这个 Action。这样以后切到手柄、触屏或改键时，不需要改玩法代码。

## 11. 常见坑

- `using UnityEngine.InputSystem;` 报错：确认 `Packages/manifest.json` 里存在 `com.unity.inputsystem`，本项目已经安装。
- Action 没响应：确认 Action 或 Action Map 已启用。
- `PlayerInput` 没响应：确认 `Default Action Map` 正确，`Behavior` 和回调方法签名匹配。
- UI 按钮没响应：确认 `EventSystem` 上是 `InputSystemUIInputModule`，不是 `StandaloneInputModule`。
- 在 `Update` 里调用 `FindAction`：不要这样做，应该在 `Start` 或 `Awake` 缓存引用。
- 使用 `PlayerInput` 时读 `InputSystem.actions`：不要这样做，应该读 `PlayerInput` 自己的 actions 或通过回调接收输入。
- 只启用 `Input System Package (New)` 后旧 `UnityEngine.Input` 抛异常：说明还有旧输入代码没迁移完。迁移期间保持 `Both` 更稳。

## 12. 本项目建议

- 输入资源统一放在 `Assets/Settings/InputSystem_Actions.inputactions`。
- 优先维护一个全局 Action Asset，包含 `Gameplay` 和 `UI`。
- 普通单人玩法优先用 Project-Wide Actions 或 `InputActionReference`。
- 玩家 Prefab、多人、本地设备配对场景使用 `PlayerInput`。
- 迁移期保留 `Active Input Handling = Both`；当所有 `UnityEngine.Input` 调用都替换后，再考虑切到 `Input System Package (New)`。
- 不要修改 `Assets/TextMesh Pro/Examples & Extras/` 下的示例脚本；这些旧输入调用来自导入示例，不属于本项目业务代码。

## 参考资料

- [Unity Input System 1.14 文档首页](https://docs.unity.cn/Packages/com.unity.inputsystem@1.14/manual/index.html)
- [Actions](https://docs.unity.cn/Packages/com.unity.inputsystem@1.14/manual/Actions.html)
- [Workflow Overview - Actions](https://docs.unity.cn/Packages/com.unity.inputsystem@1.14/manual/Workflow-Actions.html)
- [Project-Wide Actions](https://docs.unity.cn/Packages/com.unity.inputsystem@1.14/manual/ProjectWideActions.html)
- [Player Input](https://docs.unity.cn/Packages/com.unity.inputsystem@1.14/manual/PlayerInput.html)
- [Player Input Manager](https://docs.unity.cn/Packages/com.unity.inputsystem@1.14/manual/PlayerInputManager.html)
- [UI support](https://docs.unity.cn/Packages/com.unity.inputsystem@1.14/manual/UISupport.html)
