using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 씬이 시작될 때 검은 UI 이미지를 서서히 투명하게 만들어 화면을 드러냅니다.
/// </summary>
[RequireComponent(typeof(Image))]
public class SceneFadeIn : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float fadeDuration = 1f; // 검은 화면이 완전히 사라질 때까지 걸리는 시간입니다.
    [SerializeField] private bool ignoreTimeScale = true; // 게임이 일시 정지된 상태에서도 페이드를 진행할지 결정합니다.
    [SerializeField] private bool disableAfterFade = true; // 페이드가 끝난 뒤 검은 이미지 오브젝트를 비활성화할지 결정합니다.

    private Image fadeImage; // 화면 전체를 덮고 투명도가 변경될 UI 이미지입니다.
    private Color originalColor; // Inspector에서 설정한 검은 이미지의 원래 색상입니다.

    private void Awake()
    {
        fadeImage = GetComponent<Image>();
        originalColor = fadeImage.color;

        fadeImage.gameObject.SetActive(true);
        fadeImage.raycastTarget = true;
        SetAlpha(1f);
    }

    private IEnumerator Start()
    {
        float elapsedTime = 0f; // 페이드가 시작된 후 흐른 시간입니다.

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / fadeDuration); // 현재 페이드의 0~1 진행 비율입니다.
            SetAlpha(1f - progress);
            yield return null;
        }

        SetAlpha(0f);
        fadeImage.raycastTarget = false;

        if (disableAfterFade)
        {
            fadeImage.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 이미지의 원래 RGB 색상은 유지하고 투명도만 변경합니다.
    /// </summary>
    private void SetAlpha(float alpha)
    {
        Color changedColor = originalColor;
        changedColor.a = alpha;
        fadeImage.color = changedColor;
    }
}
