# BokeGameJam

Unity 2022.3（URP 2D）Game Jam 项目。

## 资源目录

```
Assets/
├── Art/                    # 美术资源（场景直接引用）
│   ├── Animations/         # 动画 Clip、Animator Controller
│   ├── Fonts/              # 字体
│   ├── Materials/          # 材质
│   └── Pictures/           # 图片（精灵、背景、UI 图等）
├── Resources/              # 运行时动态加载的资源
│   ├── Audio/
│   │   ├── Music/          # 背景音乐
│   │   └── SFX/            # 音效
│   ├── Prefabs/            # 动态实例化的预制体
│   └── Config/             # ScriptableObject 等配置
├── Prefabs/                # 场景直接引用的预制体（如管理器）
├── Scenes/                 # 场景
├── Scripts/
│   ├── Core/               # 核心系统（场景管理器等）
│   ├── Gameplay/           # 玩法逻辑
│   └── UI/                 # UI 逻辑
├── ScriptableObjects/      # 编辑器配置数据（非动态加载）
└── Settings/               # URP 等渲染设置
```

**分类原则：**
- 需要运行时按名称动态加载的 → 放 `Resources/`
- 场景或预制体直接拖引用的 → 放 `Art/`、`Prefabs/` 等
- 脚本按功能模块分，不要堆在同一文件夹

**Resources 加载路径：** 相对 `Resources/` 文件夹，不含扩展名。  
例：`Resources/Audio/SFX/Click.wav` → `Resources.Load("Audio/SFX/Click")`

---

## 场景管理器（GameSceneManager）

### 注册

1. 创建新场景
2. 场景加入 **Build Settings**（File → Build Settings → Add Open Scenes）
3. 在 `SceneNames.cs` 添加场景名常量

### 使用

```csharp
using BokeGameJam.Core;

GameSceneManager.Instance.LoadScene(SceneNames.Gameplay);        // 同步
GameSceneManager.Instance.LoadSceneAsync(SceneNames.Gameplay);   // 异步
GameSceneManager.Instance.ReloadCurrentScene();                  // 重载当前场景
```

---

## 音频管理器（GameAudioManager）

音频资源放在 `Assets/Resources/Audio/Music/` 与 `Assets/Resources/Audio/SFX/`，运行时通过 `Resources.Load` 加载。

### 注册

1. 将音频文件放入对应 Resources 子文件夹（文件名 = `AudioNames` 常量值）
2. 创建空物体，挂载 `GameAudioManager`，保存为 `Prefabs/GameAudioManager` 预制体
3. 在启动场景拖入预制体，只需放一次
4. 在 `AudioNames.cs` 添加音频名常量

### 播放

```csharp
using BokeGameJam.Core;

// BGM
GameAudioManager.Instance.PlayBGM(AudioNames.Music.MainTheme);   // 播放（循环）
GameAudioManager.Instance.SwitchBGM(AudioNames.Music.Gameplay);    // 切换（淡入淡出）
GameAudioManager.Instance.StopBGM();                             // 停止

// SFX
GameAudioManager.Instance.PlaySFX(AudioNames.SFX.Click);         // 单次
GameAudioManager.Instance.PlaySFXLoop(AudioNames.SFX.Engine);    // 循环
GameAudioManager.Instance.StopSFX(AudioNames.SFX.Engine);         // 停止循环音效
```
