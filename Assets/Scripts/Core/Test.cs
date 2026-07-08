using BokeGameJam.Core;
using UnityEngine;

/// <summary>
/// 场景切换测试：左键点击切换到配置的目标场景。
/// targetScene 填 Id、场景资源或场景名任意一项即可；多项时按 Id → 场景 → 场景名 取值。
/// </summary>
public class Test : MonoBehaviour
{
    [SerializeField] private ResourceDefinitionDatabase.SceneResource targetScene;

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            SwitchToTargetScene();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        targetScene?.SyncSceneNameFromAsset();
    }
#endif

    private void SwitchToTargetScene()
    {
        if (GameSceneManager.Instance == null)
        {
            Debug.LogError("[Test] 未找到 GameSceneManager，请在启动场景放置预制体。");
            return;
        }

        string sceneName = ResourcesManager.GetSceneName(targetScene);
        Debug.Log($"[Test] 切换到 {sceneName}");
        if (string.IsNullOrEmpty(sceneName))
            return;

        GameSceneManager.Instance.LoadScene(targetScene);
    }
}
