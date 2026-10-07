using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 검은 화면에서 로고와 제목을 즉시 표시한 뒤 다음 씬으로 전환하는 클래스입니다.
/// </summary>
public class StartupLogo : MonoBehaviour
{
    [Header("화면 요소")]
    [SerializeField] private SpriteRenderer logoRenderer; // rogo 스프라이트의 투명도를 조절합니다.
    [SerializeField] private TMP_Text titleText; // fox cave 제목의 내용과 투명도를 조절합니다.

    [Header("사운드")]
    [SerializeField] private AudioSource audioSource; // 시작 효과음을 재생하는 오디오 소스입니다.
    [SerializeField] private AudioClip introSound; // 로고가 나타나는 순간 재생할 효과음입니다.

    [Header("연출 시간")]
    [SerializeField, Min(0f)] private float startDelay = 1f; // 검은 화면을 유지한 후 로고가 나타날 때까지의 시간입니다.
    [SerializeField, Min(0f)] private float displayDuration = 2f; // 로고와 제목을 표시한 상태로 유지하는 시간입니다.
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 1f; // 로고와 제목이 사라지는 데 걸리는 시간입니다.

    [Header("씬 전환")]
    [SerializeField] private string nextSceneName; // 연출이 끝난 후 불러올 씬의 이름입니다.

    private const string GameTitle = "fox cave"; // 화면에 표시할 게임 제목입니다.
    private Color originalLogoColor; // Inspector에서 설정한 로고의 원래 색상입니다.
    private Color originalTitleColor; // Inspector에서 설정한 제목의 원래 색상입니다.

    /// <summary>
    /// 첫 화면이 그려지기 전에 로고와 제목을 완전히 숨깁니다.
    /// </summary>
    private void Awake()
    {
        if (!AreSettingsValid())
        {
            enabled = false;
            return;
        }

        originalLogoColor = logoRenderer.color;
        originalTitleColor = titleText.color;
        titleText.text = GameTitle;
        SetContentAlpha(0f);
    }

    /// <summary>
    /// 검은 화면 대기 후 시작 연출을 실행합니다.
    /// </summary>
    private void Start()
    {
        if (enabled)
        {
            StartCoroutine(PlayIntro());
        }
    }

    /// <summary>
    /// 대기, 즉시 표시, 페이드 아웃, 씬 전환을 순서대로 실행합니다.
    /// </summary>
    private IEnumerator PlayIntro()
    {
        yield return new WaitForSecondsRealtime(startDelay);

        audioSource.PlayOneShot(introSound);
        SetContentAlpha(1f);

        yield return new WaitForSecondsRealtime(displayDuration);
        yield return FadeOut();

        SceneManager.LoadScene(nextSceneName);
    }

    /// <summary>
    /// 로고와 제목을 함께 서서히 투명하게 만듭니다.
    /// </summary>
    private IEnumerator FadeOut()
    {
        float elapsedTime = 0f; // 현재까지 진행된 페이드 아웃 시간입니다.

        while (elapsedTime < fadeOutDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsedTime / fadeOutDuration); // 현재 적용할 투명도입니다.
            SetContentAlpha(alpha);
            yield return null;
        }

        SetContentAlpha(0f);
    }

    /// <summary>
    /// 로고와 제목에 동일한 투명도를 적용합니다.
    /// </summary>
    private void SetContentAlpha(float alpha)
    {
        Color logoColor = originalLogoColor; // 로고에 적용할 색상입니다.
        Color titleColor = originalTitleColor; // 제목에 적용할 색상입니다.

        logoColor.a = alpha;
        titleColor.a = alpha;
        logoRenderer.color = logoColor;
        titleText.color = titleColor;
    }

    /// <summary>
    /// 시작 연출에 필요한 Inspector 설정이 모두 연결되었는지 확인합니다.
    /// </summary>
    private bool AreSettingsValid()
    {
        if (logoRenderer == null || titleText == null || audioSource == null || introSound == null)
        {
            Debug.LogError("StartupLogo의 로고, 제목, Audio Source, Intro Sound를 모두 연결해 주세요.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogError("StartupLogo의 Next Scene Name을 입력해 주세요.", this);
            return false;
        }

        return true;
    }
}
