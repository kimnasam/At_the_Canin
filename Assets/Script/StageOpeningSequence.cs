using System.Collections;
using UnityEngine;

/// <summary>
/// Stage 씬의 검은 화면 페이드와 플레이어 등장 연출을 관리하는 클래스입니다.
/// </summary>
public class StageOpeningSequence : MonoBehaviour
{
    private static bool openingRequested; // Start 씬에서 Stage 오프닝을 요청했는지 씬 전환 중에 보관하는 값입니다.

    [Header("검은 화면")]
    [SerializeField] private SpriteRenderer blackFadeRenderer; // Stage 진입 직후 화면을 덮었다가 투명해질 검은 이미지입니다.
    [SerializeField, Min(0.01f)] private float blackFadeOutDuration = 1f; // 검은 화면이 완전히 사라지는 데 걸리는 시간입니다.

    [Header("플레이어 등장")]
    [SerializeField] private Transform playerTransform; // 오프닝 동안 아래에서 위로 이동시킬 Player 오브젝트입니다.
    [SerializeField] private PlayerGridMovement playerMovement; // 오프닝이 끝날 때까지 일반 이동 입력을 막을 이동 스크립트입니다.
    [SerializeField] private float openingStartY = -2.5f; // 오프닝이 요청되었을 때 Player가 출발할 Y 좌표입니다.
    [SerializeField] private float openingEndY = 0.5f; // 자동 등장 이동을 끝내고 조작을 허용할 Y 좌표입니다.
    [SerializeField] private float normalStartY = 4.5f; // 오프닝 신호 없이 Stage에 들어왔을 때 Player가 시작할 Y 좌표입니다.
    [SerializeField, Min(0.01f)] private float openingMoveSpeed = 2f; // 오프닝에서 Player가 자동으로 위로 이동하는 초당 거리입니다.

    private Color blackOriginalColor; // 검은 이미지의 원래 RGB 색상과 최대 투명도를 보관하는 값입니다.
    private bool shouldPlayOpening; // 이번 Stage 진입에서 플레이어 등장 연출을 실행할지 나타내는 값입니다.
    private Collider2D[] playerColliders; // 오프닝 동안 벽 충돌을 무시하도록 잠시 끌 Player의 모든 2D Collider입니다.
    private bool[] playerColliderStates; // 오프닝이 끝난 뒤 각 Collider의 기존 활성 상태를 복원하기 위한 값입니다.

    /// <summary>
    /// Start 씬이 다음 Stage 진입 시 플레이어 오프닝을 한 번 실행하도록 요청합니다.
    /// </summary>
    public static void RequestOpening()
    {
        openingRequested = true;
    }

    /// <summary>
    /// 검은 화면을 즉시 표시하고 오프닝 중 플레이어 조작을 잠급니다.
    /// </summary>
    private void Awake()
    {
        if (!AreSettingsValid())
        {
            enabled = false;
            return;
        }

        shouldPlayOpening = openingRequested;
        openingRequested = false;
        blackOriginalColor = blackFadeRenderer.color;
        blackFadeRenderer.gameObject.SetActive(true);
        SetBlackAlpha(1f);
        playerMovement.SetPlayerInputEnabled(false);

        if (shouldPlayOpening)
        {
            DisablePlayerCollisions();
            Vector3 startPosition = playerTransform.position; // 기존 X와 Z 좌표를 유지할 오프닝 시작 좌표입니다.
            startPosition.y = openingStartY;
            playerTransform.position = startPosition;
        }
        else
        {
            Vector3 normalPosition = playerTransform.position; // 기존 X와 Z 좌표를 유지할 일반 입장 좌표입니다.
            normalPosition.y = normalStartY;
            playerTransform.position = normalPosition;
        }
    }

    /// <summary>
    /// 검은 화면을 걷은 뒤 요청 여부에 따라 플레이어 등장 연출을 실행합니다.
    /// </summary>
    private IEnumerator Start()
    {
        yield return FadeBlackOut();

        if (shouldPlayOpening)
        {
            yield return MovePlayerIntoStage();
            RestorePlayerCollisions();
        }

        playerMovement.SetPlayerInputEnabled(true);
    }

    /// <summary>
    /// 검은 이미지의 투명도를 1에서 0으로 낮춰 Stage 화면을 드러냅니다.
    /// </summary>
    private IEnumerator FadeBlackOut()
    {
        float elapsedTime = 0f; // 검은 화면 페이드가 진행된 시간입니다.

        while (elapsedTime < blackFadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / blackFadeOutDuration); // 페이드 아웃의 현재 0~1 진행 비율입니다.
            SetBlackAlpha(1f - progress);
            yield return null;
        }

        SetBlackAlpha(0f);
        blackFadeRenderer.gameObject.SetActive(false);
    }

    /// <summary>
    /// 위쪽 걷기 애니메이션을 재생하며 Player를 목표 Y 좌표까지 자동으로 올립니다.
    /// </summary>
    private IEnumerator MovePlayerIntoStage()
    {
        playerMovement.PlayOpeningWalkUp();

        while (playerTransform.position.y < openingEndY)
        {
            Vector3 currentPosition = playerTransform.position; // 이번 프레임 이동 전 Player 좌표입니다.
            currentPosition.y = Mathf.MoveTowards(currentPosition.y, openingEndY, openingMoveSpeed * Time.deltaTime);
            playerTransform.position = currentPosition;
            yield return null;
        }

        Vector3 endPosition = playerTransform.position; // 부동소수점 오차 없이 끝 좌표에 맞출 Player 좌표입니다.
        endPosition.y = openingEndY;
        playerTransform.position = endPosition;
        playerMovement.PlayOpeningIdleUp();
    }

    /// <summary>
    /// Player와 자식 오브젝트의 Collider를 저장하고 꺼서 오프닝 이동 중 모든 벽을 통과하게 합니다.
    /// </summary>
    private void DisablePlayerCollisions()
    {
        playerColliders = playerTransform.GetComponentsInChildren<Collider2D>(true);
        playerColliderStates = new bool[playerColliders.Length];

        for (int index = 0; index < playerColliders.Length; index++)
        {
            playerColliderStates[index] = playerColliders[index].enabled;
            playerColliders[index].enabled = false;
        }
    }

    /// <summary>
    /// 오프닝 이동 전에 저장한 각 Player Collider의 활성 상태를 그대로 복원합니다.
    /// </summary>
    private void RestorePlayerCollisions()
    {
        if (playerColliders == null || playerColliderStates == null)
        {
            return;
        }

        for (int index = 0; index < playerColliders.Length; index++)
        {
            if (playerColliders[index] != null)
            {
                playerColliders[index].enabled = playerColliderStates[index];
            }
        }
    }

    /// <summary>
    /// 검은 이미지의 RGB는 유지하고 투명도만 변경합니다.
    /// </summary>
    private void SetBlackAlpha(float alpha)
    {
        Color changedColor = blackOriginalColor; // 검은 이미지에 실제로 적용할 색상입니다.
        changedColor.a = alpha;
        blackFadeRenderer.color = changedColor;
    }

    /// <summary>
    /// Stage 오프닝 실행에 필요한 Inspector 연결을 검사합니다.
    /// </summary>
    private bool AreSettingsValid()
    {
        bool hasRequiredObjects = blackFadeRenderer != null && playerTransform != null && playerMovement != null; // 필수 오브젝트 세 개가 모두 연결되었는지 나타냅니다.

        if (!hasRequiredObjects)
        {
            Debug.LogError("StageOpeningSequence의 Black Fade Renderer, Player Transform, Player Movement를 모두 연결해 주세요.", this);
            return false;
        }

        return true;
    }
}
