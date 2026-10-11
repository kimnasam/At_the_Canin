using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Death 씬이 시작되면 사운드 재생과 첫 번째 오브젝트 확대를 실행하고,
/// 사운드가 끝나면 두 번째 오브젝트를 페이드 인합니다.
/// </summary>
public class DeathSceneSequence : MonoBehaviour
{
    [Header("필수 연결")]
    [SerializeField] private AudioSource deathAudioSource; // Death 씬에서 지정한 사운드를 재생할 오디오 소스입니다.
    [SerializeField] private GameObject scaleTargetObject; // 씬 시작 시 화면 중앙에서 확대할 첫 번째 오브젝트입니다.
    [SerializeField] private GameObject fadeTargetObject; // 사운드가 끝난 뒤 페이드 인할 두 번째 오브젝트입니다.
    [SerializeField] private string nextSceneName = "Main"; // 페이드 인이 끝난 뒤 이동할 씬의 이름입니다.

    [Header("연출 설정")]
    [SerializeField, Min(0f)] private float targetScale = 0.5f; // 확대가 끝났을 때 첫 번째 오브젝트의 최종 스케일입니다.
    [SerializeField, Min(0f)] private float fadeDuration = 1f; // 두 번째 오브젝트가 완전히 나타나는 시간(초)입니다.

    private const float ScaleDuration = 0.2f; // 첫 번째 오브젝트가 0부터 목표 크기까지 점점 커지는 고정 시간(초)입니다.
    private Transform scaleTargetTransform; // 첫 번째 오브젝트의 크기를 변경하는 Transform입니다.
    private CanvasGroup fadeTargetCanvasGroup; // 두 번째 오브젝트와 자식 UI 전체의 투명도를 변경하는 CanvasGroup입니다.
    private SpriteRenderer[] fadeTargetSpriteRenderers; // 두 번째 오브젝트와 자식에 있는 일반 스프라이트 렌더러들입니다.
    private float[] spriteTargetAlphas; // 각 스프라이트가 페이드 인을 마쳤을 때 사용할 원래 투명도입니다.

    private void Awake()
    {
        if (scaleTargetObject == null || fadeTargetObject == null)
        {
            Debug.LogError("DeathSceneSequence에 Scale Target Object 또는 Fade Target Object가 연결되지 않았습니다.", this);
            enabled = false;
            return;
        }

        scaleTargetTransform = scaleTargetObject.transform;
        fadeTargetObject.SetActive(true);
        fadeTargetCanvasGroup = fadeTargetObject.GetComponent<CanvasGroup>();

        if (fadeTargetCanvasGroup == null)
        {
            fadeTargetCanvasGroup = fadeTargetObject.AddComponent<CanvasGroup>();
        }

        fadeTargetSpriteRenderers = fadeTargetObject.GetComponentsInChildren<SpriteRenderer>(true);
        spriteTargetAlphas = new float[fadeTargetSpriteRenderers.Length];

        for (int i = 0; i < fadeTargetSpriteRenderers.Length; i++)
        {
            spriteTargetAlphas[i] = fadeTargetSpriteRenderers[i].color.a;
        }

        scaleTargetTransform.localScale = Vector3.zero;
        SetFadeProgress(0f);
        fadeTargetCanvasGroup.interactable = false;
        fadeTargetCanvasGroup.blocksRaycasts = false;
    }

    private void Start()
    {
        StartCoroutine(PlayDeathSequence());
    }

    /// <summary>
    /// 사운드와 첫 번째 오브젝트 확대를 시작하고, 사운드 종료 후 두 번째 오브젝트를 페이드 인합니다.
    /// </summary>
    private IEnumerator PlayDeathSequence()
    {
        if (deathAudioSource != null && deathAudioSource.clip != null)
        {
            deathAudioSource.Play();
        }
        else
        {
            Debug.LogWarning("DeathSceneSequence에 재생할 AudioSource 또는 AudioClip이 없습니다.", this);
        }

        yield return ScaleObject();

        // 오디오가 재생 중인 동안 기다립니다.
        while (deathAudioSource != null && deathAudioSource.isPlaying)
        {
            yield return null;
        }

        yield return FadeInObject();
        SceneManager.LoadScene(nextSceneName);
    }

    /// <summary>
    /// 첫 번째 오브젝트를 0 스케일에서 지정한 목표 스케일까지 빠르게 확대합니다.
    /// </summary>
    private IEnumerator ScaleObject()
    {
        float elapsedTime = 0f; // 확대 연출이 시작된 뒤 흐른 시간입니다.

        while (elapsedTime < ScaleDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsedTime / ScaleDuration); // 현재 확대 진행률입니다.

            scaleTargetTransform.localScale = Vector3.one * Mathf.Lerp(0f, targetScale, progress);
            yield return null;
        }

        scaleTargetTransform.localScale = Vector3.one * targetScale;
    }

    /// <summary>
    /// 두 번째 오브젝트와 그 자식 UI의 투명도를 0에서 1까지 올려 페이드 인합니다.
    /// </summary>
    private IEnumerator FadeInObject()
    {
        if (fadeDuration <= 0f)
        {
            ShowFadeTarget();
            yield break;
        }

        float elapsedTime = 0f; // 페이드 인이 시작된 뒤 흐른 시간입니다.

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsedTime / fadeDuration); // 현재 페이드 인 진행률입니다.

            SetFadeProgress(progress);
            yield return null;
        }

        ShowFadeTarget();
    }

    /// <summary>
    /// 페이드 대상이 완전히 나타난 뒤 UI 입력도 받을 수 있게 설정합니다.
    /// </summary>
    private void ShowFadeTarget()
    {
        SetFadeProgress(1f);
        fadeTargetCanvasGroup.interactable = true;
        fadeTargetCanvasGroup.blocksRaycasts = true;
    }

    /// <summary>
    /// UI와 일반 스프라이트 양쪽에 현재 페이드 진행률을 적용합니다.
    /// </summary>
    private void SetFadeProgress(float progress)
    {
        float clampedProgress = Mathf.Clamp01(progress); // 투명도 계산에 사용할 0부터 1 사이의 페이드 진행률입니다.
        fadeTargetCanvasGroup.alpha = clampedProgress;

        for (int i = 0; i < fadeTargetSpriteRenderers.Length; i++)
        {
            Color spriteColor = fadeTargetSpriteRenderers[i].color; // 현재 스프라이트의 RGB 색상을 유지하기 위한 색상 값입니다.
            spriteColor.a = spriteTargetAlphas[i] * clampedProgress;
            fadeTargetSpriteRenderers[i].color = spriteColor;
        }
    }
}
