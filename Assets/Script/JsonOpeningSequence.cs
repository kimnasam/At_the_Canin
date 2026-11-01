using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// JSON에 지정된 사진, 대사, 목소리, 효과음, 배경음을 순서대로 재생하는 범용 오프닝 연출입니다.
/// </summary>
public sealed class JsonOpeningSequence : MonoBehaviour
{
    [Serializable]
    private sealed class OpeningFile // JSON 파일 전체 구조입니다.
    {
        public string backgroundMusic; // 오프닝 시작부터 재생할 배경음 연결 이름입니다.
        public OpeningLine[] lines; // 순서대로 실행할 사진과 대사 목록입니다.
    }

    [Serializable]
    private sealed class OpeningLine // JSON의 대사 한 줄과 해당 줄의 연출 정보입니다.
    {
        public string text; // 화면에 한 글자씩 출력할 대사입니다.
        public string voice; // 글자 목소리를 선택할 화자 이름입니다.
        public string image; // 사진 목록에서 선택할 사진 이름이며 비어 있으면 현재 사진을 유지합니다.
        public string soundCue; // 문장 시작 시 재생할 효과음 이름입니다.
    }

    [Serializable]
    private sealed class ImageProfile // JSON 사진 이름과 Unity Sprite를 연결하는 설정입니다.
    {
        public string imageName; // JSON의 image 값과 비교할 사진 이름입니다.
        public Sprite sprite; // 사진 칸에 표시할 실제 Sprite입니다.
    }

    [Serializable]
    private sealed class VoiceProfile // JSON 화자 이름과 글자 목소리를 연결하는 설정입니다.
    {
        public string voiceName; // JSON의 voice 값과 비교할 화자 이름입니다.
        public AudioClip characterSound; // 해당 화자의 글자마다 재생할 짧은 목소리입니다.
    }

    [Serializable]
    private sealed class SoundProfile // JSON 효과음 이름과 AudioClip을 연결하는 설정입니다.
    {
        public string soundName; // JSON의 soundCue 값과 비교할 효과음 이름입니다.
        public AudioClip audioClip; // 문장 시작 시 재생할 실제 효과음입니다.
        public bool loop; // 다음 효과음이나 오프닝 종료 전까지 반복할지 나타냅니다.
    }

    [Serializable]
    private sealed class MusicProfile // JSON 배경음 이름과 AudioClip을 연결하는 설정입니다.
    {
        public string musicName; // JSON의 backgroundMusic 값과 비교할 배경음 이름입니다.
        public AudioClip audioClip; // 오프닝 동안 반복 재생할 실제 배경음입니다.
    }

    [Header("JSON과 다음 씬")]
    [SerializeField] private TextAsset openingJson; // 오프닝 대사와 연출 순서가 저장된 JSON 파일입니다.
    [SerializeField] private string nextSceneName = "Stage"; // 오프닝 종료 후 불러올 씬 이름입니다.

    [Header("사진 칸")]
    [SerializeField] private Image photoImage; // JSON에서 선택한 Sprite를 표시할 UI 사진 칸입니다.
    [SerializeField] private Image photoBlackOverlay; // 사진 교체 중 사진 칸만 검게 덮을 UI Image입니다.
    [SerializeField] private ImageProfile[] imageProfiles; // JSON 사진 이름과 Sprite 연결 목록입니다.
    [SerializeField, Min(0.01f)] private float imageFadeDuration = 1f; // 사진 칸이 검게 변하거나 다시 나타나는 시간입니다.

    [Header("대화 UI")]
    [SerializeField] private GameObject dialogueWindow; // 대화창 이미지와 대사 Text를 포함하는 최상위 오브젝트입니다.
    [SerializeField] private TMP_Text dialogueText; // 현재 대사를 출력할 TextMesh Pro 텍스트입니다.
    [SerializeField, Min(0.01f)] private float secondsPerCharacter = 0.05f; // 글자 하나를 출력한 뒤 기다리는 시간입니다.

    [Header("글자 목소리")]
    [SerializeField] private AudioSource characterAudioSource; // 캐릭터 글자 목소리를 재생할 AudioSource입니다.
    [SerializeField] private string defaultVoiceName; // JSON에 voice가 없을 때 사용할 기본 화자 이름입니다.
    [SerializeField] private VoiceProfile[] voiceProfiles; // 화자 이름과 글자 목소리 연결 목록입니다.

    [Header("효과음")]
    [SerializeField] private AudioSource soundAudioSource; // 문장 효과음을 재생할 AudioSource입니다.
    [SerializeField] private SoundProfile[] soundProfiles; // 효과음 이름과 AudioClip 연결 목록입니다.

    [Header("배경음")]
    [SerializeField] private AudioSource musicAudioSource; // 오프닝 배경음을 재생할 AudioSource입니다.
    [SerializeField] private MusicProfile[] musicProfiles; // 배경음 이름과 AudioClip 연결 목록입니다.
    [SerializeField, Min(0.01f)] private float musicFadeOutDuration = 1f; // 오프닝 종료 시 배경음이 작아지는 시간입니다.

    private Color photoBlackOriginalColor; // 사진 검은 덮개의 원래 RGB와 최대 투명도입니다.
    private string currentImageName = string.Empty; // 현재 사진 칸에 표시 중인 JSON 사진 이름입니다.
    private AudioClip currentCharacterSound; // 현재 화자에게 선택된 글자 목소리입니다.
    private bool printImmediately; // 입력으로 현재 문장의 남은 글자를 즉시 출력할지 나타냅니다.
    private bool isPlaying; // 오프닝이 중복 실행되는 것을 막는 값입니다.

    private void Awake()
    {
        if (photoBlackOverlay != null)
        {
            photoBlackOriginalColor = photoBlackOverlay.color;
            SetPhotoBlackAlpha(1f);
        }

        if (dialogueWindow != null)
        {
            dialogueWindow.SetActive(false);
        }
    }

    private IEnumerator Start()
    {
        if (!AreRequiredSettingsValid())
        {
            yield break;
        }

        yield return PlayOpening();
    }

    /// <summary>
    /// JSON을 읽고 사진과 대사를 모두 재생한 뒤 다음 씬으로 이동합니다.
    /// </summary>
    private IEnumerator PlayOpening()
    {
        if (isPlaying)
        {
            yield break;
        }

        OpeningFile openingFile = JsonUtility.FromJson<OpeningFile>(openingJson.text); // 선택한 JSON에서 읽은 전체 오프닝 데이터입니다.
        if (openingFile == null || openingFile.lines == null || openingFile.lines.Length == 0)
        {
            Debug.LogError($"{openingJson.name}에 실행할 lines가 없습니다.", this);
            yield break;
        }

        isPlaying = true;
        dialogueWindow.SetActive(true);
        dialogueText.text = string.Empty;
        PlayBackgroundMusic(openingFile.backgroundMusic);

        // 씬을 불러온 입력이 첫 대사를 바로 넘기지 않도록 한 프레임 기다립니다.
        yield return null;

        foreach (OpeningLine line in openingFile.lines)
        {
            if (line == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(line.image) && !string.Equals(currentImageName, line.image, StringComparison.OrdinalIgnoreCase))
            {
                yield return ChangePhoto(line.image);
            }

            SelectVoice(line.voice);
            PlaySound(line.soundCue);
            yield return PrintLine(line.text ?? string.Empty);
            yield return WaitForAdvanceInput();
        }

        dialogueText.text = string.Empty;
        dialogueWindow.SetActive(false);
        yield return FadePhotoToBlack();
        yield return FadeMusicOut();

        StopAllAudio();
        StageOpeningSequence.RequestOpening(); // 다음 Stage 진입에서 기존 플레이어 등장 연출을 한 번 실행합니다.
        SceneManager.LoadScene(nextSceneName);
    }

    /// <summary>
    /// 사진 칸을 검게 덮고 Sprite를 교체한 뒤 새 사진을 다시 드러냅니다.
    /// </summary>
    private IEnumerator ChangePhoto(string imageName)
    {
        Sprite nextSprite = FindImage(imageName); // JSON 사진 이름과 연결된 다음 Sprite입니다.
        if (nextSprite == null)
        {
            Debug.LogWarning($"JSON의 사진 '{imageName}'에 연결된 Sprite가 없습니다.", this);
            yield break;
        }

        yield return FadePhotoBlackOverlay(photoBlackOverlay.color.a, 1f);
        photoImage.sprite = nextSprite;
        photoImage.enabled = true;
        currentImageName = imageName;
        yield return FadePhotoBlackOverlay(1f, 0f);
    }

    private Sprite FindImage(string imageName)
    {
        if (imageProfiles == null)
        {
            return null;
        }

        foreach (ImageProfile profile in imageProfiles)
        {
            if (profile != null && string.Equals(profile.imageName, imageName, StringComparison.OrdinalIgnoreCase))
            {
                return profile.sprite;
            }
        }

        return null;
    }

    private IEnumerator FadePhotoToBlack()
    {
        yield return FadePhotoBlackOverlay(photoBlackOverlay.color.a, 1f);
    }

    private IEnumerator FadePhotoBlackOverlay(float startAlpha, float endAlpha)
    {
        float elapsedTime = 0f; // 이번 사진 검은 덮개 페이드가 진행된 시간입니다.

        while (elapsedTime < imageFadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsedTime / imageFadeDuration); // 현재 사진 전환 진행 비율입니다.
            SetPhotoBlackAlpha(Mathf.Lerp(startAlpha, endAlpha, progress));
            yield return null;
        }

        SetPhotoBlackAlpha(endAlpha);
    }

    private IEnumerator PrintLine(string fullText)
    {
        dialogueText.text = string.Empty;
        printImmediately = false;

        for (int index = 0; index < fullText.Length; index++)
        {
            if (WasAdvancePressed())
            {
                printImmediately = true;
            }

            char character = fullText[index]; // 현재 화면에 추가할 한 글자입니다.
            dialogueText.text += character;

            if (!printImmediately && !char.IsWhiteSpace(character))
            {
                PlayCharacterSound();
            }

            float waitedTime = 0f; // 현재 글자 뒤에서 기다린 실제 시간입니다.
            while (!printImmediately && waitedTime < secondsPerCharacter)
            {
                if (WasAdvancePressed())
                {
                    printImmediately = true;
                    break;
                }

                waitedTime += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        yield return null;
    }

    private IEnumerator WaitForAdvanceInput()
    {
        while (!WasAdvancePressed())
        {
            yield return null;
        }

        yield return null;
    }

    private bool WasAdvancePressed()
    {
        return Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
    }

    private void SelectVoice(string voiceName)
    {
        currentCharacterSound = null;
        string selectedVoiceName = string.IsNullOrWhiteSpace(voiceName) ? defaultVoiceName : voiceName; // 실제로 검색할 화자 이름입니다.

        if (string.IsNullOrWhiteSpace(selectedVoiceName) || voiceProfiles == null)
        {
            return;
        }

        foreach (VoiceProfile profile in voiceProfiles)
        {
            if (profile != null && string.Equals(profile.voiceName, selectedVoiceName, StringComparison.OrdinalIgnoreCase))
            {
                currentCharacterSound = profile.characterSound;
                return;
            }
        }

        Debug.LogWarning($"JSON의 화자 '{selectedVoiceName}'에 연결된 목소리가 없습니다.", this);
    }

    private void PlayCharacterSound()
    {
        if (characterAudioSource != null && currentCharacterSound != null)
        {
            characterAudioSource.PlayOneShot(currentCharacterSound);
        }
    }

    private void PlaySound(string soundName)
    {
        if (string.IsNullOrWhiteSpace(soundName) || soundAudioSource == null || soundProfiles == null)
        {
            return;
        }

        foreach (SoundProfile profile in soundProfiles)
        {
            if (profile == null || profile.audioClip == null || !string.Equals(profile.soundName, soundName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            soundAudioSource.Stop();
            soundAudioSource.clip = profile.audioClip;
            soundAudioSource.loop = profile.loop;
            soundAudioSource.Play();
            return;
        }

        Debug.LogWarning($"JSON의 효과음 '{soundName}'에 연결된 AudioClip이 없습니다.", this);
    }

    private void PlayBackgroundMusic(string musicName)
    {
        if (string.IsNullOrWhiteSpace(musicName) || musicAudioSource == null || musicProfiles == null)
        {
            return;
        }

        foreach (MusicProfile profile in musicProfiles)
        {
            if (profile == null || profile.audioClip == null || !string.Equals(profile.musicName, musicName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            musicAudioSource.clip = profile.audioClip;
            musicAudioSource.loop = true;
            musicAudioSource.Play();
            return;
        }

        Debug.LogWarning($"JSON의 배경음 '{musicName}'에 연결된 AudioClip이 없습니다.", this);
    }

    private IEnumerator FadeMusicOut()
    {
        if (musicAudioSource == null || !musicAudioSource.isPlaying)
        {
            yield break;
        }

        float startVolume = musicAudioSource.volume; // 페이드 시작 전 Inspector에 설정된 배경음 음량입니다.
        float elapsedTime = 0f; // 배경음 페이드 아웃이 진행된 시간입니다.

        while (elapsedTime < musicFadeOutDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsedTime / musicFadeOutDuration); // 현재 배경음 페이드 진행 비율입니다.
            musicAudioSource.volume = Mathf.Lerp(startVolume, 0f, progress);
            yield return null;
        }

        musicAudioSource.volume = 0f;
    }

    private void StopAllAudio()
    {
        if (characterAudioSource != null)
        {
            characterAudioSource.Stop();
        }

        if (soundAudioSource != null)
        {
            soundAudioSource.Stop();
        }

        if (musicAudioSource != null)
        {
            musicAudioSource.Stop();
        }
    }

    private void SetPhotoBlackAlpha(float alpha)
    {
        Color changedColor = photoBlackOriginalColor; // 사진 덮개에 적용할 원래 RGB 기반 색상입니다.
        changedColor.a = alpha;
        photoBlackOverlay.color = changedColor;
    }

    private bool AreRequiredSettingsValid()
    {
        bool hasJsonAndScene = openingJson != null && !string.IsNullOrWhiteSpace(nextSceneName); // JSON과 다음 씬 이름 설정 여부입니다.
        bool hasPhotoObjects = photoImage != null && photoBlackOverlay != null; // 사진과 검은 덮개 연결 여부입니다.
        bool hasDialogueObjects = dialogueWindow != null && dialogueText != null; // 대화창과 TMP 텍스트 연결 여부입니다.

        if (!hasJsonAndScene || !hasPhotoObjects || !hasDialogueObjects)
        {
            Debug.LogError("JsonOpeningSequence의 JSON, 다음 씬 이름, 사진 Image, 검은 덮개 Image, 대화창, 대사 Text를 확인해 주세요.", this);
            return false;
        }

        return true;
    }
}
