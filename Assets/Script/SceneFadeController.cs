using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 화면을 검게 페이드아웃한 뒤 지정한 씬으로 이동합니다.
/// </summary>
public class SceneFadeController : MonoBehaviour
{
    [SerializeField] private Image blackScreen; // 화면 전체를 덮는 검은 UI 이미지입니다.
    [SerializeField, Min(0.01f)] private float fadeDuration = 1f; // 완전히 검어질 때까지 걸리는 시간입니다.

    private void Awake()
    {
        SetAlpha(0f);
        blackScreen.gameObject.SetActive(false);
    }

    public IEnumerator FadeOutAndLoad(string sceneName)
    {
        blackScreen.gameObject.SetActive(true);
        float elapsedTime = 0f; // 현재까지 진행된 페이드 시간입니다.

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            SetAlpha(Mathf.Clamp01(elapsedTime / fadeDuration));
            yield return null;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void SetAlpha(float alpha)
    {
        Color color = blackScreen.color; // 검은 이미지의 현재 색상입니다.
        color.a = alpha;
        blackScreen.color = color;
    }
}
