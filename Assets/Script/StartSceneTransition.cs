using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Start 클릭 후 퇴장 애니메이션과 검은 화면을 실행하고 다음 씬으로 이동하는 클래스입니다.
/// </summary>
public class StartSceneTransition : MonoBehaviour
{
    [Header("클릭과 화면 이미지")]
    [SerializeField] private SpriteRenderer startRenderer; // 클릭 영역으로 사용할 Start 이미지입니다.
    [SerializeField] private SpriteRenderer blackBackground; // 퇴장 중 페이드 인할 검은 배경입니다.
    [SerializeField] private Camera clickCamera; // Start의 화면상 클릭 영역을 계산할 카메라입니다.

    [Header("스토리 텍스트")]
    [SerializeField] private Text storyText; // 검은 화면에서 표시할 대략적인 스토리 Legacy Text입니다.
    [SerializeField] private Text pressSpaceText; // 스페이스바 입력을 안내할 Press Space Legacy Text입니다.

    [Header("애니메이터")]
    [SerializeField] private Animator leftRopeAnimator; // 첫 번째 로프의 퇴장 애니메이터입니다.
    [SerializeField] private Animator rightRopeAnimator; // 두 번째 로프의 퇴장 애니메이터입니다.
    [SerializeField] private Animator startAnimator; // Start의 퇴장 애니메이터입니다.

    [Header("퇴장 애니메이션 이름")]
    [SerializeField] private string leftRopeOutAnimationName; // 클릭할 때 바로 재생할 첫 번째 로프의 Animator 상태 이름입니다.
    [SerializeField] private string rightRopeOutAnimationName; // 클릭할 때 바로 재생할 두 번째 로프의 Animator 상태 이름입니다.
    [SerializeField] private string startOutAnimationName; // 클릭할 때 바로 재생할 Start의 Animator 상태 이름입니다.

    [Header("씬 전환")]
    [SerializeField, Min(0.01f)] private float blackFadeInDuration = 1f; // 검은 배경이 나타나는 시간입니다.
    [SerializeField, Min(0.01f)] private float exitAnimationDuration = 1f; // 퇴장 애니메이션을 기다리는 시간입니다.
    [SerializeField, Min(0.01f)] private float storyFadeInDuration = 1f; // 스토리 텍스트가 나타나는 시간입니다.
    [SerializeField, Min(0.1f)] private float pressSpacePulseDuration = 1f; // Press Space가 한 번 밝아졌다 어두워지는 시간입니다.
    [SerializeField, Min(0.01f)] private float textFadeOutDuration = 1f; // Space 입력 후 두 텍스트가 함께 사라지는 시간입니다.
    [SerializeField] private string nextSceneName; // 연출 뒤 불러올 씬 이름입니다.

    private Color blackOriginalColor; // 검은 배경의 원래 색상입니다.
    private Color storyOriginalColor; // 스토리 텍스트의 원래 색상입니다.
    private Color pressSpaceOriginalColor; // Press Space 텍스트의 원래 색상입니다.
    private const float PressSpaceDelay = 3f; // 스토리가 나타난 뒤 입력 안내를 표시하기까지의 고정 대기 시간입니다.
    private Camera sceneCamera; // 클릭 판정에 실제로 사용할 카메라입니다.
    private bool canClickStart; // Start 클릭이 허용된 상태인지 나타냅니다.
    private bool isChangingScene; // 중복 씬 전환을 방지하는 값입니다.

    /// <summary>
    /// 카메라와 검은 배경 색상을 준비하고 설정을 검사합니다.
    /// </summary>
    private void Awake()
    {
        sceneCamera = clickCamera != null ? clickCamera : Camera.main;

        if (storyText != null && pressSpaceText != null)
        {
            storyOriginalColor = storyText.color;
            pressSpaceOriginalColor = pressSpaceText.color;
            SetTextAlpha(storyText, storyOriginalColor, 0f);
            SetTextAlpha(pressSpaceText, pressSpaceOriginalColor, 0f);
        }

        if (!AreSettingsValid())
        {
            enabled = false;
            return;
        }

        blackOriginalColor = blackBackground.color;
        canClickStart = false;
    }

    /// <summary>
    /// 클릭이 허용된 뒤 Start 이미지가 눌렸는지 확인합니다.
    /// </summary>
    private void Update()
    {
        if (!canClickStart || isChangingScene || !Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (IsMouseOverStart())
        {
            StartCoroutine(PlayExitSequence());
        }
    }

    /// <summary>
    /// 등장 연출이 끝난 뒤 Start 클릭을 허용합니다.
    /// </summary>
    public void EnableClick()
    {
        if (enabled && !isChangingScene)
        {
            canClickStart = true;
        }
    }

    /// <summary>
    /// 퇴장 애니메이션과 검은 화면을 실행한 뒤 다음 씬을 불러옵니다.
    /// </summary>
    private IEnumerator PlayExitSequence()
    {
        isChangingScene = true;
        canClickStart = false;
        leftRopeAnimator.Play(leftRopeOutAnimationName, 0, 0f);
        rightRopeAnimator.Play(rightRopeOutAnimationName, 0, 0f);
        startAnimator.Play(startOutAnimationName, 0, 0f);

        blackBackground.gameObject.SetActive(true);
        SetBlackBackgroundAlpha(0f);

        float elapsedTime = 0f; // 퇴장 연출이 진행된 시간입니다.
        float totalExitDuration = Mathf.Max(blackFadeInDuration, exitAnimationDuration); // 모든 퇴장 연출에 필요한 전체 시간입니다.

        while (elapsedTime < totalExitDuration)
        {
            elapsedTime += Time.deltaTime;
            float blackAlpha = Mathf.Clamp01(elapsedTime / blackFadeInDuration); // 현재 검은 배경 투명도입니다.
            SetBlackBackgroundAlpha(blackAlpha);
            yield return null;
        }

        SetBlackBackgroundAlpha(1f);
        yield return PlayStorySequence();
        GameTimeManager.RequestNewGame(); // Main에서 Stage로 갈 때 이전 결과를 지우고 반드시 아침부터 시작합니다.
        StageOpeningSequence.RequestOpening();
        SceneManager.LoadScene(nextSceneName);
    }

    /// <summary>
    /// 스토리를 표시한 뒤 Press Space를 반복해서 점멸시키며 입력을 기다립니다.
    /// </summary>
    private IEnumerator PlayStorySequence()
    {
        SetTextAlpha(storyText, storyOriginalColor, 0f);
        SetTextAlpha(pressSpaceText, pressSpaceOriginalColor, 0f);

        float elapsedTime = 0f; // 스토리 페이드 인이 진행된 시간입니다.
        float fadeDuration = storyFadeInDuration > 0f ? storyFadeInDuration : 1f; // 0초 설정으로 페이드가 생략되는 것을 막은 실제 시간입니다.

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float storyAlpha = Mathf.Clamp01(elapsedTime / fadeDuration); // 현재 스토리 텍스트 투명도입니다.
            SetTextAlpha(storyText, storyOriginalColor, storyAlpha);
            yield return null;
        }

        SetTextAlpha(storyText, storyOriginalColor, 1f);
        yield return new WaitForSeconds(PressSpaceDelay);

        SetTextAlpha(pressSpaceText, pressSpaceOriginalColor, 0f);
        float pulseTime = 0f; // Press Space 펄스가 진행된 누적 시간입니다.
        float pulseDuration = Mathf.Max(0.1f, pressSpacePulseDuration); // 0초 설정으로 펄스 계산이 깨지는 것을 막은 실제 시간입니다.

        // Press Space 오브젝트가 활성화된 이후부터 Space 입력을 허용합니다.
        while (!Input.GetKeyDown(KeyCode.Space))
        {
            pulseTime += Time.deltaTime;
            float pulseAlpha = Mathf.PingPong(pulseTime * 2f / pulseDuration, 1f); // 0과 1을 반복하는 현재 펄스 투명도입니다.
            SetTextAlpha(pressSpaceText, pressSpaceOriginalColor, pulseAlpha);
            yield return null;
        }

        yield return FadeStoryTextsOut();
    }

    /// <summary>
    /// Space 입력 시 스토리와 Press Space를 현재 투명도에서 함께 페이드 아웃합니다.
    /// </summary>
    private IEnumerator FadeStoryTextsOut()
    {
        float elapsedTime = 0f; // 텍스트 페이드 아웃이 진행된 시간입니다.
        float fadeDuration = textFadeOutDuration > 0f ? textFadeOutDuration : 1f; // 0초 설정으로 페이드가 생략되는 것을 막은 실제 시간입니다.
        float storyStartAlpha = storyText.color.a; // 페이드 아웃 시작 시 스토리의 투명도입니다.
        float pressSpaceStartAlpha = pressSpaceText.color.a; // 페이드 아웃 시작 시 Press Space의 투명도입니다.

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / fadeDuration); // 현재 페이드 아웃 진행 비율입니다.
            SetTextAlpha(storyText, storyOriginalColor, Mathf.Lerp(storyStartAlpha, 0f, progress));
            SetTextAlpha(pressSpaceText, pressSpaceOriginalColor, Mathf.Lerp(pressSpaceStartAlpha, 0f, progress));
            yield return null;
        }

        SetTextAlpha(storyText, storyOriginalColor, 0f);
        SetTextAlpha(pressSpaceText, pressSpaceOriginalColor, 0f);
    }

    /// <summary>
    /// 마우스가 Start 이미지의 화면 영역 안에 있는지 확인합니다.
    /// </summary>
    private bool IsMouseOverStart()
    {
        Bounds startBounds = startRenderer.bounds; // Start 이미지의 월드 영역입니다.
        Vector3 firstCorner = sceneCamera.WorldToScreenPoint(startBounds.min); // Start 영역 한쪽 끝의 화면 좌표입니다.
        Vector3 secondCorner = sceneCamera.WorldToScreenPoint(startBounds.max); // Start 영역 반대쪽 끝의 화면 좌표입니다.
        float minimumX = Mathf.Min(firstCorner.x, secondCorner.x); // 클릭 영역의 왼쪽 끝입니다.
        float maximumX = Mathf.Max(firstCorner.x, secondCorner.x); // 클릭 영역의 오른쪽 끝입니다.
        float minimumY = Mathf.Min(firstCorner.y, secondCorner.y); // 클릭 영역의 아래쪽 끝입니다.
        float maximumY = Mathf.Max(firstCorner.y, secondCorner.y); // 클릭 영역의 위쪽 끝입니다.
        Rect startScreenArea = Rect.MinMaxRect(minimumX, minimumY, maximumX, maximumY); // Start 클릭 판정 영역입니다.

        return startScreenArea.Contains(Input.mousePosition);
    }

    /// <summary>
    /// 검은 배경의 RGB 색상을 유지하면서 투명도만 변경합니다.
    /// </summary>
    private void SetBlackBackgroundAlpha(float alpha)
    {
        Color changedColor = blackOriginalColor; // 검은 배경에 적용할 색상입니다.
        changedColor.a = alpha;
        blackBackground.color = changedColor;
    }

    /// <summary>
    /// Legacy Text의 RGB 색상을 유지하면서 투명도만 변경합니다.
    /// </summary>
    private void SetTextAlpha(Text targetText, Color originalColor, float alpha)
    {
        Color changedColor = originalColor; // 대상 텍스트에 적용할 색상입니다.
        changedColor.a = alpha;
        targetText.color = changedColor;
    }

    /// <summary>
    /// 퇴장 연출에 필요한 항목이 모두 설정되었는지 확인합니다.
    /// </summary>
    private bool AreSettingsValid()
    {
        bool hasScreenObjects = startRenderer != null && blackBackground != null && sceneCamera != null; // 이미지와 카메라 연결 여부입니다.
        bool hasStoryTexts = storyText != null && pressSpaceText != null; // 스토리와 입력 안내 Legacy Text의 연결 여부입니다.
        bool hasSeparateStoryTexts = hasStoryTexts && storyText.gameObject != pressSpaceText.gameObject; // 두 Text가 서로 다른 오브젝트인지 나타냅니다.
        bool hasAnimators = leftRopeAnimator != null && rightRopeAnimator != null && startAnimator != null; // 애니메이터 연결 여부입니다.
        bool hasAnimationNames = !string.IsNullOrWhiteSpace(leftRopeOutAnimationName) && !string.IsNullOrWhiteSpace(rightRopeOutAnimationName) && !string.IsNullOrWhiteSpace(startOutAnimationName); // 세 퇴장 애니메이션 이름의 입력 여부입니다.
        bool hasNextScene = !string.IsNullOrWhiteSpace(nextSceneName); // 다음 씬 이름의 입력 여부입니다.

        if (!hasScreenObjects || !hasStoryTexts || !hasSeparateStoryTexts || !hasAnimators || !hasAnimationNames || !hasNextScene)
        {
            Debug.LogError("StartSceneTransition의 설정을 확인해 주세요. Story Text와 Press Space Text는 서로 다른 오브젝트여야 합니다.", this);
            return false;
        }

        return true;
    }
}
