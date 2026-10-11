using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 체크인하지 않은 늑대가 밤 동안 창문으로 침입하는 이벤트의 시간, 공격 순서, 종료를 관리합니다.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class NightWindowDefenseController : MonoBehaviour
{
    [Header("이벤트 시간")]
    [SerializeField, Min(1f)] private float defenseDuration = 120f; // 창문을 지켜야 하는 전체 시간(초)입니다.
    [SerializeField] private Text timerText; // 화면 오른쪽 위에 남은 시간을 표시할 레거시 UI Text입니다.

    [Header("창문 공격")]
    [SerializeField] private WindowWolfDefensePoint[] windows; // 늑대가 나타날 수 있는 모든 창문입니다.
    [SerializeField, Min(0f)] private float minimumAttackDelay = 1f; // 창문에 늑대가 없을 때 다음 늑대가 나타나기까지의 최소 시간입니다.
    [SerializeField, Min(0f)] private float maximumAttackDelay = 10f; // 창문에 늑대가 없을 때 다음 늑대가 나타나기까지의 최대 시간입니다.
    [SerializeField, Min(0.1f)] private float wolfWaitDuration = 5f; // 창문에 선 늑대가 실내로 들어오기까지 기다리는 시간입니다.
    [SerializeField] private GameObject wolfPrefab; // 방어에 실패했을 때 실내에 생성할 추격 늑대 Prefab입니다.

    [Header("효과음")]
    [SerializeField] private AudioClip glassBreakSound; // 늑대가 창문을 깨고 실내에 생성될 때 재생할 소리입니다.
    [SerializeField] private AudioClip flashlightSound; // 플레이어가 창문에 플래시를 비출 때 재생할 소리입니다.

    [Header("이벤트 종료")]
    [SerializeField] private SceneFadeController fadeController; // 제한 시간이 끝났을 때 검게 페이드할 컨트롤러입니다.
    [SerializeField] private string mainSceneName = "Main"; // 페이드아웃 후 돌아갈 메인 씬 이름입니다.

    private float remainingTime; // 현재 이벤트에 남은 시간입니다.
    private float nextAttackTime; // 다음 늑대 공격을 시작할 게임 시간입니다.
    private WindowWolfDefensePoint activeWindow; // 현재 늑대가 서 있는 창문입니다.
    private bool eventRunning; // 방어 이벤트가 실제로 진행 중인지 나타냅니다.
    private bool wolfHasEntered; // 방어 실패로 추격 늑대가 이미 생성되었는지 나타냅니다.
    private bool isEnding; // 종료 코루틴이 중복 실행되지 않도록 막는 값입니다.
    private AudioSource effectAudioSource; // 유리 파손음과 플래시 효과음을 재생할 오디오 소스입니다.

    public bool CanUseFlashlight => eventRunning && !isEnding && !wolfHasEntered; // 창문에서 손전등 상호작용을 표시해도 되는 상태입니다.

    private void Awake()
    {
        effectAudioSource = GetComponent<AudioSource>();
        if (effectAudioSource == null)
        {
            effectAudioSource = gameObject.AddComponent<AudioSource>(); // 기존 씬 오브젝트에 AudioSource가 없어도 효과음을 재생할 수 있게 추가합니다.
        }

        effectAudioSource.playOnAwake = false;
    }

    /// <summary>
    /// 밤이고 늑대를 체크인시키지 않은 경우에만 2분 방어 이벤트를 시작합니다.
    /// </summary>
    private void Start()
    {
        SetTimerVisible(false);

        if (GameTimeManager.Instance == null)
        {
            Debug.LogError("씬에 GameTimeManager가 필요합니다.", this);
            enabled = false;
            return;
        }

        bool isNight = GameTimeManager.Instance.CurrentTime == GameTimeManager.TimePeriod.Night; // 현재 시간이 밤인지 확인한 결과입니다.
        bool wolfWasCheckedIn = GameTimeManager.Instance.WolfEntered; // 카운터에서 늑대를 이미 받아들였는지 확인한 결과입니다.

        if (!isNight || wolfWasCheckedIn)
        {
            enabled = false;
            return;
        }

        if (!AreSettingsValid())
        {
            enabled = false;
            return;
        }

        ClearAllWindows();
        remainingTime = defenseDuration;
        ScheduleNextAttack();
        eventRunning = true;
        SetTimerVisible(true);
        UpdateTimerText();
    }

    /// <summary>
    /// 남은 시간을 갱신하고 늑대의 침입 제한 시간과 다음 공격 시점을 검사합니다.
    /// </summary>
    private void Update()
    {
        if (!eventRunning || isEnding)
        {
            return;
        }

        remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
        UpdateTimerText();

        if (remainingTime <= 0f)
        {
            StartCoroutine(FinishDefense());
            return;
        }

        if (wolfHasEntered)
        {
            return;
        }

        if (activeWindow == null && Time.time >= nextAttackTime)
        {
            BeginRandomAttack();
            return;
        }

        if (activeWindow != null && activeWindow.WolfVisibleDuration >= wolfWaitDuration)
        {
            LetWolfEnter(activeWindow);
        }
    }

    /// <summary>
    /// 플레이어가 창문 범위에서 스페이스바를 눌렀을 때 호출되어 손전등 결과를 처리합니다.
    /// </summary>
    public void ShineFlashlightAt(WindowWolfDefensePoint illuminatedWindow)
    {
        if (!eventRunning || isEnding || wolfHasEntered || illuminatedWindow == null)
        {
            return;
        }

        illuminatedWindow.PlayFlashlightEffect();
        PlayEffectSound(flashlightSound);

        if (activeWindow == null)
        {
            return;
        }

        if (illuminatedWindow == activeWindow)
        {
            activeWindow.SetWolfVisible(false);
            activeWindow = null;
            ScheduleNextAttack();
            return;
        }

        // 늑대가 없는 창문을 비추면 실제 늑대가 서 있던 다른 창문으로 즉시 침입합니다.
        LetWolfEnter(activeWindow);
    }

    /// <summary>
    /// 창문 중 하나를 무작위로 선택해 늑대가 서 있는 이미지로 변경합니다.
    /// </summary>
    private void BeginRandomAttack()
    {
        if (windows == null || windows.Length == 0)
        {
            return;
        }

        int startIndex = Random.Range(0, windows.Length); // null 창문을 건너뛰기 위한 무작위 시작 번호입니다.

        for (int offset = 0; offset < windows.Length; offset++)
        {
            WindowWolfDefensePoint candidate = windows[(startIndex + offset) % windows.Length]; // 이번에 공격 가능한지 검사할 창문입니다.
            if (candidate == null)
            {
                continue;
            }

            activeWindow = candidate;
            activeWindow.SetWolfVisible(true);
            return;
        }
    }

    /// <summary>
    /// 늑대가 창문에 없는 시간을 1~10초 범위에서 새로 추첨해 다음 공격 시각을 정합니다.
    /// </summary>
    private void ScheduleNextAttack()
    {
        float shortestDelay = Mathf.Min(minimumAttackDelay, maximumAttackDelay); // Inspector에서 순서를 반대로 입력해도 사용할 짧은 대기 시간입니다.
        float longestDelay = Mathf.Max(minimumAttackDelay, maximumAttackDelay); // Inspector에서 순서를 반대로 입력해도 사용할 긴 대기 시간입니다.
        float randomDelay = Random.Range(shortestDelay, longestDelay); // 이번 공격 전에 무작위로 기다릴 시간입니다.
        nextAttackTime = Time.time + randomDelay;
    }

    /// <summary>
    /// 지정한 창문 앞에 추격 늑대를 한 번 생성하고 모든 창문 표시를 원래대로 되돌립니다.
    /// </summary>
    private void LetWolfEnter(WindowWolfDefensePoint sourceWindow)
    {
        if (wolfHasEntered || sourceWindow == null)
        {
            return;
        }

        PlayEffectSound(glassBreakSound);
        Instantiate(wolfPrefab, sourceWindow.WolfSpawnPosition, Quaternion.identity);
        wolfHasEntered = true;
        ClearAllWindows();
        activeWindow = null;
    }

    /// <summary>
    /// 지정한 효과음이 연결되어 있으면 다른 효과음을 끊지 않고 한 번 재생합니다.
    /// </summary>
    private void PlayEffectSound(AudioClip soundClip)
    {
        if (effectAudioSource != null && soundClip != null)
        {
            effectAudioSource.PlayOneShot(soundClip);
        }
    }

    /// <summary>
    /// 남은 초를 분:초 형식으로 변환해 타이머 UI에 표시합니다.
    /// </summary>
    private void UpdateTimerText()
    {
        int displayedSeconds = Mathf.CeilToInt(remainingTime); // 소수점 시간이 남았을 때 0초로 일찍 보이지 않게 올림한 초입니다.
        int minutes = displayedSeconds / 60; // 타이머에 표시할 분입니다.
        int seconds = displayedSeconds % 60; // 타이머에 표시할 초입니다.
        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    /// <summary>
    /// 2분이 끝나면 입력과 창문 이벤트를 정리하고 Main 씬으로 페이드아웃합니다.
    /// </summary>
    private IEnumerator FinishDefense()
    {
        isEnding = true;
        eventRunning = false;
        ClearAllWindows();
        SetTimerVisible(false);
        yield return fadeController.FadeOutAndLoad(mainSceneName);
    }

    /// <summary>
    /// 모든 창문의 늑대 이미지, 상호작용 안내, 손전등 효과를 숨깁니다.
    /// </summary>
    private void ClearAllWindows()
    {
        if (windows == null)
        {
            return;
        }

        foreach (WindowWolfDefensePoint window in windows)
        {
            if (window != null)
            {
                window.SetWolfVisible(false);
                window.HideInteractionVisuals();
            }
        }
    }

    private void SetTimerVisible(bool visible)
    {
        if (timerText != null)
        {
            timerText.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// 이벤트 실행에 반드시 필요한 Inspector 연결을 검사합니다.
    /// </summary>
    private bool AreSettingsValid()
    {
        bool valid = timerText != null && windows != null && windows.Length > 0
            && wolfPrefab != null && fadeController != null; // 필수 참조가 모두 연결되었는지 확인한 결과입니다.

        if (!valid)
        {
            Debug.LogError("NightWindowDefenseController의 Timer Text, Windows, Wolf Prefab, Fade Controller를 모두 연결해 주세요.", this);
        }

        return valid;
    }
}
