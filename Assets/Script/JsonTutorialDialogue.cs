using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// JSON 대사를 한 글자씩 표시하고 스페이스 또는 마우스 클릭으로 진행하는 공통 설명 대화입니다.
/// </summary>
public sealed class JsonTutorialDialogue : MonoBehaviour
{
    public enum DialogueMoment // 이 대화가 새 게임의 어느 순간에 한 번 실행되는지 구분합니다.
    {
        Morning,
        Counter,
        Night
    }

    [Serializable]
    private sealed class DialogueFile
    {
        public DialogueLine[] lines; // JSON에 저장된 문장 목록입니다.
    }

    [Serializable]
    private sealed class DialogueLine
    {
        public string text; // 화면에 출력할 한 문장입니다.
        public string voice; // 이 문장을 말하는 사람의 목소리 연결 이름입니다.
        public string soundCue; // 문장 시작 시 재생할 선택적인 사운드 신호 이름입니다.
    }

    [Serializable]
    private sealed class VoiceProfile
    {
        public string voiceName; // JSON의 voice와 비교할 화자 이름입니다.
        public AudioClip characterSound; // 해당 화자의 글자마다 재생할 짧은 목소리입니다.
    }

    [Serializable]
    private sealed class SoundCue
    {
        public string cueName; // JSON의 soundCue와 비교할 이름입니다.
        public AudioClip audioClip; // 신호가 일치할 때 재생할 효과음입니다.
        public bool loop; // 재난 경보처럼 다음 정지 지점까지 반복 재생할지 결정합니다.
    }

    [Header("대화 실행 조건")]
    [SerializeField] private DialogueMoment dialogueMoment; // 아침, 카운터, 첫 밤 중 이 대화의 실행 시점입니다.
    [SerializeField] private TextAsset dialogueJson; // lines 배열이 들어 있는 상황별 JSON 파일입니다.

    [Header("기존 대화 UI")]
    [SerializeField] private GameObject dialogueWindow; // 기존 대화창 이미지와 Text를 포함하는 최상위 오브젝트입니다.
    [SerializeField] private Text dialogueText; // 문장을 표시할 Legacy UI Text입니다.

    [Header("글자 출력")]
    [SerializeField, Min(0.01f)] private float secondsPerCharacter = 0.05f; // 글자 하나를 추가한 뒤 기다리는 시간입니다.
    [SerializeField] private AudioSource characterAudioSource; // 글자마다 짧은 소리를 출력할 전용 AudioSource입니다.
    [SerializeField] private string defaultVoiceName; // JSON에 voice가 없을 때 사용할 기본 화자 이름입니다.
    [SerializeField] private VoiceProfile[] voiceProfiles; // 화자 이름과 서로 다른 글자 목소리를 연결하는 목록입니다.

    [Header("문장 사운드 큐")]
    [SerializeField] private AudioSource cueAudioSource; // 재난 경보음 같은 문장 효과음을 출력할 별도 AudioSource입니다.
    [SerializeField] private SoundCue[] soundCues; // JSON 신호 이름과 실제 AudioClip의 연결 목록입니다.
    [SerializeField] private bool stopCueWhenDialogueEnds = true; // 전체 대화 종료 시 현재 문장 효과음을 정지할지 결정합니다.

    private bool lockHeld; // 이 컴포넌트가 공통 게임 진행 잠금을 보유하는지 나타냅니다.
    private bool isPlaying; // 중복 대화 실행을 막는 값입니다.
    private bool printCurrentLineImmediately; // 출력 중 입력을 받아 남은 글자를 즉시 표시할지 나타냅니다.
    private AudioClip currentCharacterSound; // 현재 문장의 화자에게 지정된 글자 목소리입니다.

    public bool WillPlay => ShouldPlay(); // 현재 새 게임에서 이 대화가 아직 필요한지 나타냅니다.

    /// <summary>
    /// 실제 UI 출력 전 페이드나 등장 연출이 진행되는 동안에도 다른 게임 시스템이 시작되지 않게 미리 잠급니다.
    /// </summary>
    public void ReserveIfNeeded()
    {
        if (ShouldPlay())
        {
            AcquireGameplayLock();
        }
    }

    private void Awake()
    {
        if (dialogueWindow != null)
        {
            dialogueWindow.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        ReleaseGameplayLock();
    }

    /// <summary>
    /// 실행 조건이 맞으면 대화를 끝까지 재생하고, 아니면 즉시 종료합니다.
    /// </summary>
    public IEnumerator PlayIfNeeded()
    {
        if (isPlaying || !ShouldPlay())
        {
            ReleaseGameplayLock();
            yield break;
        }

        if (!AreSettingsValid())
        {
            ReleaseGameplayLock();
            yield break;
        }

        DialogueFile file = JsonUtility.FromJson<DialogueFile>(dialogueJson.text); // 선택한 JSON에서 읽은 전체 대화 데이터입니다.
        if (file == null || file.lines == null || file.lines.Length == 0)
        {
            Debug.LogWarning($"{dialogueJson.name}에 출력할 lines가 없습니다.", this);
            ReleaseGameplayLock();
            yield break;
        }

        isPlaying = true;
        AcquireGameplayLock();
        dialogueWindow.SetActive(true);
        dialogueText.text = string.Empty;

        // 씬 진입에 사용한 클릭이나 스페이스 입력이 첫 문장을 바로 넘기지 않게 한 프레임 기다립니다.
        yield return null;

        foreach (DialogueLine line in file.lines)
        {
            string fullText = line != null && line.text != null ? line.text : string.Empty; // null도 안전하게 표시할 실제 문장입니다.
            SelectVoice(line != null ? line.voice : string.Empty);
            PlaySoundCue(line != null ? line.soundCue : string.Empty);
            yield return PrintLine(fullText);
            yield return WaitForAdvanceInput();
        }

        MarkAsPlayed();
        dialogueText.text = string.Empty;
        dialogueWindow.SetActive(false);

        if (stopCueWhenDialogueEnds && cueAudioSource != null)
        {
            cueAudioSource.Stop();
        }

        isPlaying = false;
        ReleaseGameplayLock();
    }

    /// <summary>
    /// 입력이 들어오면 남은 대기 시간을 생략하면서 현재 문장을 한 글자씩 출력합니다.
    /// </summary>
    private IEnumerator PrintLine(string fullText)
    {
        dialogueText.text = string.Empty;
        printCurrentLineImmediately = false;

        for (int index = 0; index < fullText.Length; index++)
        {
            if (WasAdvancePressed())
            {
                printCurrentLineImmediately = true;
            }

            char character = fullText[index]; // 이번 반복에서 화면에 추가할 한 글자입니다.
            dialogueText.text += character;

            if (!char.IsWhiteSpace(character))
            {
                PlayCurrentCharacterSound();
            }

            if (!printCurrentLineImmediately)
            {
                float waitedTime = 0f; // 현재 글자 뒤에서 기다린 실제 시간입니다.
                while (waitedTime < secondsPerCharacter && !printCurrentLineImmediately)
                {
                    if (WasAdvancePressed())
                    {
                        printCurrentLineImmediately = true;
                        break;
                    }

                    waitedTime += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        // 즉시 완성에 사용한 입력이 같은 프레임에 다음 문장까지 넘기지 않도록 분리합니다.
        yield return null;
    }

    /// <summary>
    /// 완성된 문장에서 새 스페이스 또는 마우스 왼쪽 클릭을 기다립니다.
    /// </summary>
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

    /// <summary>
    /// 현재 JSON 문장에 지정된 화자 이름과 일치하는 글자 목소리를 선택합니다.
    /// </summary>
    private void SelectVoice(string voiceName)
    {
        currentCharacterSound = null;
        string selectedVoiceName = string.IsNullOrWhiteSpace(voiceName) ? defaultVoiceName : voiceName; // 문장에 화자가 없을 때 적용할 실제 화자 이름입니다.

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

    /// <summary>
    /// 현재 문장을 말하는 화자의 목소리를 공백이 아닌 글자마다 재생합니다.
    /// </summary>
    private void PlayCurrentCharacterSound()
    {
        if (characterAudioSource != null && currentCharacterSound != null)
        {
            characterAudioSource.PlayOneShot(currentCharacterSound);
        }
    }

    /// <summary>
    /// JSON의 이름과 일치하는 문장 효과음을 별도 AudioSource에서 재생합니다.
    /// </summary>
    private void PlaySoundCue(string cueName)
    {
        if (string.IsNullOrWhiteSpace(cueName) || cueAudioSource == null || soundCues == null)
        {
            return;
        }

        foreach (SoundCue cue in soundCues)
        {
            if (cue == null || cue.audioClip == null || !string.Equals(cue.cueName, cueName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            cueAudioSource.Stop();
            cueAudioSource.clip = cue.audioClip;
            cueAudioSource.loop = cue.loop;
            cueAudioSource.Play();
            return;
        }

        Debug.LogWarning($"JSON의 사운드 신호 '{cueName}'에 연결된 AudioClip이 없습니다.", this);
    }

    private bool ShouldPlay()
    {
        if (GameTimeManager.Instance == null)
        {
            return false;
        }

        switch (dialogueMoment)
        {
            case DialogueMoment.Morning:
                return GameTimeManager.Instance.CurrentTime == GameTimeManager.TimePeriod.Morning
                    && !GameTimeManager.Instance.MorningDialoguePlayed;
            case DialogueMoment.Counter:
                return GameTimeManager.Instance.CurrentTime == GameTimeManager.TimePeriod.Morning
                    && !GameTimeManager.Instance.CounterDialoguePlayed;
            case DialogueMoment.Night:
                return GameTimeManager.Instance.CurrentTime == GameTimeManager.TimePeriod.Night
                    && !GameTimeManager.Instance.NightDialoguePlayed;
            default:
                return false;
        }
    }

    private void MarkAsPlayed()
    {
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.MarkDialoguePlayed(dialogueMoment);
        }
    }

    private void AcquireGameplayLock()
    {
        if (!lockHeld)
        {
            GameplayDialogueLock.Acquire();
            lockHeld = true;
        }
    }

    private void ReleaseGameplayLock()
    {
        if (lockHeld)
        {
            GameplayDialogueLock.Release();
            lockHeld = false;
        }
    }

    private bool AreSettingsValid()
    {
        bool valid = dialogueJson != null && dialogueWindow != null && dialogueText != null; // 필수 JSON과 기존 UI가 연결되었는지 나타냅니다.
        if (!valid)
        {
            Debug.LogError("JsonTutorialDialogue의 Dialogue Json, Dialogue Window, Dialogue Text를 모두 연결해 주세요.", this);
        }

        return valid;
    }
}
