using UnityEngine;

/// <summary>
/// 사람 또는 늑대 방문객을 무작위로 만들고 화면과 음성 자료를 정합니다.
/// </summary>
public class CounterVisitorGenerator : MonoBehaviour
{
    public readonly struct VisitorData // 한 방문객을 표시하는 데 필요한 실행 중 데이터입니다.
    {
        public readonly bool IsWolf; // 방문객이 늑대인지 나타냅니다.
        public readonly Sprite Sprite; // 방문객 오브젝트에 표시할 이미지입니다.
        public readonly AudioClip Voice; // 글자마다 재생할 방문객 음성입니다.
        public readonly string Dialogue; // 말풍선에 출력할 대사입니다.

        public VisitorData(bool isWolf, Sprite sprite, AudioClip voice, string dialogue)
        {
            IsWolf = isWolf;
            Sprite = sprite;
            Voice = voice;
            Dialogue = dialogue;
        }
    }

    [Header("방문객 등장 확률")]
    [SerializeField, Range(0f, 1f)] private float wolfChance = 0.3f; // 새 방문객이 늑대일 확률입니다.

    [Header("방문객 이미지")]
    [SerializeField] private Sprite humanSprite; // 모든 사람 방문객이 사용하는 이미지입니다.
    [SerializeField] private Sprite[] wolfSprites = new Sprite[3]; // 늑대 방문객이 무작위로 사용하는 세 이미지입니다.

    [Header("글자 음성")]
    [SerializeField] private AudioClip humanVoice; // 사람 대사의 글자마다 재생할 음성입니다.
    [SerializeField] private AudioClip wolfVoice; // 늑대 대사의 글자마다 재생할 음성입니다.

    [Header("대사")]
    [SerializeField] private VisitorDialogueDatabase dialogueDatabase; // JSON 대사를 제공하는 코드입니다.

    /// <summary>
    /// 종류, 이미지, 음성, 대사를 각각 무작위로 정해 새 방문객을 만듭니다.
    /// </summary>
    public VisitorData CreateVisitor()
    {
        bool wolfAlreadyCheckedIn = GameTimeManager.Instance != null && GameTimeManager.Instance.WolfEntered; // 오늘 늑대가 이미 체크인했는지 나타냅니다.
        bool isWolf = !wolfAlreadyCheckedIn && Random.value < wolfChance; // 늑대가 아직 들어오지 않았을 때만 확률에 따라 늑대를 생성합니다.
        Sprite selectedSprite = isWolf ? GetRandomWolfSprite() : humanSprite; // 이번 방문객에게 표시할 이미지입니다.
        AudioClip selectedVoice = isWolf ? wolfVoice : humanVoice; // 이번 방문객의 글자 음성입니다.
        string selectedDialogue = dialogueDatabase.GetRandomDialogue(isWolf); // 이번 방문객이 말할 JSON 대사입니다.

        return new VisitorData(isWolf, selectedSprite, selectedVoice, selectedDialogue);
    }

    private Sprite GetRandomWolfSprite()
    {
        if (wolfSprites == null || wolfSprites.Length == 0)
        {
            Debug.LogError("CounterVisitorGenerator에 늑대 이미지를 연결해 주세요.", this);
            return humanSprite;
        }

        return wolfSprites[Random.Range(0, wolfSprites.Length)];
    }
}
