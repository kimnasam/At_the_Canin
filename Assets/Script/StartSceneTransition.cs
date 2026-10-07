using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Start 클릭 후 퇴장 애니메이션과 검은 화면을 실행하고 다음 씬으로 이동하는 클래스입니다.
/// </summary>
public class StartSceneTransition : MonoBehaviour
{
    [Header("클릭과 화면 이미지")]
    [SerializeField] private SpriteRenderer startRenderer; // 클릭 영역으로 사용할 Start 이미지입니다.
    [SerializeField] private SpriteRenderer blackBackground; // 퇴장 중 페이드 인할 검은 배경입니다.
    [SerializeField] private Camera clickCamera; // Start의 화면상 클릭 영역을 계산할 카메라입니다.

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
    [SerializeField] private string nextSceneName; // 연출 뒤 불러올 씬 이름입니다.

    private Color blackOriginalColor; // 검은 배경의 원래 색상입니다.
    private Camera sceneCamera; // 클릭 판정에 실제로 사용할 카메라입니다.
    private bool canClickStart; // Start 클릭이 허용된 상태인지 나타냅니다.
    private bool isChangingScene; // 중복 씬 전환을 방지하는 값입니다.

    /// <summary>
    /// 카메라와 검은 배경 색상을 준비하고 설정을 검사합니다.
    /// </summary>
    private void Awake()
    {
        sceneCamera = clickCamera != null ? clickCamera : Camera.main;
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
        SceneManager.LoadScene(nextSceneName);
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
    /// 퇴장 연출에 필요한 항목이 모두 설정되었는지 확인합니다.
    /// </summary>
    private bool AreSettingsValid()
    {
        bool hasScreenObjects = startRenderer != null && blackBackground != null && sceneCamera != null; // 이미지와 카메라 연결 여부입니다.
        bool hasAnimators = leftRopeAnimator != null && rightRopeAnimator != null && startAnimator != null; // 애니메이터 연결 여부입니다.
        bool hasAnimationNames = !string.IsNullOrWhiteSpace(leftRopeOutAnimationName) && !string.IsNullOrWhiteSpace(rightRopeOutAnimationName) && !string.IsNullOrWhiteSpace(startOutAnimationName); // 세 퇴장 애니메이션 이름의 입력 여부입니다.
        bool hasNextScene = !string.IsNullOrWhiteSpace(nextSceneName); // 다음 씬 이름의 입력 여부입니다.

        if (!hasScreenObjects || !hasAnimators || !hasAnimationNames || !hasNextScene)
        {
            Debug.LogError("StartSceneTransition의 이미지, 카메라, 애니메이터 이름, 다음 씬 이름을 모두 설정해 주세요.", this);
            return false;
        }

        return true;
    }
}
