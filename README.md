# BokeGameJam

Unity 2022.3.62f3c1（URP 2D）Game Jam 项目。

## 项目结构

```
Assets/
├── Prefabs/
│   └── Manager/            # 管理器预制体（GameSceneManager、GameAudioManager 等）
├── Resources/              # 运行时需要通过 ResourcesManager 自动加载的入口资产
│   ├── Art/
│   │   ├── Animations/     # 动画 Clip、Animator Controller
│   │   ├── Fonts/          # 字体
│   │   ├── Materials/      # 材质
│   │   └── Pictures/       # 图片、Sprite、背景、UI 图等
│   ├── Audio/
│   │   ├── Music/          # 背景音乐源文件
│   │   └── SFX/            # 音效源文件
│   ├── Config/             # 配置数据
│   ├── Prefabs/            # 可运行时实例化的预制体
│   └── ScriptableObjects/  # ResourceDefinitionDatabase 等 SO 配置
├── Scenes/                 # Unity 场景
├── Scripts/
│   ├── Core/               # 核心系统：事件、资源、场景、音频
│   ├── Gameplay/           # 玩法逻辑
│   └── UI/                 # UI 逻辑
├── Settings/               # URP 等渲染设置
└── TextMesh Pro/           # TMP 插件资源（无需手动修改）
```

核心脚本位于 `Assets/Scripts/`。资源访问统一走 `ResourcesManager`，上层系统不要直接调用 `Resources.Load`。

## 资源定义数据库

资源统一登记在：

`Assets/Resources/ScriptableObjects/ResourceDefinitionDatabase.asset`

对应脚本：

`Assets/Scripts/Core/ResourceDefinitionDatabase.cs`

数据库分四类：

- `UIResource`：UI prefab，字段包含 `id`、`prefab`
- `SpriteResource`：Sprite，字段包含 `id`、`sprite`
- `SoundResource`：音频，字段包含 `id`、`category`、`clip`、`loop`、`volumeScale`
- `SceneResource`：场景，字段包含 `id`、`sceneAsset`、`sceneName`

新增资源时，先把资源文件放到合适目录，再在 `ResourceDefinitionDatabase.asset` 中登记。代码里优先传资源条目，动态场景或临时逻辑可以用显式的 `ById` 方法查数据库。

## ResourcesManager

`ResourcesManager` 是底层资源入口：

```csharp
using BokeGameJam.Core;

ResourcesManager.LoadUI(uiResource);
ResourcesManager.LoadSprite(spriteResource);
ResourcesManager.LoadSound(soundResource);
ResourcesManager.GetSceneName(sceneResource);
```

也可以按数据库 id 查询：

```csharp
ResourcesManager.LoadUIById("PausePanel");
ResourcesManager.LoadSpriteById("PlayerIcon");
ResourcesManager.LoadSoundById("Click");
ResourcesManager.GetSceneNameById("NewScene");
```

`ById` 只查 `ResourceDefinitionDatabase.asset`，不会按旧的 `Resources` 路径兜底。

## 场景管理器

脚本：

`Assets/Scripts/Core/GameSceneManager.cs`

使用方式：

```csharp
using BokeGameJam.Core;

GameSceneManager.Instance.LoadScene(sceneResource);
GameSceneManager.Instance.LoadSceneAsync(sceneResource);
GameSceneManager.Instance.LoadSceneById("NewScene");
GameSceneManager.Instance.ReloadCurrentScene();
```

新增场景流程：

1. 创建场景并加入 Build Settings。
2. 在 `ResourceDefinitionDatabase.asset` 的 `scenes` 列表中新增条目。
3. 填写稳定的 `id`，并绑定 `sceneAsset`。

## 音频管理器

脚本：

`Assets/Scripts/Core/GameAudioManager.cs`

音频资源登记在 `ResourceDefinitionDatabase.asset` 的 `sounds` 列表中。`category` 用来区分 `Music` 和 `SFX`，管理器会检查分类，避免把音效当 BGM 播放。

```csharp
using BokeGameJam.Core;

GameAudioManager.Instance.PlayBGM(musicResource);
GameAudioManager.Instance.SwitchBGM(musicResource);
GameAudioManager.Instance.StopBGM();

GameAudioManager.Instance.PlaySFX(sfxResource);
GameAudioManager.Instance.PlaySFXLoop(sfxResource);
GameAudioManager.Instance.StopSFX(sfxResource);
```

动态查询：

```csharp
GameAudioManager.Instance.PlayBGMById("MainTheme");
GameAudioManager.Instance.PlaySFXById("Click");
```

## UI 管理器

脚本：

`Assets/Scripts/UI/UIManager.cs`

UI prefab 登记在 `ResourceDefinitionDatabase.asset` 的 `uiPrefabs` 列表中。

```csharp
using BokeGameJam.UI;

UIManager.Instance.LoadUI(uiResource);
UIManager.Instance.HideUI(uiResource);
UIManager.Instance.CloseUI(uiResource);
UIManager.Instance.CloseAllUI();
```

动态查询：

```csharp
UIManager.Instance.LoadUIById("PausePanel");
UIManager.Instance.HideUIById("PausePanel");
UIManager.Instance.CloseUIById("PausePanel");
```

`UIManager` 只负责 UI 实例生命周期，prefab 引用由 `ResourcesManager` 从数据库中解析。

## 事件管理器

脚本：

`Assets/Scripts/Core/EventManager.cs`

用于简单的全局事件解耦：

```csharp
using BokeGameJam.Core;

EventManager.On("GameStart", OnGameStart);
EventManager.Emit("GameStart");
EventManager.Off("GameStart", OnGameStart);

EventManager.On<int>("ScoreChanged", OnScoreChanged);
EventManager.Emit("ScoreChanged", score);
EventManager.Off<int>("ScoreChanged", OnScoreChanged);
```

事件系统仍使用字符串 key。需要强类型事件时，可以后续再引入事件 Channel 类型的 ScriptableObject。

## 开发约定

- 运行时代码放在 `Assets/Scripts/`，按 `Core`、`Gameplay`、`UI` 分模块。
- 管理器预制体放在 `Assets/Prefabs/Manager/`。
- `.meta` 文件需要跟随资源一起提交。
- 不要提交 `Library/`、`Temp/`、`Logs/`、`UserSettings/` 等 Unity 生成目录。
- 新资源优先登记到 `ResourceDefinitionDatabase.asset`，避免在业务代码里散落资源路径字符串。
