using BokeGameJam.Core;
using UnityEngine;

/// <summary>
/// 场景切换测试：左键点击切换到配置的目标场景。
/// </summary>
public class Test : MonoBehaviour
{
    [SerializeField] private ResourceDefinitionDatabase.SceneResource targetScene;

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            SwitchToTargetScene();
    }

    private void SwitchToTargetScene()
    {
        if (GameSceneManager.Instance == null)
        {
            Debug.LogError("[Test] 未找到 GameSceneManager，请在启动场景放置预制体。");
            return;
        }

        Debug.Log($"[Test] 切换到 {targetScene?.SceneName}");
        GameSceneManager.Instance.LoadScene(targetScene);
    }
}
