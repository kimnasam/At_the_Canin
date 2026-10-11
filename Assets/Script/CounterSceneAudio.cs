using System.Collections;
using UnityEngine;

/// <summary>
/// Counter 씬이 열려 있는 동안 배경 음악을 반복 재생하는 클래스입니다.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public sealed class CounterSceneAudio : MonoBehaviour
{
    [Header("카운터 배경 음악")]
    [SerializeField] private AudioClip backgroundMusic; // Counter 씬에서 반복 재생할 배경 음악입니다.
    [SerializeField, Range(0f, 1f)] private float musicVolume = 1f; // 배경 음악의 재생 음량입니다.
    [SerializeField, Min(0.01f)] private float slowFadeInDuration = 6f; // 장면 시작 후 음악이 아주 천천히 설정 음량까지 커지는 시간입니다.

    private AudioSource musicAudioSource; // 배경 음악을 실제로 출력하는 오디오 소스입니다.

    /// <summary>
    /// 같은 오브젝트의 AudioSource를 반복 배경 음악 재생용으로 설정합니다.
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
    /// Counter 씬 시작과 함께 지정된 음악을 처음부터 재생합니다.
    /// </summary>
    private void Start()
    {
        if (backgroundMusic == null)
        {
            Debug.LogError("CounterSceneAudio에 Background Music을 연결해 주세요.", this);
            enabled = false;
            return;
        }

        musicAudioSource.clip = backgroundMusic;
        musicAudioSource.volume = 0f;
        musicAudioSource.Play();
        StartCoroutine(FadeInMusic());
    }

    /// <summary>
    /// 배경 음악을 무음에서 설정한 음량까지 서서히 높입니다.
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
