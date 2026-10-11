using UnityEngine;

/// <summary>
/// 카메라가 지정한 캐릭터를 화면 중심에 두고 부드럽게 따라가도록 만드는 클래스입니다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("추적 대상")]
    [SerializeField] private Transform target; // 카메라가 중심에 두고 따라갈 캐릭터의 Transform입니다.

    [Header("카메라 이동 설정")]
    [SerializeField, Min(0f)] private float smoothTime = 0.15f; // 카메라가 캐릭터 위치까지 부드럽게 따라가는 데 사용하는 시간입니다.
    [SerializeField] private Vector2 centerOffset = Vector2.zero; // 캐릭터를 화면 중심에서 추가로 이동시킬 X, Y 보정 거리입니다.
    [SerializeField] private bool snapToTargetOnStart = true; // 게임 시작 시 카메라를 캐릭터 중심으로 즉시 이동시킬지 결정합니다.

    [Header("카메라 이동 경계")]
    [SerializeField] private Collider2D cameraBounds; // 카메라 화면이 밖으로 넘어가지 않도록 제한할 맵 경계 Collider2D입니다.

    private Camera sceneCamera; // 화면 크기를 계산할 현재 오브젝트의 Camera 컴포넌트입니다.
    private float cameraZPosition; // 2D 카메라의 앞뒤 위치가 바뀌지 않도록 저장하는 최초 Z 좌표입니다.
    private Vector3 followVelocity; // SmoothDamp가 부드러운 카메라 이동 속도를 계산할 때 사용하는 값입니다.

    /// <summary>
    /// 카메라 컴포넌트와 최초 Z 좌표를 저장합니다.
    /// </summary>
    private void Awake()
    {
        sceneCamera = GetComponent<Camera>();
        cameraZPosition = transform.position.z;
    }

    /// <summary>
    /// 게임 시작 시 필요하면 카메라를 캐릭터 중심과 맵 경계 안으로 즉시 이동합니다.
    /// </summary>
    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("카메라가 따라갈 Target 캐릭터가 설정되지 않았습니다.", this);
            return;
        }

        if (snapToTargetOnStart)
        {
            transform.position = GetTargetPosition();
        }
    }

    /// <summary>
    /// 캐릭터 이동이 끝난 뒤 카메라가 캐릭터 중심을 따라가도록 위치를 갱신합니다.
    /// </summary>
    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 targetPosition = GetTargetPosition(); // 현재 캐릭터를 중심에 놓기 위한 카메라의 목표 좌표입니다.

        if (smoothTime <= 0f)
        {
            transform.position = targetPosition;
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref followVelocity,
            smoothTime);
    }

    /// <summary>
    /// 캐릭터의 X, Y 위치와 카메라의 고정된 Z 위치를 조합해 목표 좌표를 반환합니다.
    /// </summary>
    private Vector3 GetTargetPosition()
    {
        Vector3 targetPosition = new Vector3(
            target.position.x + centerOffset.x,
            target.position.y + centerOffset.y,
            cameraZPosition); // 캐릭터를 중심으로 계산한 제한 전 카메라 목표 좌표입니다.

        if (cameraBounds == null || !sceneCamera.orthographic)
        {
            return targetPosition;
        }

        Bounds bounds = cameraBounds.bounds; // Inspector에서 지정한 맵 경계의 월드 좌표 범위입니다.
        float cameraHalfHeight = sceneCamera.orthographicSize; // 카메라 화면 높이의 절반에 해당하는 월드 거리입니다.
        float cameraHalfWidth = cameraHalfHeight * sceneCamera.aspect; // 카메라 화면 너비의 절반에 해당하는 월드 거리입니다.
        float minimumX = bounds.min.x + cameraHalfWidth; // 화면 왼쪽이 경계를 넘지 않는 카메라의 최소 X 좌표입니다.
        float maximumX = bounds.max.x - cameraHalfWidth; // 화면 오른쪽이 경계를 넘지 않는 카메라의 최대 X 좌표입니다.
        float minimumY = bounds.min.y + cameraHalfHeight; // 화면 아래쪽이 경계를 넘지 않는 카메라의 최소 Y 좌표입니다.
        float maximumY = bounds.max.y - cameraHalfHeight; // 화면 위쪽이 경계를 넘지 않는 카메라의 최대 Y 좌표입니다.

        targetPosition.x = minimumX <= maximumX
            ? Mathf.Clamp(targetPosition.x, minimumX, maximumX)
            : bounds.center.x;
        targetPosition.y = minimumY <= maximumY
            ? Mathf.Clamp(targetPosition.y, minimumY, maximumY)
            : bounds.center.y;

        return targetPosition;
    }
}
