using UnityEngine;

/// <summary>현재 방에 따라 고정 화면 또는 경계 안의 플레이어 추적 화면을 적용합니다.</summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public enum RoomCameraMode // 방별 카메라 동작을 선택합니다.
    {
        Fixed,
        FollowPlayer
    }

    [System.Serializable]
    public class RoomCameraSetting // 목록에 원하는 개수만큼 추가하는 방별 카메라 설정입니다.
    {
        public string roomName = "복도"; // 플레이어 CurrentRoom과 일치하는 방 이름입니다.
        public RoomCameraMode mode = RoomCameraMode.Fixed; // Fixed는 고정, FollowPlayer는 추적입니다.
        public Vector2 fixedPosition; // 고정 모드의 월드 X/Y 좌표입니다.
        public Collider2D followBounds; // 추적 화면의 선택 사항인 경계입니다.
    }

    [Header("추적 대상")]
    [SerializeField] private Transform target; // 따라갈 플레이어 Transform입니다.
    [SerializeField] private PlayerGridMovement roomPlayer; // 현재 방 정보를 제공하며 Target에서 자동으로 찾습니다.
    [Header("카메라 이동 설정")]
    [SerializeField, Min(0f)] private float smoothTime = 0.15f; // 플레이어 추적에 사용하는 보간 시간입니다.
    [SerializeField] private Vector2 centerOffset = Vector2.zero; // 추적 중심의 X/Y 보정값입니다.
    [SerializeField] private bool snapToTargetOnStart = true; // 시작 시 기본 추적 화면을 즉시 맞춥니다.
    [Header("기본 카메라 이동 경계")]
    [SerializeField] private Collider2D cameraBounds; // 방 설정이 없을 때 사용하는 기본 추적 경계입니다.
    [Header("방별 카메라 설정")]
    [SerializeField] private RoomCameraSetting[] rooms = new RoomCameraSetting[0]; // Size 또는 +로 방을 원하는 만큼 추가합니다.

    private Camera sceneCamera; // 화면 크기 계산에 사용하는 카메라입니다.
    private float cameraZPosition; // 기존 카메라의 Z 위치를 유지합니다.
    private Vector3 followVelocity; // 부드러운 추적의 현재 속도입니다.
    private RoomCameraSetting activeRoom; // 현재 방에 해당하는 설정입니다.
    private string appliedRoom; // 마지막으로 적용한 방 이름입니다.

    private void Awake()
    {
        sceneCamera = GetComponent<Camera>();
        cameraZPosition = transform.position.z;
        if (roomPlayer == null && target != null)
            roomPlayer = target.GetComponentInParent<PlayerGridMovement>();
        if (target == null && roomPlayer != null) target = roomPlayer.transform;
    }

    private void Start()
    {
        ApplyCurrentRoom();
        if (target == null && !IsFixedRoom())
        {
            Debug.LogWarning("CameraFollow의 Target 또는 Room Player를 설정해 주세요.", this);
            return;
        }
        if (IsFixedRoom() || (target != null && snapToTargetOnStart))
            transform.position = GetTargetPosition();
    }

    private void LateUpdate()
    {
        ApplyCurrentRoom();
        if (IsFixedRoom())
        {
            transform.position = GetTargetPosition();
            return;
        }
        if (target == null) return;
        Vector3 targetPosition = GetTargetPosition(); // 현재 추적 대상의 경계를 반영한 카메라 위치입니다.
        if (smoothTime <= 0f)
            transform.position = targetPosition;
        else
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref followVelocity, smoothTime);
    }

    /// <summary>방이 바뀌면 추적 속도를 초기화하고 새 방의 화면으로 즉시 전환합니다.</summary>
    private void ApplyCurrentRoom()
    {
        if (roomPlayer == null || appliedRoom == roomPlayer.CurrentRoom) return;
        appliedRoom = roomPlayer.CurrentRoom;
        activeRoom = null;
        foreach (RoomCameraSetting room in rooms)
        {
            if (room != null && !string.IsNullOrWhiteSpace(room.roomName) && room.roomName.Trim() == appliedRoom)
            {
                activeRoom = room;
                break;
            }
        }
        followVelocity = Vector3.zero;
        if (IsFixedRoom() || target != null) transform.position = GetTargetPosition();
        if (activeRoom == null && rooms.Length > 0)
            Debug.LogWarning($"현재 방 '{appliedRoom}'의 설정이 없어 기본 추적을 사용합니다.", this);
    }

    private bool IsFixedRoom()
    {
        return activeRoom != null && activeRoom.mode == RoomCameraMode.Fixed;
    }

    /// <summary>고정 좌표 또는 플레이어를 중심으로 계산한 추적 좌표를 반환합니다.</summary>
    private Vector3 GetTargetPosition()
    {
        if (IsFixedRoom())
            return new Vector3(activeRoom.fixedPosition.x, activeRoom.fixedPosition.y, cameraZPosition);

        Vector3 targetPosition = new Vector3(target.position.x + centerOffset.x,
            target.position.y + centerOffset.y, cameraZPosition); // 플레이어 중심의 기본 추적 좌표입니다.
        Collider2D effectiveBounds = activeRoom != null ? activeRoom.followBounds : cameraBounds; // 현재 방의 경계 또는 기본 경계입니다.
        if (effectiveBounds == null || !sceneCamera.orthographic) return targetPosition;

        Bounds bounds = effectiveBounds.bounds; // 추적 화면을 제한하는 월드 범위입니다.
        float halfHeight = sceneCamera.orthographicSize; // 화면 높이의 절반입니다.
        float halfWidth = halfHeight * sceneCamera.aspect; // 화면 너비의 절반입니다.
        float minimumX = bounds.min.x + halfWidth; // 허용하는 최소 카메라 X 좌표입니다.
        float maximumX = bounds.max.x - halfWidth; // 허용하는 최대 카메라 X 좌표입니다.
        float minimumY = bounds.min.y + halfHeight; // 허용하는 최소 카메라 Y 좌표입니다.
        float maximumY = bounds.max.y - halfHeight; // 허용하는 최대 카메라 Y 좌표입니다.
        targetPosition.x = minimumX <= maximumX ? Mathf.Clamp(targetPosition.x, minimumX, maximumX) : bounds.center.x;
        targetPosition.y = minimumY <= maximumY ? Mathf.Clamp(targetPosition.y, minimumY, maximumY) : bounds.center.y;
        return targetPosition;
    }

    private void OnValidate()
    {
        for (int i = 0; i < rooms.Length; i++) // 이름이 비어 있거나 중복된 방 설정을 확인합니다.
        {
            if (rooms[i] == null || string.IsNullOrWhiteSpace(rooms[i].roomName))
            {
                Debug.LogWarning("카메라 방 이름은 비워 둘 수 없습니다.", this);
                continue;
            }
            for (int j = 0; j < i; j++) // 현재 항목보다 앞에 있는 방 이름과 비교합니다.
            {
                if (rooms[j] != null && rooms[j].roomName != null
                    && rooms[j].roomName.Trim() == rooms[i].roomName.Trim())
                {
                    Debug.LogWarning($"중복된 카메라 방 이름: {rooms[i].roomName}. 첫 번째 설정을 사용합니다.", this);
                    break;
                }
            }
        }
    }
}