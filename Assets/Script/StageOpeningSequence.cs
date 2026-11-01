using System.Collections;
using System;
using UnityEngine;

/// <summary>
/// Stage 씬의 검은 화면 페이드와 플레이어 등장 연출을 관리하는 클래스입니다.
/// </summary>
public class StageOpeningSequence : MonoBehaviour
{
    [Serializable]
    private sealed class SceneEntryPoint // 출발 씬과 그 씬에서 돌아왔을 때 사용할 Stage 시작 지점을 연결하는 설정입니다.
    {
        [SerializeField] private string sourceSceneName; // Stage로 이동하기 직전에 있던 씬 이름입니다.
        [SerializeField] private Transform entryPoint; // 해당 씬에서 들어왔을 때 플레이어를 배치할 위치입니다.

        public string SourceSceneName => sourceSceneName; // 시작 지점 검색에 사용할 출발 씬 이름입니다.
        public Transform EntryPoint => entryPoint; // 플레이어에게 적용할 위치와 회전값을 가진 Transform입니다.
    }

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

    [Header("이전 씬별 시작 위치")]
    [SerializeField] private SceneEntryPoint[] sceneEntryPoints = Array.Empty<SceneEntryPoint>(); // 출발 씬 이름에 따라 선택할 Stage 내부 시작 지점 목록입니다.

    [Header("상황 설명 대화")]
    [SerializeField] private JsonTutorialDialogue morningDialogue; // 새 게임의 첫 아침에 표시할 조작 및 맵 설명입니다.
    [SerializeField] private JsonTutorialDialogue nightDialogue; // 새 게임의 첫 밤에 표시할 대처 방법 설명입니다.

    [Header("방 확인 퀘스트")]
    [SerializeField] private RoomInspectionQuest roomInspectionQuest; // 오프닝과 아침 설명이 끝난 뒤 시작할 방 확인 퀘스트입니다.

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
    /// 게임 초기화 시 아직 실행되지 않은 Stage 오프닝 요청을 제거합니다.
    /// </summary>
    public static void ResetOpeningRequest()
    {
        openingRequested = false;
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
    }

    /// <summary>
    /// 검은 화면을 걷은 뒤 요청 여부에 따라 플레이어 등장 연출을 실행합니다.
    /// </summary>
    private IEnumerator Start()
    {
        if (!shouldPlayOpening)
        {
            PlacePlayerAtSceneEntryPoint();
        }

        JsonTutorialDialogue currentDialogue = GetCurrentDialogue(); // 현재 시간에 맞춰 재생할 아침 또는 밤 설명입니다.
        if (currentDialogue != null)
        {
            currentDialogue.ReserveIfNeeded();
        }

        yield return FadeBlackOut();

        if (shouldPlayOpening)
        {
            yield return MovePlayerIntoStage();
            RestorePlayerCollisions();
        }

        if (currentDialogue != null)
        {
            yield return currentDialogue.PlayIfNeeded();
        }

        if (GameTimeManager.Instance != null && roomInspectionQuest != null)
        {
            if (GameTimeManager.Instance.CurrentTime == GameTimeManager.TimePeriod.Morning)
            {
                roomInspectionQuest.BeginQuest();
            }
            else
            {
                roomInspectionQuest.BeginNightQuest();
            }
        }

        playerMovement.SetPlayerInputEnabled(true);
    }

    /// <summary>직전 씬과 일치하는 시작 지점을 찾아 플레이어 위치를 적용하고, 없으면 기존 기본 위치를 사용합니다.</summary>
    private void PlacePlayerAtSceneEntryPoint()
    {
        string previousSceneName = GameTimeManager.ConsumePreviousSceneName(); // 씬 전환 직전에 별도로 보관한 직전 씬 이름입니다.

        foreach (SceneEntryPoint sceneEntryPoint in sceneEntryPoints)
        {
            if (sceneEntryPoint == null || sceneEntryPoint.EntryPoint == null)
            {
                continue;
            }

            if (string.Equals(sceneEntryPoint.SourceSceneName?.Trim(), previousSceneName, StringComparison.Ordinal))
            {
                playerTransform.SetPositionAndRotation(sceneEntryPoint.EntryPoint.position, sceneEntryPoint.EntryPoint.rotation);
                Debug.Log($"'{previousSceneName}' 씬 시작 위치 '{sceneEntryPoint.EntryPoint.name}'를 적용했습니다.", this);
                return;
            }
        }

        Vector3 normalPosition = playerTransform.position; // 일치하는 설정이 없을 때 기존 X와 Z를 유지할 기본 좌표입니다.
        normalPosition.y = normalStartY;
        playerTransform.position = normalPosition;
        Debug.LogWarning($"'{previousSceneName}' 씬에 해당하는 시작 위치가 없어 기본 위치를 적용했습니다.", this);
    }

    /// <summary>
    /// 현재 게임 시간이 아침이면 아침 대화, 밤이면 첫 밤 대화를 반환합니다.
    /// </summary>
    private JsonTutorialDialogue GetCurrentDialogue()
    {
        if (GameTimeManager.Instance == null)
        {
            return null;
        }

        return GameTimeManager.Instance.CurrentTime == GameTimeManager.TimePeriod.Morning
            ? morningDialogue
            : nightDialogue;
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
