using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// 하나의 방문객 오브젝트에서 이미지를 교체하고 입장, 체크인, 퇴장 애니메이션을 재생합니다.
/// </summary>
public class CounterVisitorView : MonoBehaviour
{
    [Header("방문객 표시")]
    [SerializeField] private SpriteRenderer visitorRenderer; // 사람과 늑대 이미지가 계속 교체되는 한 개의 렌더러입니다.
    [SerializeField] private Animator visitorAnimator; // 애니메이션 클립을 방문객 오브젝트에 적용할 Animator입니다.

    [Header("직접 재생할 애니메이션")]
    [SerializeField] private AnimationClip enterAnimation; // 방문객이 화면 안으로 들어오는 애니메이션입니다.
    [SerializeField] private AnimationClip checkInAnimation; // 방문객을 들여보낼 때 재생하는 애니메이션입니다.
    [SerializeField] private AnimationClip leaveAnimation; // 방문객을 내보낼 때 재생하는 애니메이션입니다.

    private PlayableGraph animationGraph; // Animator Controller 없이 현재 클립을 직접 재생하는 그래프입니다.

    public void SetVisitorSprite(Sprite visitorSprite)
    {
        visitorRenderer.sprite = visitorSprite;
        visitorRenderer.enabled = visitorSprite != null;
    }

    public IEnumerator PlayEnter()
    {
        yield return PlayAnimation(enterAnimation);
    }

    public IEnumerator PlayCheckIn()
    {
        yield return PlayAnimation(checkInAnimation);
    }

    public IEnumerator PlayLeave()
    {
        yield return PlayAnimation(leaveAnimation);
    }

    public void HideVisitor()
    {
        visitorRenderer.enabled = false;
    }

    private IEnumerator PlayAnimation(AnimationClip animationClip)
    {
        StopCurrentAnimation();

        if (animationClip == null)
        {
            Debug.LogError("CounterVisitorView에 재생할 애니메이션 클립을 연결해 주세요.", this);
            yield break;
        }

        AnimationPlayableUtilities.PlayClip(visitorAnimator, animationClip, out animationGraph);
        yield return new WaitForSeconds(animationClip.length);
        StopCurrentAnimation();
    }

    private void OnDestroy()
    {
        StopCurrentAnimation();
    }

    private void StopCurrentAnimation()
    {
        if (animationGraph.IsValid())
        {
            animationGraph.Destroy();
        }
    }
}
