using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 늑대에게 잡혔을 때 게임을 멈추고 게임 오버 화면과 재시작을 관리합니다.
/// </summary>
public class GameOverController : MonoBehaviour
{
    [SerializeField] private GameObject gameOverPanel; // 늑대에게 잡힌 뒤 표시할 전체 게임 오버 UI입니다.
    [SerializeField] private string restartSceneName = "Main"; // 재시작 버튼이 불러올 첫 씬 이름입니다.

    private bool isGameOver; // 게임 오버가 중복 실행되는 것을 막는 값입니다.

    private void Awake()
    {
        gameOverPanel.SetActive(false);
    }

    public void ShowGameOver()
    {
        if (isGameOver)
        {
            return;
        }

        isGameOver = true;
        gameOverPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        if (GameTimeManager.Instance != null)
        {
            Destroy(GameTimeManager.Instance.gameObject);
        }

        SceneManager.LoadScene(restartSceneName);
    }
}
