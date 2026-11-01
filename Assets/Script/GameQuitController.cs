using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 모든 씬에서 Esc 입력을 감지해 메인 씬으로 돌아가거나 게임을 종료합니다.
/// 씬 오브젝트 연결 없이 게임 시작 시 자동으로 생성됩니다.
/// </summary>
public sealed class GameQuitController : MonoBehaviour
{
    private const string MainSceneName = "Main"; // 게임 종료 여부를 구분하고 초기화 후 돌아갈 메인 씬 이름입니다.
    private static GameQuitController instance; // 씬이 바뀌어도 유지되는 ESC 입력 관리자입니다.
    private bool isHandlingEscape; // 한 프레임에 초기화와 씬 이동이 중복 실행되는 것을 막는 값입니다.

    /// <summary>
    /// 첫 씬이 열리기 전에 종료 입력 관리자를 한 번만 만듭니다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateController()
    {
        if (instance != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject(nameof(GameQuitController)); // 자동 생성되는 종료 입력 전용 오브젝트입니다.
        instance = controllerObject.AddComponent<GameQuitController>();
        DontDestroyOnLoad(controllerObject);
    }

    private void Update()
    {
        if (isHandlingEscape || !Input.GetKeyDown(KeyCode.Escape))
        {
            return;
        }

        isHandlingEscape = true;

        if (SceneManager.GetActiveScene().name != MainSceneName)
        {
            ResetGameAndReturnToMain();
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 에디터에서는 Play 모드를 종료해 동작을 바로 확인합니다.
#else
        Application.Quit(); // 빌드된 게임 프로세스를 정상 종료합니다.
#endif
    }

    /// <summary>
    /// 진행 중인 게임의 전역 상태를 초기화하고 Main 씬을 새로 불러옵니다.
    /// </summary>
    private void ResetGameAndReturnToMain()
    {
        Time.timeScale = 1f;
        GameplayDialogueLock.Reset();
        StageOpeningSequence.ResetOpeningRequest();
        StageTeleportTrigger2D.ActivePortals.Clear();
        GameTimeManager.RequestNewGame();
        SceneManager.LoadScene(MainSceneName);
        isHandlingEscape = false;
    }
}
