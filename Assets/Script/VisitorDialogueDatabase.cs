using System;
using UnityEngine;

/// <summary>
/// JSON 파일에서 사람과 늑대의 대사를 읽고 무작위 대사를 반환합니다.
/// </summary>
public class VisitorDialogueDatabase : MonoBehaviour
{
    [Serializable]
    private class DialogueJson // JSON 최상위 구조입니다.
    {
        public string[] humanDialogues; // 사람 방문객이 말할 수 있는 대사 목록입니다.
        public string[] wolfDialogues; // 늑대 방문객이 말할 수 있는 대사 목록입니다.
    }

    [SerializeField] private TextAsset dialogueJson; // 대사가 저장된 JSON 파일입니다.

    private DialogueJson dialogueData; // JSON을 읽어 메모리에 저장한 대사 데이터입니다.

    private void Awake()
    {
        LoadDialogue();
    }

    /// <summary>
    /// 방문객 종류에 맞는 대사를 무작위로 하나 선택합니다.
    /// </summary>
    public string GetRandomDialogue(bool isWolf)
    {
        if (dialogueData == null)
        {
            LoadDialogue();
        }

        string[] dialogues = isWolf ? dialogueData?.wolfDialogues : dialogueData?.humanDialogues; // 현재 방문객이 사용할 대사 배열입니다.
        if (dialogues == null || dialogues.Length == 0)
        {
            return "대사가 설정되지 않았습니다.";
        }

        return dialogues[UnityEngine.Random.Range(0, dialogues.Length)];
    }

    private void LoadDialogue()
    {
        if (dialogueJson == null || string.IsNullOrWhiteSpace(dialogueJson.text))
        {
            Debug.LogError("VisitorDialogueDatabase에 대사 JSON 파일을 연결해 주세요.", this);
            dialogueData = new DialogueJson();
            return;
        }

        dialogueData = JsonUtility.FromJson<DialogueJson>(dialogueJson.text);
        if (dialogueData == null)
        {
            Debug.LogError("방문객 대사 JSON 형식이 올바르지 않습니다.", this);
            dialogueData = new DialogueJson();
        }
    }
}
