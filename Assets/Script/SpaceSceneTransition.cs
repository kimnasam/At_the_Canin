using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Space 키를 누르면 Inspector에서 지정한 다음 씬으로 이동하는 클래스입니다.
/// </summary>
public sealed class SpaceSceneTransition : MonoBehaviour
{
    [Header("씬 이동 설정")]
    [SerializeField] private string nextSceneName; // Space 키를 눌렀을 때 불러올 씬의 이름입니다.

    private bool isChangingScene; // 중복 입력으로 같은 씬을 여러 번 불러오는 것을 막는 값입니다.

    /// <summary>
    /// 매 프레임 Space 입력을 확인하고 지정된 씬으로 이동합니다.
    /// </summary>
    private void Update()
    {
        if (isChangingScene || !Input.GetKeyDown(KeyCode.Space))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogError("SpaceSceneTransition에 Next Scene Name을 입력해 주세요.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"Build Settings에 '{nextSceneName}' 씬이 없거나 씬 이름이 올바르지 않습니다.", this);
            return;
        }

        isChangingScene = true;
        SceneManager.LoadScene(nextSceneName);
    }
}
