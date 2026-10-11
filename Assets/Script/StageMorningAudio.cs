using System.Collections;
using UnityEngine;

/// <summary>
/// Stage 씬의 시간이 아침일 때만 지정된 배경 음악을 반복 재생하는 클래스입니다.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public sealed class StageMorningAudio : MonoBehaviour
{
    [Header("스테이지 아침 배경 음악")]
    [SerializeField] private AudioClip morningBackgroundMusic; // Stage 씬이 아침일 때 반복 재생할 배경 음악입니다.
    [SerializeField, Range(0f, 1f)] private float musicVolume = 1f; // 아침 배경 음악의 재생 음량입니다.
    [SerializeField, Min(0.01f)] private float slowFadeInDuration = 6f; // 아침 음악이 시작된 후 아주 천천히 설정 음량까지 커지는 시간입니다.

    private AudioSource musicAudioSource; // 아침 배경 음악을 실제로 출력하는 오디오 소스입니다.
    private Coroutine fadeInCoroutine; // 현재 실행 중인 아침 음악 페이드 인 작업입니다.

    /// <summary>
    /// 같은 오브젝트의 AudioSource를 2D 반복 배경 음악용으로 설정합니다.
    /// </summary>
    private void Awake()
    {
        musicAudioSource = GetComponent<AudioSource>();
        musicAudioSource.playOnAwake = false;
        musicAudioSource.loop = true;
        musicAudioSource.spatialBlend = 0f;
        musicAudioSource.volume = musicVolume;
    }

    /// <summary>
    /// 시간 변경 신호를 구독하고 현재 시간이 아침이면 음악을 바로 시작합니다.
    /// </summary>
    private void Start()
    {
        if (morningBackgroundMusic == null)
        {
            Debug.LogError("StageMorningAudio에 Morning Background Music을 연결해 주세요.", this);
            enabled = false;
            return;
        }

        if (GameTimeManager.Instance == null)
        {
            Debug.LogError("StageMorningAudio를 사용하려면 씬에 GameTimeManager가 필요합니다.", this);
            enabled = false;
            return;
        }

        musicAudioSource.clip = morningBackgroundMusic;
        GameTimeManager.Instance.TimeChanged += HandleTimeChanged;
        HandleTimeChanged(GameTimeManager.Instance.CurrentTime);
    }

    /// <summary>
    /// 오브젝트가 사라질 때 시간 변경 신호 연결을 해제하고 음악을 정지합니다.
    /// </summary>
    private void OnDestroy()
    {
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.TimeChanged -= HandleTimeChanged;
        }

        StopMusic();
    }

    /// <summary>
    /// 아침에는 음악을 반복 재생하고 밤에는 음악을 정지합니다.
    /// </summary>
    private void HandleTimeChanged(GameTimeManager.TimePeriod time)
    {
        bool isMorning = time == GameTimeManager.TimePeriod.Morning; // 변경된 시간이 아침인지 나타냅니다.

        if (isMorning && !musicAudioSource.isPlaying)
        {
            musicAudioSource.volume = 0f;
            musicAudioSource.Play();
            fadeInCoroutine = StartCoroutine(FadeInMusic());
        }
        else if (!isMorning)
        {
            StopMusic();
        }
    }

    /// <summary>
    /// 실행 중인 페이드 인을 정리하고 아침 음악을 정지합니다.
    /// </summary>
    private void StopMusic()
    {
        if (fadeInCoroutine != null)
        {
            StopCoroutine(fadeInCoroutine);
            fadeInCoroutine = null;
        }

        if (musicAudioSource != null && musicAudioSource.isPlaying)
        {
            musicAudioSource.Stop();
        }
    }

    /// <summary>
    /// 아침 배경 음악을 무음에서 설정한 음량까지 서서히 높입니다.
    /// </summary>
    private IEnumerator FadeInMusic()
    {
        float elapsedTime = 0f; // 현재까지 진행된 페이드 인 시간입니다.

        while (elapsedTime < slowFadeInDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float fadeRatio = Mathf.Clamp01(elapsedTime / slowFadeInDuration); // 현재 페이드 인의 0~1 진행 비율입니다.
            musicAudioSource.volume = Mathf.Lerp(0f, musicVolume, fadeRatio);
            yield return null;
        }

        musicAudioSource.volume = musicVolume;
        fadeInCoroutine = null;
    }

    /// <summary>
    /// 실행 중 Inspector에서 음량을 바꾸면 AudioSource에도 즉시 반영합니다.
    /// </summary>
    private void OnValidate()
    {
        AudioSource source = GetComponent<AudioSource>(); // Inspector 변경값을 반영할 현재 오디오 소스입니다.
        if (source != null)
        {
            source.volume = musicVolume;
        }
    }
}
