using System;
using UnityEngine;

/// <summary>
/// 씬이 바뀌어도 현재 시간과 카운터 결과를 유지하는 전역 게임 상태입니다.
/// </summary>
public class GameTimeManager : MonoBehaviour
{
    private static bool newGameRequested; // GameTimeManager가 만들어지기 전에 새 게임이 요청되었는지 보관합니다.

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

        if (newGameRequested)
        {
            ResetDialogueProgress();
            BeginMorning();
            newGameRequested = false;
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
    }

    private void SetTime(TimePeriod newTime)
    {
        currentTime = newTime;
        TimeChanged?.Invoke(currentTime);
    }
}
