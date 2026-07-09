# 三种可交互物体：简单实现思路

按键统一为 **E**（`InputEvents.PlayerInteractPressed`）。玩家侧由 `PlayerInteractor` 负责：检测范围内物体、按模式分发交互、管理持有物。

## 总览

| 类型 | 脚本 | 模式 | 作用 |
|------|------|------|------|
| A | `InteractableObject`（基类默认） | PickUp | 可捡起 / 丢弃的物品 |
| B | `InteractableObjectB` | Trigger | 触发型开关 |
| C | `InteractableObjectC` | Trigger | 交付处（需检定状态后才开放） |

同一关卡若有多套机制，用基类上的 **`mechanismId`** 绑定：同一机制的 A / B / C 填相同 id，彼此不可交叉。

```
玩家按 E
  ├─ 手上有物 → 优先找附近可交付的 C；没有则丢弃
  └─ 手上无物 → 找最近 CanInteract 的物体
        ├─ PickUp → 捡起（A）
        └─ Trigger → OnInteract（B / C）
```

## 物品 A（可拾取）

- 直接用基类 `InteractableObject`，`Mode = PickUp`。
- 进入玩家交互 Trigger 范围后按 E 捡起，挂到玩家下；再按 E（附近没有可交付 C 时）丢到身前。
- 捡起时关掉自身 Collider；丢弃时恢复，并还原 `localScale`（避免被玩家 0.5 缩放越丢越小）。
- 持有变化会广播 `GameEvents.HeldItemChanged`，供 `InventorySlotUI` 显示。

## 物品 B（触发开关）

- 继承基类，`Mode = Trigger`。
- **独立开关**：`sequenceGroupId` 留空 → 按一次后 `activated`，不可再互动。
- **顺序组**：同组填相同 `sequenceGroupId`，用 `sequenceIndex` 定顺序 → 必须按序触发；全部触发后等待 `resetDelaySeconds`，再整组复位外观/状态。
- 成功触发后会把对应 `mechanismId` 记为已满足（`IsMechanismSatisfied`），供 C 检定；序列组复位后仍保持「已成功过」，交付处可继续开放。

## 物品 C（交付处）

- 继承基类，`Mode = Trigger`。
- 默认锁定；满足以下**任意一条**才 `CanInteract`：
  1. 玩家持有 **同 `mechanismId`** 的物品 A
  2. **同 `mechanismId`** 的物品 B 已成功触发
- 外观由事件刷新：`HeldItemChanged`（含 `MechanismId`）、`MechanismSatisfied`。
- 交互成功后标记 `completed`，不再可互动；若手上是匹配的 A，则 `ConsumeHeldItem` 销毁持有物。
- 空手但 B 已满足时，也可对 C 按 E 完成交付（纯开关线）。

## 关卡配置要点

1. 每套机制选一个唯一 `mechanismId`（如 `door_1`、`puzzle_red`）。
2. 该机制下的 A / B / C 都填这个 id。
3. B 若要顺序触发：同组 `sequenceGroupId` + 递增 `sequenceIndex`，按需调 `resetDelaySeconds`。
4. 物体需有 Collider2D（建议 Trigger）；玩家需有交互范围 Trigger + `PlayerInteractor`。

## 相关脚本

- `Assets/Scripts/Gameplay/InteractableObject.cs` — 基类 / A
- `Assets/Scripts/Gameplay/InteractableObjectB.cs` — B
- `Assets/Scripts/Gameplay/InteractableObjectC.cs` — C
- `Assets/Scripts/Gameplay/PlayerInteractor.cs` — 玩家交互入口
- `Assets/Scripts/UI/InventorySlotUI.cs` — 持有物 HUD
