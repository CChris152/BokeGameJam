using BokeGameJam.Core;
using UnityEngine;

/// <summary>
/// 场景切换测试：左键点击切换到 NewScene。
/// </summary>
public class Test : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            SwitchToNewScene();
    }

    private void SwitchToNewScene()
    {
        if (GameSceneManager.Instance == null)
        {
            Debug.LogError("[Test] 未找到 GameSceneManager，请在启动场景放置预制体。");
            return;
        }

        Debug.Log($"[Test] 切换到 {SceneNames.NewScene}");
        GameSceneManager.Instance.LoadScene(SceneNames.NewScene);
    }
}
