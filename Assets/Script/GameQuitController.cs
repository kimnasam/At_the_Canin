using UnityEngine;

/// <summary>
/// 모든 씬에서 Esc 입력을 감지해 실행 중인 게임을 종료합니다.
/// 씬 오브젝트 연결 없이 게임 시작 시 자동으로 생성됩니다.
/// </summary>
public sealed class GameQuitController : MonoBehaviour
{
    private static GameQuitController instance; // 씬이 바뀌어도 유지되는 종료 입력 관리자입니다.

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
        if (!Input.GetKeyDown(KeyCode.Escape))
        {
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 에디터에서는 Play 모드를 종료해 동작을 바로 확인합니다.
#else
        Application.Quit(); // 빌드된 게임 프로세스를 정상 종료합니다.
#endif
    }
}
