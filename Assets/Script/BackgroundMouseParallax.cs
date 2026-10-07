using UnityEngine;

/// <summary>
/// 마우스 위치에 따라 배경을 제한된 범위 안에서 부드럽게 움직이는 클래스입니다.
/// </summary>
public class BackgroundMouseParallax : MonoBehaviour
{
    [Header("이동 범위")]
    [SerializeField, Min(0f)] private float horizontalRange = 30f; // 처음 위치를 기준으로 좌우로 움직일 수 있는 최대 거리입니다.
    [SerializeField, Min(0f)] private float verticalRange = 20f; // 처음 위치를 기준으로 위아래로 움직일 수 있는 최대 거리입니다.

    [Header("움직임 설정")]
    [SerializeField, Min(0.01f)] private float smoothTime = 0.2f; // 배경이 목표 위치까지 부드럽게 이동하는 데 사용하는 시간입니다.
    [SerializeField] private bool reverseMovement = false; // 마우스와 반대 방향으로 배경을 움직일지 결정합니다.

    private Vector3 centerPosition; // 배경 이동 범위의 중심이 되는 최초 로컬 위치입니다.
    private Vector3 moveVelocity; // SmoothDamp가 부드러운 이동 속도를 계산할 때 사용하는 값입니다.

    /// <summary>
    /// 씬이 시작될 때 현재 배경 위치를 이동 범위의 중심으로 저장합니다.
    /// </summary>
    private void Awake()
    {
        centerPosition = transform.localPosition;
    }

    /// <summary>
    /// 마우스 위치를 화면 중앙 기준의 비율로 바꿔 배경의 목표 위치를 계산합니다.
    /// </summary>
    private void Update()
    {
        if (Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        float normalizedMouseX = Mathf.Clamp((Input.mousePosition.x / Screen.width - 0.5f) * 2f, -1f, 1f); // 화면 중앙 기준 마우스의 가로 위치 비율입니다.
        float normalizedMouseY = Mathf.Clamp((Input.mousePosition.y / Screen.height - 0.5f) * 2f, -1f, 1f); // 화면 중앙 기준 마우스의 세로 위치 비율입니다.
        float direction = reverseMovement ? -1f : 1f; // 배경이 마우스를 따라갈 방향을 결정하는 값입니다.

        Vector3 targetPosition = centerPosition + new Vector3(
            normalizedMouseX * horizontalRange * direction,
            normalizedMouseY * verticalRange * direction,
            0f); // 현재 마우스 위치에 대응하는 배경의 목표 로컬 위치입니다.

        transform.localPosition = Vector3.SmoothDamp(
            transform.localPosition,
            targetPosition,
            ref moveVelocity,
            smoothTime);
    }
}
