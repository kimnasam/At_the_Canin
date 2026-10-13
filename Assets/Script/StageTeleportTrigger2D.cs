using System.Collections.Generic;
using UnityEngine;

/// <summary>지정한 Trigger 안에서 방향키를 누르면 플레이어의 위치와 방을 변경합니다.</summary>
[RequireComponent(typeof(Collider2D))]
public class StageTeleportTrigger2D : MonoBehaviour
{
    public enum EntryDirection // 순간이동을 실행할 입력 방향입니다.
    {
        Up,
        Down,
        Left,
        Right
    }

    [SerializeField] private PlayerGridMovement player; // 감지할 플레이어이며 비워 두면 씬에서 자동으로 찾습니다.
    [SerializeField] private EntryDirection requiredDirection = EntryDirection.Up; // 작동 방향이며 동일 방향 WASD도 지원합니다.
    [SerializeField] private Vector2 destinationPosition; // 도착할 월드 X/Y 좌표입니다.
    [SerializeField] private string destinationRoom = "복도"; // CameraFollow 목록과 일치하는 도착 방 이름입니다.
    private Collider2D triggerArea; // 플레이어와 겹치는지 확인할 Trigger 영역입니다.
    private Collider2D[] playerColliders; // 자식 오브젝트까지 포함한 플레이어 콜라이더입니다.

    private bool hasTeleportedDuringOverlap; // 같은 영역에 머무는 동안 순간이동이 반복되지 않게 합니다.

    [Header("늑대의 방 이동")]
    [SerializeField] private string sourceRoom; // 이 출입구가 설치된 출발 방 이름입니다.
    [SerializeField] private bool allowWolfTravel = true; // 늑대가 이 출입구를 이용할 수 있는지 설정합니다.
    [SerializeField] private Transform wolfApproachPoint; // 선택 사항이며 Trigger 안의 벽 없는 접근 지점을 지정합니다.
    public string SourceRoom => string.IsNullOrWhiteSpace(sourceRoom) ? "" : sourceRoom.Trim(); // 방 연결 탐색에 사용하는 출발 방입니다.
    public string DestinationRoom => string.IsNullOrWhiteSpace(destinationRoom) ? "" : destinationRoom.Trim(); // 방 연결 탐색에 사용하는 도착 방입니다.
    public Vector2 DestinationPosition => destinationPosition; // 플레이어와 늑대가 공유하는 도착 좌표입니다.
    public Vector2 WolfApproachPosition => wolfApproachPoint != null ? (Vector2)wolfApproachPoint.position : (Vector2)GetComponent<Collider2D>().bounds.center; // 늑대가 걸어갈 출입구 위치입니다.
    public bool CanWolfTravel => isActiveAndEnabled && allowWolfTravel && GetComponent<Collider2D>().enabled
        && SourceRoom.Length > 0 && DestinationRoom.Length > 0; // 유효하고 활성화된 연결만 탐색합니다.
    public static readonly HashSet<StageTeleportTrigger2D> ActivePortals = new HashSet<StageTeleportTrigger2D>(); // 현재 활성화된 씬 출입구 목록입니다.

    private void OnEnable()
    {
        ActivePortals.Add(this);
    }

    /// <summary>늑대가 선택한 출입구의 Trigger 영역에 실제 도착했는지 검사합니다.</summary>
    public bool ContainsWolf(Collider2D wolfCollider)
    {
        return CanWolfTravel && wolfCollider != null && wolfCollider.enabled
            && GetComponent<Collider2D>().Distance(wolfCollider).isOverlapped;
    }
    private void Awake()
    {
        triggerArea = GetComponent<Collider2D>();
        triggerArea.isTrigger = true;
        if (player == null) player = FindObjectOfType<PlayerGridMovement>();
        if (player != null) playerColliders = player.GetComponentsInChildren<Collider2D>(true);
        if (string.IsNullOrWhiteSpace(destinationRoom))
            Debug.LogWarning("순간이동의 Destination Room을 설정해 주세요.", this);
    }

    /// <summary>물리 프레임 사이의 키 입력을 놓치지 않도록 Update에서 실제 겹침을 확인합니다.</summary>
    private void Update()
    {
        if (!triggerArea.enabled || player == null || !player.isActiveAndEnabled)
        {
            hasTeleportedDuringOverlap = false;
            return;
        }

        bool isPlayerInside = false; // 하나 이상의 플레이어 콜라이더가 영역과 겹치는지 나타냅니다.
        foreach (Collider2D playerCollider in playerColliders)
        {
            if (playerCollider != null && playerCollider.enabled
                && playerCollider.gameObject.activeInHierarchy
                && triggerArea.Distance(playerCollider).isOverlapped)
            {
                isPlayerInside = true;
                break;
            }
        }

        if (!isPlayerInside)
        {
            hasTeleportedDuringOverlap = false;
            return;
        }

        if (!hasTeleportedDuringOverlap && Time.timeScale > 0f && IsDirectionHeld())
            hasTeleportedDuringOverlap = player.TryTeleport(destinationPosition, destinationRoom);
    }

    private void OnDisable()
    {
        ActivePortals.Remove(this);
        hasTeleportedDuringOverlap = false;
    }
    private bool IsDirectionHeld()
    {
        switch (requiredDirection)
        {
            case EntryDirection.Up:
                return Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
            case EntryDirection.Down:
                return Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
            case EntryDirection.Left:
                return Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
            default:
                return Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(new Vector3(destinationPosition.x, destinationPosition.y, transform.position.z), 0.25f);
    }
}