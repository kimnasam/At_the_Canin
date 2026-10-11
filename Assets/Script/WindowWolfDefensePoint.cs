using System.Collections;
using UnityEngine;

/// <summary>
/// 창문 하나의 늑대 이미지, 플레이어 감지, 손전등 입력과 실내 스폰 위치를 관리합니다.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class WindowWolfDefensePoint : MonoBehaviour
{
    [Header("이벤트 연결")]
    [SerializeField] private NightWindowDefenseController defenseController; // 손전등 사용 결과를 전달할 밤 방어 관리자입니다.
    [SerializeField] private string playerTag = "Player"; // 상호작용 범위에 들어왔는지 판별할 플레이어 태그입니다.

    [Header("창문 이미지")]
    [SerializeField] private Sprite normalWindowSprite; // 늑대가 없을 때 표시할 밤 창문 이미지입니다.
    [SerializeField] private Sprite wolfWindowSprite; // 늑대가 창밖에 서 있을 때 표시할 창문 이미지입니다.

    [Header("상호작용 표시")]
    [SerializeField] private GameObject interactionPrompt; // "스페이스바를 눌러 후레쉬를 비추기" 안내 UI입니다.
    [SerializeField] private GameObject flashlightVisual; // 스페이스바를 눌렀을 때 잠시 보여 줄 손전등 빛 오브젝트입니다.
    [SerializeField, Min(0f)] private float flashlightVisualDuration = 0.2f; // 손전등 빛을 화면에 유지할 시간입니다.

    [Header("늑대 생성 위치")]
    [SerializeField] private Transform wolfSpawnPoint; // 방어 실패 시 늑대를 생성할 창문 안쪽 위치입니다.

    private SpriteRenderer windowRenderer; // 일반 창문과 늑대 창문 이미지를 교체할 렌더러입니다.
    private float wolfAppearedTime; // 현재 늑대 이미지가 나타난 게임 시간입니다.
    private bool playerInside; // 플레이어가 이 창문의 Trigger 안에 있는지 나타냅니다.
    private bool wolfVisible; // 현재 이 창문에 늑대가 서 있는지 나타냅니다.
    private Coroutine flashlightCoroutine; // 연속 입력 시 이전 손전등 숨김 코루틴을 정리하기 위한 참조입니다.

    public float WolfVisibleDuration => wolfVisible ? Time.time - wolfAppearedTime : 0f; // 늑대가 이 창문 앞에 서 있던 시간입니다.
    public Vector3 WolfSpawnPosition => wolfSpawnPoint != null ? wolfSpawnPoint.position : transform.position; // 늑대가 실내에 생성될 월드 좌표입니다.

    private void Awake()
    {
        windowRenderer = GetComponent<SpriteRenderer>();
        GetComponent<BoxCollider2D>().isTrigger = true;
        HideInteractionVisuals();
    }

    private void Update()
    {
        if (playerInside && defenseController != null && defenseController.CanUseFlashlight && Input.GetKeyDown(KeyCode.Space))
        {
            defenseController.ShineFlashlightAt(this);
        }
    }

    /// <summary>
    /// 창문 그림을 늑대 유무에 맞게 바꾸고 등장 시각을 기록합니다.
    /// </summary>
    public void SetWolfVisible(bool visible)
    {
        wolfVisible = visible;

        if (visible)
        {
            wolfAppearedTime = Time.time;
        }

        Sprite targetSprite = visible ? wolfWindowSprite : normalWindowSprite; // 현재 상태에 표시할 창문 이미지입니다.
        if (windowRenderer != null && targetSprite != null)
        {
            windowRenderer.sprite = targetSprite;
        }
    }

    /// <summary>
    /// 손전등 빛 오브젝트를 설정한 시간 동안 잠시 표시합니다.
    /// </summary>
    public void PlayFlashlightEffect()
    {
        if (flashlightVisual == null)
        {
            return;
        }

        if (flashlightCoroutine != null)
        {
            StopCoroutine(flashlightCoroutine);
        }

        flashlightCoroutine = StartCoroutine(ShowFlashlightBriefly());
    }

    public void HideInteractionVisuals()
    {
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }

        if (flashlightVisual != null)
        {
            flashlightVisual.SetActive(false);
        }
    }

    private IEnumerator ShowFlashlightBriefly()
    {
        flashlightVisual.SetActive(true);
        yield return new WaitForSeconds(flashlightVisualDuration);
        flashlightVisual.SetActive(false);
        flashlightCoroutine = null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        playerInside = true;
        if (interactionPrompt != null && defenseController != null && defenseController.CanUseFlashlight)
        {
            interactionPrompt.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        playerInside = false;
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
    }
}
