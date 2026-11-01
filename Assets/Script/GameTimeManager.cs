using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬이 바뀌어도 현재 시간과 카운터 결과를 유지하는 전역 게임 상태입니다.
/// </summary>
public class GameTimeManager : MonoBehaviour
{
    private static bool newGameRequested; // GameTimeManager가 만들어지기 전에 새 게임이 요청되었는지 보관합니다.
    private static string pendingPreviousSceneName = string.Empty; // 관리자 인스턴스가 없는 씬에서도 다음 Stage 진입까지 보관할 출발 씬 이름입니다.
    private readonly HashSet<string> inspectedRoomNames = new HashSet<string>(); // 씬이 바뀌어도 유지할 방 확인 미션의 방문 완료 방 이름입니다.

    public enum TimePeriod // 스테이지에서 사용하는 시간 구분입니다.
    {
        Morning,
        Night
    }

    public static GameTimeManager Instance { get; private set; } // 현재 게임에서 유일한 시간 관리자입니다.

    public event Action<TimePeriod> TimeChanged; // 시간이 변경될 때 스테이지 오브젝트에 알리는 이벤트입니다.

    [SerializeField] private TimePeriod currentTime = TimePeriod.Morning; // 현재 게임 시간입니다.
    [SerializeField, Min(1)] private int maximumGuests = 6; // 하루에 받아야 하는 손님의 수입니다.

    public TimePeriod CurrentTime => currentTime; // 다른 코드가 읽을 수 있는 현재 시간입니다.
    public int CheckedInGuests { get; private set; } // 오늘 체크인시킨 손님의 수입니다.
    public bool WolfEntered { get; private set; } // 오늘 늑대를 한 마리라도 들였는지 나타냅니다.
    public int MaximumGuests => maximumGuests; // 하루 최대 정원입니다.
    public bool MorningDialoguePlayed { get; private set; } // 이번 새 게임에서 아침 설명을 이미 완료했는지 나타냅니다.
    public bool CounterDialoguePlayed { get; private set; } // 이번 새 게임에서 카운터 설명을 이미 완료했는지 나타냅니다.
    public bool NightDialoguePlayed { get; private set; } // 이번 새 게임에서 첫 밤 설명을 이미 완료했는지 나타냅니다.
    public string PreviousSceneName { get; private set; } = string.Empty; // 현재 씬으로 이동하기 직전에 플레이하던 씬 이름입니다.

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // DontDestroyOnLoad only preserves root GameObjects. The Stage hierarchy can
        // group manager objects under a container, so detach this manager first.
        if (transform.parent != null)
        {
            transform.SetParent(null, true);
        }

        DontDestroyOnLoad(gameObject);
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;

        if (newGameRequested)
        {
            ResetDialogueProgress();
            BeginMorning();
            newGameRequested = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            Instance = null;
        }
    }

    /// <summary>
    /// Main 씬에서 새 게임을 시작할 때 이전 진행 정보를 지우고 아침으로 되돌립니다.
    /// 첫 Stage 진입처럼 관리자가 아직 없으면 생성 직후 초기화하도록 요청을 보관합니다.
    /// </summary>
    public static void RequestNewGame()
    {
        if (Instance != null)
        {
            Instance.ResetDialogueProgress();
            Instance.BeginMorning();
            newGameRequested = false;
            return;
        }

        newGameRequested = true;
    }

    /// <summary>
    /// 새 아침을 시작하고 전날의 카운터 결과를 초기화합니다.
    /// </summary>
    public void BeginMorning()
    {
        CheckedInGuests = 0;
        WolfEntered = false;
        SetTime(TimePeriod.Morning);
    }

    /// <summary>
    /// 체크인 결과를 기록합니다.
    /// </summary>
    public void RegisterCheckIn(bool isWolf)
    {
        CheckedInGuests = Mathf.Min(CheckedInGuests + 1, maximumGuests);
        WolfEntered |= isWolf;
    }

    /// <summary>
    /// 카운터 업무가 끝난 뒤 시간을 밤으로 변경합니다.
    /// </summary>
    public void BeginNight()
    {
        SetTime(TimePeriod.Night);
    }

    /// <summary>방 확인 미션에서 처음 방문한 방을 씬 전환 후에도 유지되는 목록에 저장합니다.</summary>
    public void RegisterInspectedRoom(string roomName)
    {
        if (!string.IsNullOrWhiteSpace(roomName))
        {
            inspectedRoomNames.Add(roomName.Trim());
        }
    }

    /// <summary>지정한 방이 현재 새 게임에서 이미 확인된 방인지 반환합니다.</summary>
    public bool IsRoomInspected(string roomName)
    {
        return !string.IsNullOrWhiteSpace(roomName) && inspectedRoomNames.Contains(roomName.Trim());
    }

    /// <summary>씬을 불러오기 직전에 현재 씬 이름을 저장하여 다음 씬의 시작 위치 결정에 사용합니다.</summary>
    public static void RecordCurrentSceneAsPrevious()
    {
        Scene currentScene = SceneManager.GetActiveScene(); // 아직 전환되지 않은 현재 활성 씬입니다.
        pendingPreviousSceneName = currentScene.IsValid() ? currentScene.name : string.Empty;

        if (Instance != null)
        {
            Instance.PreviousSceneName = pendingPreviousSceneName;
        }
    }

    /// <summary>다음 Stage 진입용으로 기록한 출발 씬 이름을 반환하고 대기값을 비웁니다.</summary>
    public static string ConsumePreviousSceneName()
    {
        string sceneName = !string.IsNullOrWhiteSpace(pendingPreviousSceneName)
            ? pendingPreviousSceneName
            : Instance != null ? Instance.PreviousSceneName : string.Empty; // 직접 기록값을 우선하고 이벤트 기록은 예비값으로 사용합니다.

        pendingPreviousSceneName = string.Empty;
        return sceneName;
    }

    /// <summary>
    /// 완료된 상황의 설명 대화를 이번 새 게임에서 다시 실행하지 않도록 기록합니다.
    /// </summary>
    public void MarkDialoguePlayed(JsonTutorialDialogue.DialogueMoment dialogueMoment)
    {
        switch (dialogueMoment)
        {
            case JsonTutorialDialogue.DialogueMoment.Morning:
                MorningDialoguePlayed = true;
                break;
            case JsonTutorialDialogue.DialogueMoment.Counter:
                CounterDialoguePlayed = true;
                break;
            case JsonTutorialDialogue.DialogueMoment.Night:
                NightDialoguePlayed = true;
                break;
        }
    }

    /// <summary>
    /// Main에서 새 게임을 시작할 때 세 설명 대화의 완료 기록을 초기화합니다.
    /// </summary>
    private void ResetDialogueProgress()
    {
        MorningDialoguePlayed = false;
        CounterDialoguePlayed = false;
        NightDialoguePlayed = false;
        inspectedRoomNames.Clear();
        PreviousSceneName = string.Empty;
        pendingPreviousSceneName = string.Empty;
    }

    /// <summary>활성 씬이 바뀔 때 떠난 씬 이름을 다음 씬의 시작 위치 선택용으로 저장합니다.</summary>
    private void HandleActiveSceneChanged(Scene previousScene, Scene currentScene)
    {
        PreviousSceneName = previousScene.IsValid() ? previousScene.name : string.Empty;
    }

    private void SetTime(TimePeriod newTime)
    {
        currentTime = newTime;
        TimeChanged?.Invoke(currentTime);
    }
}
