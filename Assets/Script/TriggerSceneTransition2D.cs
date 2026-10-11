using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어가 2D Trigger 안에 있을 때 E 안내를 표시하고 다음 씬으로 이동합니다.
/// 카운터 출입구는 아침에만 이용할 수 있습니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TriggerSceneTransition2D : MonoBehaviour
{
    [Header("플레이어 감지")]
    [SerializeField] private string playerTag = "Player"; // Trigger에서 플레이어를 구분할 태그입니다.
    [SerializeField] private KeyCode interactionKey = KeyCode.E; // 씬 이동을 시작할 상호작용 키입니다.

    [Header("화면 오브젝트")]
    [SerializeField] private SpriteRenderer interactionGuide; // 플레이어에게 보여 줄 E 안내 이미지입니다.
    [SerializeField] private SpriteRenderer blackScreen; // 씬 이동 전에 화면을 가릴 검은 이미지입니다.

    [Header("안내 이미지 연출")]
    [SerializeField, Min(0.01f)] private float guideFadeDuration = 0.25f; // E 안내가 나타나거나 사라지는 시간입니다.

    [Header("씬 전환")]
    [SerializeField, Min(0.01f)] private float fadeInDuration = 1f; // 검은 화면의 불투명도가 0에서 1이 되는 시간입니다.
    [SerializeField] private string nextSceneName; // 페이드가 끝난 뒤 불러올 씬 이름입니다.

    private Color interactionGuideColor; // 안내 이미지의 원래 RGB와 불투명도를 보관합니다.
    private Color blackScreenColor; // 검은 화면의 원래 RGB와 불투명도를 보관합니다.
    private bool isPlayerInside; // 플레이어가 현재 Trigger 범위 안에 있는지 나타냅니다.
    private bool isChangingScene; // 중복 입력으로 씬 전환이 여러 번 실행되는 것을 막습니다.
    private Coroutine guideFadeCoroutine; // 현재 실행 중인 안내 이미지 페이드 작업입니다.

    private void Awake()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>(); // 플레이어 감지 범위로 사용할 Collider2D입니다.

        if (!triggerCollider.isTrigger)
        {
            triggerCollider.isTrigger = true;
            Debug.LogWarning("TriggerSceneTransition2D의 Collider2D가 자동으로 Is Trigger 상태로 변경되었습니다.", this);
        }

        if (!AreSettingsValid())
        {
            enabled = false;
            return;
        }

        interactionGuideColor = interactionGuide.color;
        blackScreenColor = blackScreen.color;
        SetRendererAlpha(interactionGuide, interactionGuideColor, 0f);
        SetRendererAlpha(blackScreen, blackScreenColor, 0f);
    }

    private void Start()
    {
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.TimeChanged += HandleTimeChanged;
        }
        else
        {
            Debug.LogError("카운터 출입구를 사용하려면 씬에 GameTimeManager가 필요합니다.", this);
        }
    }

    private void OnDestroy()
    {
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.TimeChanged -= HandleTimeChanged;
        }
    }

    private void Update()
    {
        if (isPlayerInside && !isChangingScene && IsInteractionAvailable() && Input.GetKeyDown(interactionKey))
        {
            StartCoroutine(ChangeScene());
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isChangingScene || !other.CompareTag(playerTag))
        {
            return;
        }

        isPlayerInside = true;
        StartGuideFade(IsInteractionAvailable() ? 1f : 0f);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        isPlayerInside = false;

        if (!isChangingScene)
        {
            StartGuideFade(0f);
        }
    }

    /// <summary>
    /// E 안내를 숨긴 뒤 화면을 검게 만들고 다음 씬을 불러옵니다.
    /// </summary>
    private IEnumerator ChangeScene()
    {
        isChangingScene = true;
        StartGuideFade(0f);

        float elapsedTime = 0f; // 검은 화면 페이드가 진행된 누적 시간입니다.

        while (elapsedTime < fadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            float fadeRatio = Mathf.Clamp01(elapsedTime / fadeInDuration); // 현재 페이드의 0~1 진행 비율입니다.
            SetRendererAlpha(blackScreen, blackScreenColor, fadeRatio);
            yield return null;
        }

        SetRendererAlpha(blackScreen, blackScreenColor, 1f);
        SceneManager.LoadScene(nextSceneName);
    }

    /// <summary>
    /// 실행 중인 안내 페이드를 멈추고 목표 불투명도로 새 페이드를 시작합니다.
    /// </summary>
    private void StartGuideFade(float targetAlpha)
    {
        if (guideFadeCoroutine != null)
        {
            StopCoroutine(guideFadeCoroutine);
        }

        guideFadeCoroutine = StartCoroutine(FadeInteractionGuide(targetAlpha));
    }

    /// <summary>
    /// 시간이 바뀌면 플레이어 위치와 이용 가능 여부에 맞춰 E 안내를 갱신합니다.
    /// </summary>
    private void HandleTimeChanged(GameTimeManager.TimePeriod time)
    {
        if (!isChangingScene)
        {
            StartGuideFade(isPlayerInside && IsInteractionAvailable() ? 1f : 0f);
        }
    }

    /// <summary>
    /// 현재 시간이 아침일 때만 카운터 상호작용을 허용합니다.
    /// </summary>
    private bool IsInteractionAvailable()
    {
        return GameTimeManager.Instance != null
            && GameTimeManager.Instance.CurrentTime == GameTimeManager.TimePeriod.Morning;
    }

    /// <summary>
    /// E 안내 이미지의 불투명도를 부드럽게 변경합니다.
    /// </summary>
    private IEnumerator FadeInteractionGuide(float targetAlpha)
    {
        float startAlpha = interactionGuide.color.a; // 페이드 시작 시점의 불투명도입니다.
        float elapsedTime = 0f; // 안내 이미지 페이드의 누적 시간입니다.

        while (elapsedTime < guideFadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float fadeRatio = Mathf.Clamp01(elapsedTime / guideFadeDuration); // 현재 페이드의 0~1 진행 비율입니다.
            float currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, fadeRatio); // 이번 프레임에 적용할 불투명도입니다.
            SetRendererAlpha(interactionGuide, interactionGuideColor, currentAlpha);
            yield return null;
        }

        SetRendererAlpha(interactionGuide, interactionGuideColor, targetAlpha);
        guideFadeCoroutine = null;
    }

    /// <summary>
    /// SpriteRenderer의 RGB 색상은 유지하고 불투명도만 변경합니다.
    /// </summary>
    private void SetRendererAlpha(SpriteRenderer targetRenderer, Color originalColor, float alpha)
    {
        Color changedColor = originalColor; // 대상 이미지에 새로 적용할 색상입니다.
        changedColor.a = alpha;
        targetRenderer.color = changedColor;
    }

    /// <summary>
    /// 실행에 필요한 이미지, 태그, 다음 씬 이름이 연결되어 있는지 검사합니다.
    /// </summary>
    private bool AreSettingsValid()
    {
        bool hasRenderers = interactionGuide != null && blackScreen != null; // 두 SpriteRenderer가 연결되었는지 나타냅니다.
        bool hasPlayerTag = !string.IsNullOrWhiteSpace(playerTag); // 플레이어 태그가 입력되었는지 나타냅니다.
        bool hasNextScene = !string.IsNullOrWhiteSpace(nextSceneName); // 다음 씬 이름이 입력되었는지 나타냅니다.

        if (!hasRenderers || !hasPlayerTag || !hasNextScene)
        {
            Debug.LogError("TriggerSceneTransition2D의 안내 이미지, 검은 화면, 플레이어 태그, 다음 씬 이름을 모두 설정해 주세요.", this);
            return false;
        }

        return true;
    }
}
