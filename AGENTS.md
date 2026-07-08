# BokeGameJam — Unity GameJam Project

## 1. Project Info & Structure

- **Unity Version:** 2022.3.62f3c1
- **Render Pipeline:** URP 2D
- **Main namespaces:** `BokeGameJam.Core`, `BokeGameJam.Gameplay`, `BokeGameJam.UI`

Keep authored project files under `Assets/`, package declarations under `Packages/`, and Unity settings under `ProjectSettings/`. Do not edit or commit generated folders such as `Library/`, `Temp/`, `Logs/`, `Obj/`, or `UserSettings/`.

Project layout:

- `Assets/Scripts/Core/`: shared managers and infrastructure, such as scene, audio, resources, and event systems
- `Assets/Scripts/Gameplay/`: gameplay rules, runtime systems, interactables, player/enemy logic, and level mechanics
- `Assets/Scripts/UI/`: UI controllers and view logic only
- `Assets/Scripts/Data/`: shared data definitions and ScriptableObject types; keep the namespace consistent with the owning module
- `Assets/Scenes/`: authored scenes
- `Assets/Prefabs/`: authored prefabs, with persistent managers under `Assets/Prefabs/Manager/`
- `Assets/Resources/`: runtime-loaded resources, including `Audio/`, `Art/`, `Config/`, `Prefabs/`, and `ScriptableObjects/`

For `Resources.Load`, paths are relative to `Assets/Resources` and omit file extensions.

## 2. Gamejam Priorities

Prioritize a playable loop, fast iteration, and low-risk changes. Prefer simple, explicit MonoBehaviours and ScriptableObjects over framework-style architecture unless the reuse is already clear.

Good default behavior:

- Make the smallest coherent change that supports the current gameplay goal.
- Keep shared systems stable and move experimental gameplay into `Gameplay/` or scene-specific components.
- Avoid broad refactors during feature work unless the current structure is blocking the feature.
- Add abstractions only when they remove real duplication or match an existing pattern.
- Preserve momentum: for visual tuning, scene composition, and prototype UI, editor/manual verification is acceptable when automated tests would be low value.

## 3. Quick Commands

| Command | Usage |
|---------|-------|
| `Unity Editor` | Open project via Unity Hub or the Unity Editor version above |
| `Unity.exe -projectPath . -runTests -testPlatform EditMode` | Run EditMode tests from CLI |
| `Unity.exe -projectPath . -runTests -testPlatform PlayMode` | Run PlayMode tests from CLI |
| `Unity.exe -projectPath . -quit -batchmode -nographics` | Batch open for compile/import validation |

Do not run Unity startup, Unity verification, csproj generation, git operations, or test commands after writing scripts unless the user explicitly asks or the task itself clearly requires it.

## 4. Agent Operating Guidelines

### 4.1 Source Lookup Order

Before modifying code or serialized Unity assets, read in this order:

1. `AGENTS.md` for project rules
2. Relevant docs under `Docs/`, if present
3. Current implementation code, prefabs/scenes/settings, and related tests

Skip missing docs or tests without blocking the task.

### 4.2 Unity Asset Safety

Before changing scenes, prefabs, ScriptableObjects, or settings, inspect the relevant YAML-backed files and preserve unrelated serialized changes. Keep each Unity `.meta` file with its asset.

Avoid modifying imported package examples, especially under `Assets/TextMesh Pro/Examples & Extras/`, unless the task explicitly requires it.

When editing resources used by scenes or prefabs, identify the source of truth first: ScriptableObject, prefab reference, scene object, or `Resources` path.

### 4.3 Feature Boundaries

Before adding a feature, decide:

- **Data ownership:** ScriptableObject, prefab/scene reference, or runtime state
- **Code ownership:** `Core`, `Gameplay`, `UI`, or `Data`
- **Verification:** focused test, static review, Unity Editor check, or manual Play Mode check
- **Compatibility:** whether existing scenes, prefabs, resources, or saved references need migration

Shared logic belongs in `Core/` or `Gameplay/`, not inside UI components. UI should display state and forward player intent; it should not own gameplay rules.

### 4.4 Stop and Confirm Rule

Stop and confirm only when the choice has meaningful consequences:

- Requirements or docs conflict with current code
- The change touches source-of-truth data used by multiple systems
- The change would rewrite scenes, prefabs, project settings, or many assets
- Multiple implementation approaches would affect gameplay architecture or team workflow differently
- Backward-compatible changes would require migrating existing resources or scenes

Do not stop for small local implementation choices that can be made consistently with the existing codebase.

### 4.5 Unity MCP Usage

Use the local Unity MCP server for editor inspection and small, explicit editor actions. Keep the server bound to localhost, inspect scene hierarchy, console output, assets, and components before mutating Unity state, and review diffs after scene, prefab, or asset changes.

Do not use MCP for broad asset rewrites, destructive project operations, or large binary asset generation unless the user explicitly asks for that scope.

## 5. Code Style & Architecture

- Use C# with 4-space indentation and braces on their own lines.
- Use PascalCase for public types, methods, and properties.
- Use camelCase for private fields and locals.
- Use `[SerializeField] private` for inspector-exposed state instead of public fields.
- Keep manager singletons explicit with an `Instance` property.
- Avoid class names that conflict with Unity APIs.
- Variable, method, type, and file names should be English.
- Production code comments and log strings should be English.
- Agent-user communication should be Chinese; this file may use English for agent precision.

Dependency direction:

- `Core` must not reference `Gameplay` or `UI`.
- `Gameplay` may reference `Core`.
- `UI` may reference `Core` and `Gameplay`.
- Shared data types should live in `Data` or the owning module namespace; avoid circular references.

Size guidance:

- Keep new files under about 800 lines when practical.
- Split files approaching 1500 lines.
- Keep methods focused; if a method grows past about 50 lines, check whether it is mixing responsibilities.
- Avoid god classes. Split by responsibility, not by dumping unrelated helper methods elsewhere.

## 6. Testing & Verification

The Unity Test Framework package is installed, but test folders may not exist yet. Add Edit Mode tests under `Assets/Tests/EditMode/` and Play Mode tests under `Assets/Tests/PlayMode/` when the value is clear.

Testing priorities:

- Add focused tests for core managers, data transformations, resource lookup, state machines, and regression fixes.
- Prefer behavior tests over private-method tests.
- Assert specific expected outcomes, not only `Assert.IsNotNull` or `Assert.DoesNotThrow`.
- For scene layout, prefab wiring, visual tuning, and rapid prototype UI, static review or manual Unity verification is acceptable.
- If verification is skipped because Unity was not run, say so clearly in the final response.

Good tests should fail when the behavior is broken, run independently, avoid network/timer dependencies, and document the intended behavior through their names.

## 7. Error Handling

| Rule | Description |
|------|-------------|
| No empty catch blocks | Every catch must rethrow, log the full error, or handle the error meaningfully |
| No silent failures | If an operation can fail, the caller or player-facing flow must know |
| Specific error messages | Include context such as resource key, scene name, or input value |
| Use Unity logging | Use `Debug.Log`, `Debug.LogWarning`, and `Debug.LogError`; avoid `print` |

For prototype-only code, lightweight handling is acceptable, but shared managers and resource-loading paths should fail loudly enough to debug quickly.

## 8. Git & Collaboration

- Never commit, pull, merge, rebase, reset, or discard changes without explicit user instruction.
- Assume the worktree may contain teammate or user changes; preserve unrelated edits.
- Prefer concise, imperative commit messages. Conventional prefixes such as `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, and `chore:` are preferred, but matching the team's current style is acceptable.
- Keep commits scoped to one logical change when the user asks for a commit.
- Before committing, report related test or validation results when available.
