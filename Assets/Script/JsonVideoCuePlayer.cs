using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// JSON에 적힌 영상 신호 이름을 VideoClip과 연결하고 영상 재생이 끝날 때까지 기다리는 범용 클래스입니다.
/// </summary>
[RequireComponent(typeof(VideoPlayer))]
public sealed class JsonVideoCuePlayer : MonoBehaviour
{
    [Serializable]
    private sealed class VideoCue
    {
        public string cueName; // JSON의 videoCue 값과 비교할 영상 신호 이름입니다.
        public VideoClip videoClip; // 신호가 일치할 때 재생할 MP4 영상입니다.
    }

    [Header("영상 출력")]
    [SerializeField] private GameObject videoScreen; // 영상이 재생되는 동안에만 표시할 RawImage 또는 화면 오브젝트입니다.
    [SerializeField] private VideoCue[] videoCues; // JSON 신호 이름과 실제 MP4 VideoClip의 연결 목록입니다.
    [SerializeField, Min(0f)] private float returnFadeDuration = 0.6f; // 영상 종료 후 기존 게임 화면이 서서히 나타나는 시간입니다.

    [Header("영상 건너뛰기")]
    [SerializeField] private bool allowSkipWithDoubleSpace = true; // true이면 스페이스바를 빠르게 두 번 눌러 영상을 건너뛸 수 있습니다.
    [SerializeField, Min(0.1f)] private float doubleSpaceInterval = 0.75f; // 두 스페이스 입력을 연속 입력으로 인정할 최대 시간 간격입니다.

    private VideoPlayer videoPlayer; // 연결된 VideoClip의 영상과 소리를 실제로 재생하는 Unity VideoPlayer입니다.
    private CanvasGroup videoScreenCanvasGroup; // 영상 화면의 투명도를 조절해 기존 화면을 페이드 인시키는 UI 그룹입니다.
    private bool prepareFailed; // 영상 준비 중 오류가 발생했는지 기록합니다.
    private bool previousAudioPauseState; // 영상 시작 전 게임 전체 소리의 일시 정지 상태입니다.
    private bool audioPauseStateSaved; // 복구할 게임 소리 상태를 저장했는지 나타냅니다.
    private int spacePressCount; // 현재 연속으로 입력된 스페이스바 횟수입니다.
    private float lastSpacePressTime; // 마지막 스페이스바 입력이 발생한 실제 시간입니다.

    private void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;

        if (videoScreen != null)
        {
            videoScreenCanvasGroup = videoScreen.GetComponent<CanvasGroup>();
            if (videoScreenCanvasGroup == null)
            {
                videoScreenCanvasGroup = videoScreen.AddComponent<CanvasGroup>();
            }

            videoScreenCanvasGroup.alpha = 1f;
            videoScreen.SetActive(false);
        }
    }

    private void OnDisable()
    {
        StopAndHide();
    }

    /// <summary>
    /// 이름이 일치하는 영상을 처음부터 재생하고 재생 종료 후 화면을 숨깁니다.
    /// </summary>
    public IEnumerator Play(string cueName)
    {
        VideoClip selectedClip = FindClip(cueName); // 이번 JSON 신호로 선택된 실제 영상입니다.
        if (selectedClip == null)
        {
            Debug.LogWarning($"JSON의 영상 신호 '{cueName}'에 연결된 VideoClip이 없습니다.", this);
            yield break;
        }

        prepareFailed = false;
        videoPlayer.errorReceived += HandleVideoError;
        videoPlayer.Stop();
        videoPlayer.clip = selectedClip;
        videoPlayer.Prepare();

        while (!videoPlayer.isPrepared && !prepareFailed)
        {
            yield return null;
        }

        if (prepareFailed)
        {
            videoPlayer.errorReceived -= HandleVideoError;
            StopAndHide();
            yield break;
        }

        if (videoScreen != null)
        {
            if (videoScreenCanvasGroup != null)
            {
                videoScreenCanvasGroup.alpha = 1f;
            }

            videoScreen.SetActive(true);
        }

        PauseGameAudio();
        ResetSkipInput();
        videoPlayer.Play();

        // Play 직후 한 프레임은 isPlaying이 아직 false일 수 있으므로 먼저 한 프레임 기다립니다.
        yield return null;

        while (videoPlayer.isPlaying)
        {
            if (ShouldSkipVideo())
            {
                break;
            }

            yield return null;
        }

        yield return FadeBackToGameScreen();
        videoPlayer.errorReceived -= HandleVideoError;
        StopAndHide();

        // 영상 종료에 사용한 입력이 다음 대사까지 넘기지 않도록 한 프레임 분리합니다.
        yield return null;
    }

    private VideoClip FindClip(string cueName)
    {
        if (string.IsNullOrWhiteSpace(cueName) || videoCues == null)
        {
            return null;
        }

        foreach (VideoCue cue in videoCues)
        {
            if (cue != null && string.Equals(cue.cueName, cueName, StringComparison.OrdinalIgnoreCase))
            {
                return cue.videoClip;
            }
        }

        return null;
    }

    private void HandleVideoError(VideoPlayer source, string message)
    {
        prepareFailed = true;
        Debug.LogError($"영상 재생에 실패했습니다: {message}", this);
    }

    private void StopAndHide()
    {
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }

        if (videoScreen != null)
        {
            videoScreen.SetActive(false);
        }

        if (videoScreenCanvasGroup != null)
        {
            videoScreenCanvasGroup.alpha = 1f;
        }

        RestoreGameAudio();
        ResetSkipInput();
    }

    /// <summary>
    /// 영상 화면을 서서히 투명하게 만들어 뒤의 기존 게임 화면이 자연스럽게 나타나게 합니다.
    /// </summary>
    private IEnumerator FadeBackToGameScreen()
    {
        if (videoScreenCanvasGroup == null || returnFadeDuration <= 0f)
        {
            yield break;
        }

        float elapsedTime = 0f; // 현재까지 진행된 영상 화면 페이드 시간입니다.
        while (elapsedTime < returnFadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float fadeRatio = Mathf.Clamp01(elapsedTime / returnFadeDuration); // 영상 화면이 사라진 0~1 진행 비율입니다.
            videoScreenCanvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeRatio);
            yield return null;
        }

        videoScreenCanvasGroup.alpha = 0f;
    }

    /// <summary>
    /// 영상 소리를 제외한 배경 음악과 게임 효과음을 영상 재생 동안 일시 정지합니다.
    /// </summary>
    private void PauseGameAudio()
    {
        if (audioPauseStateSaved)
        {
            return;
        }

        previousAudioPauseState = AudioListener.pause;
        audioPauseStateSaved = true;
        AudioListener.pause = true;
    }

    /// <summary>
    /// 영상 재생 전의 게임 전체 소리 일시 정지 상태로 되돌립니다.
    /// </summary>
    private void RestoreGameAudio()
    {
        if (!audioPauseStateSaved)
        {
            return;
        }

        AudioListener.pause = previousAudioPauseState;
        audioPauseStateSaved = false;
    }

    /// <summary>
    /// 지정된 시간 간격 안에 스페이스바가 두 번 입력되었는지 확인합니다.
    /// </summary>
    private bool ShouldSkipVideo()
    {
        if (!allowSkipWithDoubleSpace || !Input.GetKeyDown(KeyCode.Space))
        {
            return false;
        }

        float currentTime = Time.unscaledTime; // 게임 시간 정지 여부와 무관한 현재 입력 시간입니다.
        if (spacePressCount == 0 || currentTime - lastSpacePressTime > doubleSpaceInterval)
        {
            spacePressCount = 1;
            lastSpacePressTime = currentTime;
            return false;
        }

        spacePressCount++;
        lastSpacePressTime = currentTime;
        return spacePressCount >= 2;
    }

    /// <summary>
    /// 이전 영상에서 사용한 스페이스바 연속 입력 기록을 초기화합니다.
    /// </summary>
    private void ResetSkipInput()
    {
        spacePressCount = 0;
        lastSpacePressTime = 0f;
    }
}
