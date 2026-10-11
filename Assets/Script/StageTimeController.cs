using UnityEngine;

/// <summary>
/// 현재 시간이 아침인지 밤인지에 따라 스테이지의 오브젝트와 배경 이미지를 변경합니다.
/// </summary>
public class StageTimeController : MonoBehaviour
{
    [Header("시간별 오브젝트")]
    [SerializeField] private GameObject[] morningObjects; // 아침에만 활성화할 오브젝트 목록입니다.
    [SerializeField] private GameObject[] nightObjects; // 밤에만 활성화할 오브젝트 목록입니다.

    [Header("창문 이미지")]
    [SerializeField] private SpriteRenderer[] windowRenderers; // 시간에 따라 이미지를 교체할 모든 창문입니다.
    [SerializeField] private Sprite morningWindowSprite; // 아침에 창문에 표시할 이미지입니다.
    [SerializeField] private Sprite nightWindowSprite; // 밤에 창문에 표시할 이미지입니다.

    [Header("달 배경")]
    [SerializeField] private SpriteRenderer moonBackground; // 아침에는 숨기고 밤에는 표시할 달 배경입니다.

    private Color moonOriginalColor; // 달 배경의 원래 RGB 색상을 보관합니다.

    private void Awake()
    {
        if (moonBackground != null)
        {
            moonOriginalColor = moonBackground.color;
        }
    }

    private void Start()
    {
        if (GameTimeManager.Instance == null)
        {
            Debug.LogError("씬에 GameTimeManager가 필요합니다.", this);
            enabled = false;
            return;
        }

        GameTimeManager.Instance.TimeChanged += ApplyTime;
        ApplyTime(GameTimeManager.Instance.CurrentTime);
    }

    private void OnDestroy()
    {
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.TimeChanged -= ApplyTime;
        }
    }

    /// <summary>
    /// 새로운 아침을 시작하고 카운터 결과를 초기화합니다.
    /// </summary>
    public void StartNewMorning()
    {
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.BeginMorning();
        }
    }

    /// <summary>
    /// 현재 시간에 맞춰 오브젝트, 창문 이미지, 달 배경을 한 번에 갱신합니다.
    /// </summary>
    private void ApplyTime(GameTimeManager.TimePeriod time)
    {
        bool isMorning = time == GameTimeManager.TimePeriod.Morning; // 아침 상태인지 판별한 결과입니다.

        SetObjectsActive(morningObjects, isMorning);
        SetObjectsActive(nightObjects, !isMorning);
        SetWindowSprite(isMorning ? morningWindowSprite : nightWindowSprite);
        SetMoonAlpha(isMorning ? 0f : 1f);
    }

    /// <summary>
    /// 목록에 연결된 오브젝트의 활성 상태를 변경합니다.
    /// </summary>
    private void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null)
        {
            return;
        }

        foreach (GameObject targetObject in objects)
        {
            if (targetObject != null)
            {
                targetObject.SetActive(active);
            }
        }
    }

    /// <summary>
    /// 모든 창문에 현재 시간에 맞는 이미지를 적용합니다.
    /// </summary>
    private void SetWindowSprite(Sprite targetSprite)
    {
        if (windowRenderers == null || targetSprite == null)
        {
            return;
        }

        foreach (SpriteRenderer windowRenderer in windowRenderers)
        {
            if (windowRenderer != null)
            {
                windowRenderer.sprite = targetSprite;
            }
        }
    }

    /// <summary>
    /// 달 배경의 RGB 색상은 유지하고 불투명도만 변경합니다.
    /// </summary>
    private void SetMoonAlpha(float alpha)
    {
        if (moonBackground == null)
        {
            return;
        }

        Color changedColor = moonOriginalColor; // 달 배경에 새로 적용할 색상입니다.
        changedColor.a = alpha;
        moonBackground.color = changedColor;
    }
}
