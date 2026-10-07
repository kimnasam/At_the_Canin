using UnityEngine;

/// <summary>
/// Main 씬의 반복 배경음과 등장 효과음을 관리하는 클래스입니다.
/// </summary>
public class MainSceneAudio : MonoBehaviour
{
    [Header("오디오 소스")]
    [SerializeField] private AudioSource effectAudioSource; // 로프와 Start 효과음용 소스입니다.
    [SerializeField] private AudioSource musicAudioSource; // 반복 배경 음악용 소스입니다.
    [SerializeField] private AudioSource rainAudioSource; // 반복 빗소리용 소스입니다.

    [Header("오디오 클립")]
    [SerializeField] private AudioClip ropeDropSound; // 로프 등장 효과음입니다.
    [SerializeField] private AudioClip startDropSound; // Start 등장 효과음입니다.
    [SerializeField] private AudioClip backgroundMusic; // 반복 재생할 배경 음악입니다.
    [SerializeField] private AudioClip rainLoopSound; // 반복 재생할 빗소리입니다.

    private bool settingsAreValid; // 모든 오디오가 연결되었는지 나타냅니다.

    /// <summary>
    /// 오디오 소스를 코드 재생에 맞게 초기화합니다.
    /// </summary>
    private void Awake()
    {
        settingsAreValid = AreSettingsValid();
        if (!settingsAreValid)
        {
            enabled = false;
            return;
        }

        effectAudioSource.playOnAwake = false;
        effectAudioSource.loop = false;
        effectAudioSource.Stop();
        musicAudioSource.playOnAwake = false;
        musicAudioSource.Stop();
        rainAudioSource.playOnAwake = false;
        rainAudioSource.Stop();
    }

    /// <summary>
    /// 씬 시작과 함께 음악과 빗소리를 반복 재생합니다.
    /// </summary>
    private void Start()
    {
        if (!settingsAreValid)
        {
            return;
        }

        musicAudioSource.clip = backgroundMusic;
        musicAudioSource.loop = true;
        musicAudioSource.Play();
        rainAudioSource.clip = rainLoopSound;
        rainAudioSource.loop = true;
        rainAudioSource.Play();
    }

    /// <summary>
    /// 로프 등장 효과음을 한 번 재생합니다.
    /// </summary>
    public void PlayRopeDropSound()
    {
        if (settingsAreValid)
        {
            effectAudioSource.PlayOneShot(ropeDropSound);
        }
    }

    /// <summary>
    /// Start 등장 효과음을 한 번 재생합니다.
    /// </summary>
    public void PlayStartDropSound()
    {
        if (settingsAreValid)
        {
            effectAudioSource.PlayOneShot(startDropSound);
        }
    }

    /// <summary>
    /// 필요한 오디오 소스와 클립이 모두 연결되었는지 확인합니다.
    /// </summary>
    private bool AreSettingsValid()
    {
        bool hasSources = effectAudioSource != null && musicAudioSource != null && rainAudioSource != null; // 오디오 소스 연결 여부입니다.
        bool hasClips = ropeDropSound != null && startDropSound != null && backgroundMusic != null && rainLoopSound != null; // 오디오 클립 연결 여부입니다.

        if (!hasSources || !hasClips)
        {
            Debug.LogError("MainSceneAudio의 오디오 소스와 클립을 모두 연결해 주세요.", this);
            return false;
        }

        return true;
    }
}
