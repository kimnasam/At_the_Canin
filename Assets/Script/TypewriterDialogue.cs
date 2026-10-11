using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 말풍선을 켜고 대사를 한 글자씩 출력하면서 글자 음성을 재생합니다.
/// </summary>
public class TypewriterDialogue : MonoBehaviour
{
    [SerializeField] private GameObject speechBubble; // 대사를 표시하는 말풍선 전체 오브젝트입니다.
    [SerializeField] private Text dialogueText; // 한 글자씩 내용이 들어갈 Legacy UI Text입니다.
    [SerializeField] private AudioSource voiceAudioSource; // 방문객의 짧은 글자 음성을 재생할 AudioSource입니다.
    [SerializeField, Min(0.01f)] private float secondsPerCharacter = 0.05f; // 다음 글자가 나타날 때까지 기다리는 시간입니다.

    private void Awake()
    {
        Hide();
    }

    public IEnumerator ShowDialogue(string dialogue, AudioClip voice)
    {
        speechBubble.SetActive(true);
        dialogueText.text = string.Empty;

        foreach (char character in dialogue)
        {
            dialogueText.text += character;

            if (!char.IsWhiteSpace(character) && voice != null)
            {
                voiceAudioSource.PlayOneShot(voice);
            }

            yield return new WaitForSeconds(secondsPerCharacter);
        }
    }

    public void Hide()
    {
        dialogueText.text = string.Empty;
        speechBubble.SetActive(false);
        voiceAudioSource.Stop();
    }
}
