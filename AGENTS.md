# Repository Guidelines

## Project Structure & Module Organization

This is a Unity 2022.3.62f3c1 game project using URP 2D. Keep authored project files under `Assets/`, package declarations under `Packages/`, and Unity settings under `ProjectSettings/`. Do not edit or commit generated folders such as `Library/`, `Temp/`, `Logs/`, or `UserSettings/`.

Main code lives in `Assets/Scripts/`, grouped by feature: `Core/` for shared managers such as scene and audio systems, `Gameplay/` for play logic, and `UI/` for interface logic. Scenes are in `Assets/Scenes/`. Prefabs are in `Assets/Prefabs/`, with persistent managers in `Assets/Prefabs/Manager/`. Runtime-loaded resources belong under `Assets/Resources/`, for example `Audio/Music`, `Audio/SFX`, `Config`, `Prefabs`, and `ScriptableObjects`.

## Build, Test, and Development Commands

Open the project with Unity Hub or the Unity Editor version listed in `ProjectSettings/ProjectVersion.txt`.

Useful batch-mode examples:

```powershell
Unity.exe -projectPath . -runTests -testPlatform EditMode
Unity.exe -projectPath . -runTests -testPlatform PlayMode
Unity.exe -projectPath . -quit -batchmode -nographics
```

Use the Unity Editor Build Settings for local builds unless a project-specific build script is added.

## Coding Style & Naming Conventions

Use C# with 4-space indentation and braces on their own lines, matching the existing scripts. Put runtime code in clear namespaces such as `BokeGameJam.Core` or `BokeGameJam.UI`. Use PascalCase for public types, methods, and properties; camelCase for private fields and locals; and `[SerializeField] private` for inspector-exposed state. Keep manager singletons explicit and avoid class names that conflict with Unity APIs.

When adding assets, keep each Unity `.meta` file with its asset. For `Resources.Load`, paths are relative to `Assets/Resources` and omit file extensions.

## Testing Guidelines

The Unity Test Framework package is installed, but no test folders are currently present. Add Edit Mode tests under `Assets/Tests/EditMode/` and Play Mode tests under `Assets/Tests/PlayMode/`. Name test files after the behavior under test, such as `GameAudioManagerTests.cs`, and run them from Test Runner or the batch commands above.

## Commit & Pull Request Guidelines

Recent commit subjects use short prefixes such as `add ...` and `fix ...`. Keep commits concise, imperative, and scoped to one change. Pull requests should describe the gameplay or tooling impact, list test results, link related issues, and include screenshots or clips for visible scene, UI, or asset changes.

## Agent-Specific Instructions

Before changing Unity scenes, prefabs, or settings, inspect the relevant YAML-backed files and preserve unrelated serialized changes. Avoid modifying imported package examples, especially under `Assets/TextMesh Pro/Examples & Extras/`, unless the task explicitly requires it.
写完脚本后不用执行 git、csproj、Unity 启动或 Unity 验证等额外检查，除非用户明确要求。
