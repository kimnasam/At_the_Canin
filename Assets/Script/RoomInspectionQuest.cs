using System.Collections;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 오프닝이 끝난 뒤 플레이어가 지정된 방을 방문했는지 확인하고 카운터 입장 목표를 안내합니다.
/// </summary>
public sealed class RoomInspectionQuest : MonoBehaviour
{
    [Header("퀘스트 연결")]
    [SerializeField] private PlayerGridMovement playerMovement; // 현재 방 이름과 방 변경 알림을 제공하는 플레이어 이동 코드입니다.
    [SerializeField] private TMP_Text questText; // 퀘스트 목표와 진행도를 표시할 TextMesh Pro 텍스트입니다.
    [SerializeField] private CanvasGroup questCanvasGroup; // 퀘스트 창 전체의 투명도를 조절하여 페이드 효과를 적용합니다.

    [Header("퀘스트 창 페이드")]
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.35f; // 퀘스트 창이 나타나거나 사라지는 데 걸리는 시간입니다.

    [Header("확인할 방")]
    [SerializeField] private string[] requiredRooms = { "거실", "복도", "2층", "침실", "주방" }; // 한 번씩 방문해야 하는 방 이름 목록입니다.

    [Header("표시 문구")]
    [SerializeField] private string inspectionQuestText = "모든 방 확인하기"; // 방 확인 중 진행도 앞에 표시할 목표 문구입니다.
    [SerializeField] private string counterQuestText = "카운터에서 일을 시작하기"; // 모든 방을 확인한 뒤 표시할 다음 목표 문구입니다.
    [SerializeField] private string nightQuestText = "창문을 수비하기"; // 저녁에 Stage로 돌아왔을 때 표시할 방어 목표 문구입니다.

    private readonly HashSet<string> requiredRoomNames = new HashSet<string>(); // 중복과 빈 항목을 제거한 실제 목표 방 이름입니다.
    private readonly HashSet<string> visitedRoomNames = new HashSet<string>(); // 이번 퀘스트에서 이미 방문한 목표 방 이름입니다.
    private bool questStarted; // 오프닝과 아침 설명이 끝나 퀘스트가 시작되었는지 나타냅니다.
    private bool questCompleted; // 모든 목표 방을 한 번 이상 방문했는지 나타냅니다.
    private bool isRoomInspectionActive; // 아침의 방 확인 퀘스트일 때만 방 이동 진행도를 집계하는 값입니다.
    private Coroutine questFadeCoroutine; // 현재 실행 중인 퀘스트 창 페이드 작업입니다.

    public bool IsCompleted => questCompleted; // 카운터 출입구가 입장 가능 여부를 확인할 때 사용하는 값입니다.
    public event Action<bool> CompletionChanged; // 완료 상태가 바뀌었음을 카운터 출입구에 알리는 이벤트입니다.

    private void Awake()
    {
        BuildRequiredRoomNames();

        if (!AreSettingsValid())
        {
            enabled = false;
            return;
        }

        RestoreSavedProgress();

        questCanvasGroup.alpha = 0f;
        questText.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (playerMovement != null)
        {
            playerMovement.RoomChanged += HandleRoomChanged;
        }
    }

    private void OnDisable()
    {
        if (playerMovement != null)
        {
            playerMovement.RoomChanged -= HandleRoomChanged;
        }
    }

    /// <summary>오프닝과 설명 대화가 끝난 시점에 방문 수를 0으로 두고 방 확인 퀘스트를 표시합니다.</summary>
    public void BeginQuest()
    {
        if (!enabled || questStarted)
        {
            return;
        }

        questStarted = true;
        isRoomInspectionActive = true;
        questText.gameObject.SetActive(true);
        UpdateQuestText();
        StartQuestFade(FadeCanvasGroup(0f, 1f));
    }

    /// <summary>저녁 설명 대화가 끝난 시점에 창문 방어 목표를 페이드 인하여 표시합니다.</summary>
    public void BeginNightQuest()
    {
        if (!enabled || questStarted)
        {
            return;
        }

        questStarted = true;
        isRoomInspectionActive = false;
        questText.gameObject.SetActive(true);
        questText.text = nightQuestText;
        StartQuestFade(FadeCanvasGroup(0f, 1f));
    }

    /// <summary>플레이어의 방이 바뀌면 해당 방이 목표 목록에 있는지 확인합니다.</summary>
    private void HandleRoomChanged(string roomName)
    {
        if (!questStarted || !isRoomInspectionActive || questCompleted)
        {
            return;
        }

        bool wasCompleted = questCompleted; // 이번 방 방문으로 퀘스트가 완료되는지 구분하기 위한 기존 상태입니다.
        RegisterRoom(roomName);

        if (!wasCompleted && questCompleted)
        {
            StartQuestFade(ChangeToCounterQuest());
        }
        else
        {
            UpdateQuestText();
        }
    }

    /// <summary>목표 방을 처음 방문한 경우에만 진행도에 추가하고 전체 완료 여부를 갱신합니다.</summary>
    private void RegisterRoom(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
        {
            return;
        }

        string normalizedRoomName = roomName.Trim(); // Player와 Inspector의 방 이름을 정확히 비교하기 위해 공백을 제거한 이름입니다.
        if (!requiredRoomNames.Contains(normalizedRoomName) || !visitedRoomNames.Add(normalizedRoomName))
        {
            return;
        }

        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.RegisterInspectedRoom(normalizedRoomName);
        }

        if (visitedRoomNames.Count >= requiredRoomNames.Count)
        {
            questCompleted = true;
            CompletionChanged?.Invoke(true);
        }
    }

    /// <summary>진행 중에는 방문 수를, 완료 뒤에는 카운터에서 시작할 다음 목표를 표시합니다.</summary>
    private void UpdateQuestText()
    {
        questText.text = questCompleted
            ? counterQuestText
            : $"{inspectionQuestText} ({visitedRoomNames.Count}/{requiredRoomNames.Count})";
    }

    /// <summary>진행 목표를 숨긴 뒤 카운터 목표로 문구를 교체하고 다시 표시합니다.</summary>
    private IEnumerator ChangeToCounterQuest()
    {
        yield return FadeCanvasGroup(questCanvasGroup.alpha, 0f);
        questText.text = counterQuestText;
        yield return FadeCanvasGroup(0f, 1f);
        questFadeCoroutine = null;
    }

    /// <summary>기존 페이드가 있다면 정리하고 새로운 퀘스트 창 페이드를 시작합니다.</summary>
    private void StartQuestFade(IEnumerator fadeRoutine)
    {
        if (questFadeCoroutine != null)
        {
            StopCoroutine(questFadeCoroutine);
        }

        questFadeCoroutine = StartCoroutine(fadeRoutine);
    }

    /// <summary>게임 일시 정지와 관계없이 CanvasGroup의 투명도를 부드럽게 변경합니다.</summary>
    private IEnumerator FadeCanvasGroup(float startAlpha, float targetAlpha)
    {
        float elapsedTime = 0f; // 현재 페이드에 사용된 실제 누적 시간입니다.

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float fadeRatio = Mathf.Clamp01(elapsedTime / fadeDuration); // 페이드 시작부터 끝까지의 0~1 진행 비율입니다.
            questCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, fadeRatio);
            yield return null;
        }

        questCanvasGroup.alpha = targetAlpha;
    }

    /// <summary>Inspector에 입력한 방 이름을 중복 없이 실제 목표 목록으로 만듭니다.</summary>
    private void BuildRequiredRoomNames()
    {
        requiredRoomNames.Clear();

        foreach (string roomName in requiredRooms)
        {
            if (!string.IsNullOrWhiteSpace(roomName))
            {
                requiredRoomNames.Add(roomName.Trim());
            }
        }
    }

    /// <summary>전역 게임 상태에 저장된 방 방문 기록을 현재 씬의 미션 진행도에 복원합니다.</summary>
    private void RestoreSavedProgress()
    {
        if (GameTimeManager.Instance == null)
        {
            return;
        }

        foreach (string roomName in requiredRoomNames)
        {
            if (GameTimeManager.Instance.IsRoomInspected(roomName))
            {
                visitedRoomNames.Add(roomName);
            }
        }

        questCompleted = requiredRoomNames.Count > 0 && visitedRoomNames.Count >= requiredRoomNames.Count;
    }

    /// <summary>퀘스트 실행에 필요한 플레이어, TMP 텍스트, 목표 방 목록이 설정되었는지 검사합니다.</summary>
    private bool AreSettingsValid()
    {
        bool hasFiveUniqueRooms = requiredRoomNames.Count == 5; // 빈 이름과 중복을 제외한 목표 방이 정확히 5개인지 나타냅니다.
        bool isValid = playerMovement != null && questText != null && questCanvasGroup != null && hasFiveUniqueRooms; // 필수 연결이 모두 준비되었는지 나타냅니다.

        if (!isValid)
        {
            Debug.LogError("RoomInspectionQuest의 연결 항목과 서로 다른 Required Rooms 5개를 모두 설정해 주세요.", this);
        }

        return isValid;
    }
}
