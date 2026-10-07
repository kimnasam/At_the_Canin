using System.Collections;
using UnityEngine;

/// <summary>
/// Main 씬의 검은 화면, 로프, 타이틀, Start 등장 연출을 실행하는 클래스입니다.
/// </summary>
public class MainIntroSequence : MonoBehaviour
{
    [Header("화면 이미지")]
    [SerializeField] private SpriteRenderer blackBackground; // 페이드 아웃할 검은 배경입니다.
    [SerializeField] private SpriteRenderer titleRenderer; // 로프와 함께 페이드 인할 At_the_Canin 이미지입니다.
    [SerializeField] private SpriteRenderer startRenderer; // 로프 연출 뒤 표시할 Start 이미지입니다.

    [Header("애니메이터")]
    [SerializeField] private Animator leftRopeAnimator; // 첫 번째 로프의 등장 애니메이터입니다.
    [SerializeField] private Animator rightRopeAnimator; // 두 번째 로프의 등장 애니메이터입니다.
    [SerializeField] private Animator startAnimator; // Start의 등장 애니메이터입니다.

    [Header("제어 코드")]
    [SerializeField] private MainSceneAudio mainSceneAudio; // 등장 효과음을 재생할 오디오 제어 코드입니다.
    [SerializeField] private StartSceneTransition startSceneTransition; // 등장 후 클릭을 허용할 씬 전환 코드입니다.

    [Header("연출 시간")]
    [SerializeField, Min(0.01f)] private float blackFadeOutDuration = 1f; // 검은 배경이 사라지는 시간입니다.
    [SerializeField, Min(0.01f)] private float titleFadeInDuration = 1f; // 타이틀이 나타나는 시간입니다.
    [SerializeField, Min(0.01f)] private float ropeAnimationDuration = 1f; // 로프 등장 후 Start까지 기다리는 시간입니다.

    private Color blackOriginalColor; // 검은 배경의 원래 색상입니다.
    private Color titleOriginalColor; // 타이틀의 원래 색상입니다.
    private Color startOriginalColor; // Start의 원래 색상입니다.

    /// <summary>
    /// 첫 화면 전에 이미지와 애니메이터를 시작 상태로 설정합니다.
    /// </summary>
    private void Awake()
    {
        if (!AreSettingsValid())
        {
            enabled = false;
            return;
        }

        blackOriginalColor = blackBackground.color;
        titleOriginalColor = titleRenderer.color;
        startOriginalColor = startRenderer.color;
        SetRendererAlpha(blackBackground, blackOriginalColor, 1f);
        SetRendererAlpha(titleRenderer, titleOriginalColor, 0f);
        SetRendererAlpha(startRenderer, startOriginalColor, 0f);
        leftRopeAnimator.enabled = false;
        rightRopeAnimator.enabled = false;
        startAnimator.enabled = false;
    }

    /// <summary>
    /// Main 씬의 등장 연출을 시작합니다.
    /// </summary>
    private void Start()
    {
        if (enabled)
        {
            StartCoroutine(PlayIntroSequence());
        }
    }

    /// <summary>
    /// 검은 배경, 로프와 타이틀, Start 순서로 등장시킵니다.
    /// </summary>
    private IEnumerator PlayIntroSequence()
    {
        yield return FadeBlackBackgroundOut();
        blackBackground.gameObject.SetActive(false);

        leftRopeAnimator.enabled = true;
        rightRopeAnimator.enabled = true;
        mainSceneAudio.PlayRopeDropSound();
        yield return PlayRopeAndTitleSequence();

        SetRendererAlpha(startRenderer, startOriginalColor, 1f);
        startAnimator.enabled = true;
        mainSceneAudio.PlayStartDropSound();
        startSceneTransition.EnableClick();
    }

    /// <summary>
    /// 검은 배경을 불투명 상태에서 투명 상태로 변경합니다.
    /// </summary>
    private IEnumerator FadeBlackBackgroundOut()
    {
        float elapsedTime = 0f; // 검은 배경 페이드가 진행된 시간입니다.

        while (elapsedTime < blackFadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsedTime / blackFadeOutDuration); // 현재 검은 배경 투명도입니다.
            SetRendererAlpha(blackBackground, blackOriginalColor, alpha);
            yield return null;
        }

        SetRendererAlpha(blackBackground, blackOriginalColor, 0f);
    }

    /// <summary>
    /// 두 로프가 등장하는 동안 타이틀을 페이드 인합니다.
    /// </summary>
    private IEnumerator PlayRopeAndTitleSequence()
    {
        float elapsedTime = 0f; // 로프 애니메이션이 진행된 시간입니다.

        while (elapsedTime < ropeAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float titleAlpha = Mathf.Clamp01(elapsedTime / titleFadeInDuration); // 현재 타이틀 투명도입니다.
            SetRendererAlpha(titleRenderer, titleOriginalColor, titleAlpha);
            yield return null;
        }

        SetRendererAlpha(titleRenderer, titleOriginalColor, 1f);
    }

    /// <summary>
    /// 이미지의 RGB 색상을 유지하면서 투명도만 변경합니다.
    /// </summary>
    private void SetRendererAlpha(SpriteRenderer targetRenderer, Color originalColor, float alpha)
    {
        Color changedColor = originalColor; // 대상 이미지에 적용할 색상입니다.
        changedColor.a = alpha;
        targetRenderer.color = changedColor;
    }

    /// <summary>
    /// 등장 연출에 필요한 항목이 모두 연결되었는지 확인합니다.
    /// </summary>
    private bool AreSettingsValid()
    {
        bool hasRenderers = blackBackground != null && titleRenderer != null && startRenderer != null; // 이미지 연결 여부입니다.
        bool hasAnimators = leftRopeAnimator != null && rightRopeAnimator != null && startAnimator != null; // 애니메이터 연결 여부입니다.
        bool hasControllers = mainSceneAudio != null && startSceneTransition != null; // 보조 제어 코드 연결 여부입니다.

        if (!hasRenderers || !hasAnimators || !hasControllers)
        {
            Debug.LogError("MainIntroSequence의 이미지, 애니메이터, MainSceneAudio, StartSceneTransition을 모두 연결해 주세요.", this);
            return false;
        }

        return true;
    }
}
