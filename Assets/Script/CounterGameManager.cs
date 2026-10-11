using System.Collections;
using UnityEngine;

/// <summary>
/// 방문객 생성부터 선택, 정원 확인, 밤 전환까지 카운터 게임의 전체 순서를 제어합니다.
/// </summary>
public class CounterGameManager : MonoBehaviour
{
    [Header("카운터 구성 요소")]
    [SerializeField] private CounterVisitorGenerator visitorGenerator; // 무작위 방문객을 만드는 코드입니다.
    [SerializeField] private CounterVisitorView visitorView; // 방문객 이미지와 애니메이션을 표시하는 코드입니다.
    [SerializeField] private TypewriterDialogue dialogueView; // 말풍선 대사를 출력하는 코드입니다.
    [SerializeField] private CounterDecisionUI decisionUI; // 두 선택 버튼과 정원을 표시하는 코드입니다.
    [SerializeField] private SceneFadeController fadeController; // 업무 완료 뒤 Stage 씬으로 이동시키는 코드입니다.

    [Header("씬 설정")]
    [SerializeField] private string stageSceneName = "Stage"; // 밤이 되었을 때 돌아갈 스테이지 씬 이름입니다.
    [SerializeField, Min(0f)] private float nextVisitorDelay = 0.35f; // 한 방문객이 나간 뒤 다음 방문객이 들어오기까지의 시간입니다.

    private CounterVisitorGenerator.VisitorData currentVisitor; // 현재 플레이어가 판별 중인 방문객입니다.
    private bool canChoose; // 대사가 끝나 선택 버튼을 누를 수 있는 상태인지 나타냅니다.

    private void Start()
    {
        if (GameTimeManager.Instance == null)
        {
            Debug.LogError("씬에 GameTimeManager가 필요합니다.", this);
            enabled = false;
            return;
        }

        GameTimeManager.Instance.BeginMorning();
        decisionUI.Initialize(CheckInCurrentVisitor, SendAwayCurrentVisitor);
        decisionUI.UpdateCapacity(0, GameTimeManager.Instance.MaximumGuests);
        StartCoroutine(ShowNextVisitor());
    }

    public void CheckInCurrentVisitor()
    {
        if (canChoose)
        {
            StartCoroutine(ResolveDecision(true));
        }
    }

    public void SendAwayCurrentVisitor()
    {
        if (canChoose)
        {
            StartCoroutine(ResolveDecision(false));
        }
    }

    private IEnumerator ShowNextVisitor()
    {
        canChoose = false;
        decisionUI.SetButtonsVisible(false);
        dialogueView.Hide();

        currentVisitor = visitorGenerator.CreateVisitor();
        visitorView.SetVisitorSprite(currentVisitor.Sprite);
        yield return visitorView.PlayEnter();
        yield return dialogueView.ShowDialogue(currentVisitor.Dialogue, currentVisitor.Voice);

        canChoose = true;
        decisionUI.SetButtonsVisible(true);
    }

    private IEnumerator ResolveDecision(bool checkedIn)
    {
        canChoose = false;
        decisionUI.SetButtonsVisible(false);
        dialogueView.Hide();

        if (checkedIn)
        {
            GameTimeManager.Instance.RegisterCheckIn(currentVisitor.IsWolf);
            decisionUI.UpdateCapacity(GameTimeManager.Instance.CheckedInGuests, GameTimeManager.Instance.MaximumGuests);
            yield return visitorView.PlayCheckIn();
        }
        else
        {
            yield return visitorView.PlayLeave();
        }

        visitorView.HideVisitor();

        if (GameTimeManager.Instance.CheckedInGuests >= GameTimeManager.Instance.MaximumGuests)
        {
            GameTimeManager.Instance.BeginNight();
            yield return fadeController.FadeOutAndLoad(stageSceneName);
            yield break;
        }

        yield return new WaitForSeconds(nextVisitorDelay);
        yield return ShowNextVisitor();
    }
}
