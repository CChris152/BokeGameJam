# BokeGameJam

Unity 2022.3（URP 2D）Game Jam 项目。

## 资源目录

```
Assets/
├── Art/                    # 美术资源
│   ├── Animations/         # 动画 Clip、Animator Controller
│   ├── Fonts/              # 字体
│   ├── Materials/          # 材质
│   └── Pictures/           # 图片（精灵、背景、UI 图等）
├── Audio/
│   ├── Music/              # 背景音乐
│   └── SFX/                # 音效
├── Prefabs/                # 预制体
├── Scenes/                 # 场景
├── Scripts/
│   ├── Core/               # 核心系统（场景管理器等）
│   ├── Gameplay/           # 玩法逻辑
│   └── UI/                 # UI 逻辑
├── ScriptableObjects/      # 配置数据（ScriptableObject）
└── Settings/               # URP 等渲染设置
```

**分类原则：**
- 按资源类型分目录（Art、Audio），再按用途分子文件夹
- 脚本按功能模块分，不要堆在同一文件夹
- 预制体统一放在 `Prefabs/`，可按需再分子目录

---

## 场景管理器（GameSceneManager）

### 注册

1. 创建新场景
2. 场景加入 **Build Settings**（File → Build Settings → Add Open Scenes）
4. 在 `SceneNames.cs` 添加场景名常量

### 切换

```csharp
using BokeGameJam.Core;

GameSceneManager.Instance.LoadScene(SceneNames.Gameplay);        // 同步
GameSceneManager.Instance.LoadSceneAsync(SceneNames.Gameplay);   // 异步
GameSceneManager.Instance.ReloadCurrentScene();                  // 重载当前场景
```
