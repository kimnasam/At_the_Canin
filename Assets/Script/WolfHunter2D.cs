using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 밤에 스폰된 늑대가 플레이어를 따라가고 접촉하면 게임 오버를 요청합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public class WolfHunter2D : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 5f; // 늑대가 플레이어를 추격하는 초당 이동 거리입니다.
    [SerializeField] private string playerTag = "Player"; // 추격하고 잡아야 할 플레이어의 태그입니다.
    [SerializeField] private string deathSceneName = "Death"; // 플레이어가 늑대에게 잡혔을 때 불러올 씬 이름입니다.
    [SerializeField] private string walkLeftStateName = "Wolf_Walk_Left"; // 왼쪽으로 이동할 때 재생할 Animator 상태 이름입니다.
    [SerializeField] private string walkRightStateName = "Wolf_Walk_Right"; // 오른쪽으로 이동할 때 재생할 Animator 상태 이름입니다.
    [SerializeField] private string walkUpStateName = "Wolf_Walk_Up"; // 위아래로 이동할 때 재생할 Animator 상태 이름입니다.
    [SerializeField] private string wallLayerName = "Wall"; // 늑대의 이동을 막을 벽 레이어 이름입니다.
    [SerializeField, Min(0f)] private float wallSkinWidth = 0.02f; // 늑대 Collider가 벽 안으로 파고들지 않게 남겨 둘 간격입니다.

    [Header("경로 탐색")]
    [SerializeField, Min(0.1f)] private float pathCellSize = 0.5f; // A* 경로 탐색에서 한 칸으로 사용할 월드 크기입니다.
    [SerializeField, Min(0.05f)] private float pathRefreshInterval = 0.25f; // 움직이는 플레이어를 따라 경로를 다시 계산하는 시간 간격입니다.
    [SerializeField, Min(0f)] private float pathWallClearance = 0.18f; // 경로의 각 지점이 Wall에서 유지해야 하는 최소 간격입니다.
    [SerializeField, Min(100)] private int maximumSearchNodes = 6000; // 한 번의 경로 탐색에서 검사할 최대 격자 칸 수입니다.

    [Header("현재 방과 출입구 추격")]
    [SerializeField] private string currentRoom = "복도"; // 스포너 또는 창문이 생성 직후 설정하는 늑대의 현재 방입니다.
    public string CurrentRoom => currentRoom; // 외부에서 확인하는 늑대의 방 이름입니다.
    private PlayerGridMovement playerMovement; // 추격 대상의 현재 방 정보를 제공합니다.
    private StageTeleportTrigger2D pursuitPortal; // 다른 방의 플레이어에게 가기 위한 다음 출입구입니다.
    private Vector2 pursuitTarget; // 같은 방이면 플레이어, 다른 방이면 출입구의 접근 좌표입니다.
    private string lastPlayerRoom; // 방 변경 시 출입구 경로를 즉시 다시 선택하기 위한 기록입니다.

    /// <summary>생성 직후 창문 또는 스포너에서 늑대의 시작 방을 설정합니다.</summary>
    public void SetCurrentRoom(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
        {
            Debug.LogWarning("늑대의 생성 방 이름을 설정해 주세요.", this);
            return;
        }
        currentRoom = roomName.Trim();
        pursuitPortal = null;
        currentPath.Clear();
        currentPathIndex = 0;
        hasPathToTarget = false;
        nextPathRefreshTime = 0f;
    }

    /// <summary>방 연결을 너비 우선으로 탐색하여 플레이어 방으로 향하는 첫 출입구를 반환합니다.</summary>
    private StageTeleportTrigger2D FindNextPortal(string targetRoom)
    {
        List<StageTeleportTrigger2D> portals = new List<StageTeleportTrigger2D>(); // 사용 가능한 같은 씬의 출입구 목록입니다.
        foreach (StageTeleportTrigger2D portal in StageTeleportTrigger2D.ActivePortals)
        {
            if (portal != null && portal.gameObject.scene == gameObject.scene && portal.CanWolfTravel)
                portals.Add(portal);
        }
        portals.Sort((a, b) => Vector2.SqrMagnitude(a.WolfApproachPosition - wolfBody.position)
            .CompareTo(Vector2.SqrMagnitude(b.WolfApproachPosition - wolfBody.position)));

        Queue<string> pendingRooms = new Queue<string>(); // 아직 연결을 검사하지 않은 방입니다.
        Dictionary<string, StageTeleportTrigger2D> firstPortals = new Dictionary<string, StageTeleportTrigger2D>(); // 각 방까지 가는 첫 출입구입니다.
        pendingRooms.Enqueue(currentRoom);
        firstPortals.Add(currentRoom, null);
        while (pendingRooms.Count > 0)
        {
            string room = pendingRooms.Dequeue(); // 이번에 연결을 탐색할 방입니다.
            foreach (StageTeleportTrigger2D portal in portals)
            {
                if (portal.SourceRoom != room || firstPortals.ContainsKey(portal.DestinationRoom))
                    continue;
                StageTeleportTrigger2D firstPortal = firstPortals[room] ?? portal; // 시작 방에서 이용해야 할 출입구를 유지합니다.
                if (portal.DestinationRoom == targetRoom) return firstPortal;
                firstPortals.Add(portal.DestinationRoom, firstPortal);
                pendingRooms.Enqueue(portal.DestinationRoom);
            }
        }
        return null;
    }

    /// <summary>다른 방에서는 출입구를, 같은 방에서는 플레이어를 추격 목표로 정합니다.</summary>
    private bool UpdatePursuitTarget()
    {
        if (playerMovement == null || currentRoom == playerMovement.CurrentRoom)
        {
            pursuitPortal = null;
            pursuitTarget = player.position;
            return true;
        }
        pursuitPortal = FindNextPortal(playerMovement.CurrentRoom);
        if (pursuitPortal == null)
        {
            currentPath.Clear();
            currentPathIndex = 0;
            hasPathToTarget = false;
            return false;
        }
        pursuitTarget = pursuitPortal.WolfApproachPosition;
        return true;
    }

    /// <summary>출입구에 도착하면 플레이어와 동일한 좌표로 이동하고 다음 방의 경로를 새로 찾습니다.</summary>
    private bool TryTravelThroughPortal()
    {
        bool reachedPortalPathEnd = hasPathToTarget && currentPathIndex >= currentPath.Count;
        if (pursuitPortal == null || pursuitPortal.SourceRoom != currentRoom
            || (!reachedPortalPathEnd && !pursuitPortal.CanWolfEnter(wolfCollider, pathCellSize * 0.5f)))
            return false;

        Vector2 destination = pursuitPortal.DestinationPosition; // 플레이어와 공유하는 도착 좌표입니다.
        string destinationRoom = pursuitPortal.DestinationRoom; // 이동한 뒤 늑대가 있는 방입니다.
        wolfBody.velocity = Vector2.zero;
        wolfBody.angularVelocity = 0f;
        wolfBody.position = destination;
        transform.position = new Vector3(destination.x, destination.y, transform.position.z);
        Physics2D.SyncTransforms();
        isLeavingSpawnWall = false;
        SetCurrentRoom(destinationRoom);
        return true;
    }
    private Rigidbody2D wolfBody; // 물리 이동에 사용하는 늑대 Rigidbody2D입니다.
    private Collider2D wolfCollider; // 벽과 플레이어의 충돌을 감지하는 늑대 Collider2D입니다.
    private Animator wolfAnimator; // 이동 방향에 맞는 걷기 애니메이션을 재생하는 Animator입니다.
    private Transform player; // 현재 추격할 플레이어 Transform입니다.
    private string currentAnimationState; // 매 프레임 같은 애니메이션을 다시 시작하지 않도록 현재 상태를 저장합니다.
    private bool isLoadingDeathScene; // 접촉 판정이 겹쳐 Death 씬을 여러 번 불러오는 것을 막습니다.
    private ContactFilter2D wallContactFilter; // Wall 레이어만 이동 장애물로 검사하는 충돌 필터입니다.
    private readonly RaycastHit2D[] wallCastResults = new RaycastHit2D[8]; // 이동 경로에서 발견한 벽 충돌 결과를 저장하는 배열입니다.
    private readonly Collider2D[] overlappingWalls = new Collider2D[8]; // 스폰 순간 늑대와 겹쳐 있는 Wall Collider를 저장하는 배열입니다.
    private bool isLeavingSpawnWall = true; // 벽 안에서 스폰된 늑대가 먼저 벽 밖으로 빠져나오도록 허용하는 값입니다.
    private readonly List<Vector2> currentPath = new List<Vector2>(); // A* 탐색으로 계산한 현재 이동 경로입니다.
    private int currentPathIndex; // 현재 늑대가 향하고 있는 경로 지점 번호입니다.
    private bool hasPathToTarget; // 현재 경로가 목표의 가장 가까운 안전 격자까지 완성되었는지 나타냅니다.
    private float nextPathRefreshTime; // 다음 경로 재계산이 가능한 물리 시간입니다.
    private int wallLayerMask; // Wall 레이어만 포함하는 물리 검사 비트 마스크입니다.

    /// <summary>
    /// A* 탐색 중 한 격자 칸의 비용과 부모 칸을 저장합니다.
    /// </summary>
    private sealed class PathNode
    {
        public Vector2Int GridPosition; // 이 노드가 나타내는 격자 좌표입니다.
        public int TravelCost; // 출발점부터 이 노드까지 이동한 비용입니다.
        public int EstimatedTotalCost; // 이동 비용과 목적지 예상 비용을 합한 값입니다.
        public PathNode Parent; // 최종 경로를 역추적할 때 사용할 이전 노드입니다.
    }

    private void Awake()
    {
        wolfBody = GetComponent<Rigidbody2D>();
        wolfCollider = GetComponent<Collider2D>();
        if (wolfCollider == null)
        {
            wolfCollider = gameObject.AddComponent<CapsuleCollider2D>(); // 기존 Prefab에 Collider가 없어도 실행 중 충돌 판정을 가능하게 합니다.
        }
        wolfAnimator = GetComponent<Animator>();

        // CameraBounds 같은 다른 Collider에 밀리지 않고 코드로 계산한 위치만 따라가도록 설정합니다.
        wolfBody.bodyType = RigidbodyType2D.Kinematic;
        wolfBody.gravityScale = 0f;
        wolfBody.freezeRotation = true;
        wolfBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        wolfBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        wolfCollider.isTrigger = true;

        wallLayerMask = LayerMask.GetMask(wallLayerName);
        wallContactFilter = new ContactFilter2D();
        wallContactFilter.SetLayerMask(wallLayerMask);
        wallContactFilter.useLayerMask = true;
        wallContactFilter.useTriggers = false;

        if (wallLayerMask == 0)
        {
            Debug.LogError($"'{wallLayerName}' 레이어를 찾을 수 없습니다.", this);
        }
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag); // Player 태그로 찾은 추격 대상입니다.
        if (playerObject != null)
        {
            player = playerObject.transform;
            playerMovement = playerObject.GetComponent<PlayerGridMovement>();
        }
    }

    private void FixedUpdate()
    {
        if (player == null || isLoadingDeathScene) return;

        string playerRoom = playerMovement != null ? playerMovement.CurrentRoom : ""; // 플레이어의 방 변경 여부를 확인하는 이름입니다.
        if (playerRoom != lastPlayerRoom)
        {
            lastPlayerRoom = playerRoom;
            pursuitPortal = null;
            currentPath.Clear();
            hasPathToTarget = false;
            nextPathRefreshTime = 0f;
        }

        if (Time.fixedTime >= nextPathRefreshTime
            || (pursuitPortal != null && !pursuitPortal.CanWolfTravel))
        {
            nextPathRefreshTime = Time.fixedTime + pathRefreshInterval;
            if (!UpdatePursuitTarget()) return;
            RebuildPath();
        }

        if (TryTravelThroughPortal()) return;
        if (currentPathIndex >= currentPath.Count) return;

        Vector2 targetPoint = currentPath[currentPathIndex]; // 지금 걸어갈 경로 지점입니다.
        Vector2 directionToPoint = targetPoint - wolfBody.position; // 경로 지점을 향한 방향입니다.
        float arrivalDistance = pursuitPortal != null && currentPathIndex == currentPath.Count - 1
            ? 0.01f : pathCellSize * 0.2f; // 마지막 출입구 지점은 작은 Trigger에도 들어가도록 가까이 접근합니다.
        if (directionToPoint.magnitude <= arrivalDistance)
        {
            currentPathIndex++;
            return;
        }

        Vector2 moveDirection = directionToPoint.normalized; // 이번 물리 프레임의 이동 방향입니다.
        float requestedDistance = Mathf.Min(moveSpeed * Time.fixedDeltaTime, directionToPoint.magnitude); // 이동하려는 거리입니다.
        float allowedDistance = GetWallLimitedDistance(moveDirection, requestedDistance); // 벽을 고려한 실제 이동 거리입니다.
        wolfBody.MovePosition(wolfBody.position + moveDirection * allowedDistance);
        PlayWalkAnimation(moveDirection);
    }
    /// <summary>
    /// 현재 늑대 위치에서 플레이어까지 Wall을 피하는 최단 격자 경로를 다시 계산합니다.
    /// </summary>
    private void RebuildPath()
    {
        currentPath.Clear();
        currentPathIndex = 0;
        hasPathToTarget = false;
        Vector2Int startGrid = FindNearestWalkableGrid(WorldToGrid(wolfBody.position)); // 늑대와 가장 가까운 이동 가능 격자입니다.
        Vector2Int goalGrid = FindNearestWalkableGrid(WorldToGrid(pursuitTarget)); // 플레이어 또는 출입구와 가장 가까운 이동 가능 격자입니다.
        List<PathNode> openNodes = new List<PathNode>(); // 아직 검사할 후보 노드 목록입니다.
        Dictionary<Vector2Int, PathNode> knownNodes = new Dictionary<Vector2Int, PathNode>(); // 이미 발견한 모든 노드입니다.
        HashSet<Vector2Int> closedNodes = new HashSet<Vector2Int>(); // 검사가 끝난 노드 좌표입니다.
        PathNode startNode = new PathNode
        {
            GridPosition = startGrid,
            TravelCost = 0,
            EstimatedTotalCost = GetGridDistance(startGrid, goalGrid)
        };

        openNodes.Add(startNode);
        knownNodes.Add(startGrid, startNode);
        PathNode goalNode = null; // 목적지에 도달했을 때 경로를 역추적할 마지막 노드입니다.
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left }; // 상하좌우 네 이동 방향입니다.

        while (openNodes.Count > 0 && closedNodes.Count < maximumSearchNodes)
        {
            PathNode currentNode = RemoveLowestCostNode(openNodes);

            bool reachedGoal = pursuitPortal != null
                ? pursuitPortal.IsWolfApproachPosition(GridToWorld(currentNode.GridPosition), pathCellSize)
                : currentNode.GridPosition == goalGrid;

            if (reachedGoal)
            {
                goalNode = currentNode;
                break;
            }

            closedNodes.Add(currentNode.GridPosition);

            foreach (Vector2Int direction in directions)
            {
                Vector2Int neighborGrid = currentNode.GridPosition + direction; // 현재 노드와 인접한 다음 격자 좌표입니다.

                if (closedNodes.Contains(neighborGrid) || !IsGridWalkable(neighborGrid))
                {
                    continue;
                }

                int newTravelCost = currentNode.TravelCost + 10; // 상하좌우 한 칸을 이동한 새로운 누적 비용입니다.
                PathNode neighborNode;

                if (!knownNodes.TryGetValue(neighborGrid, out neighborNode))
                {
                    neighborNode = new PathNode { GridPosition = neighborGrid, TravelCost = int.MaxValue };
                    knownNodes.Add(neighborGrid, neighborNode);
                    openNodes.Add(neighborNode);
                }

                if (newTravelCost >= neighborNode.TravelCost)
                {
                    continue;
                }

                neighborNode.TravelCost = newTravelCost;
                neighborNode.EstimatedTotalCost = newTravelCost + GetGridDistance(neighborGrid, goalGrid);
                neighborNode.Parent = currentNode;
            }
        }

        if (goalNode != null)
        {
            StorePath(goalNode);
            hasPathToTarget = true;
        }
    }

    /// <summary>
    /// 후보 목록에서 예상 총비용이 가장 낮은 노드를 꺼냅니다.
    /// </summary>
    private PathNode RemoveLowestCostNode(List<PathNode> nodes)
    {
        int lowestIndex = 0; // 가장 비용이 낮은 노드의 목록 번호입니다.

        for (int index = 1; index < nodes.Count; index++)
        {
            if (nodes[index].EstimatedTotalCost < nodes[lowestIndex].EstimatedTotalCost)
            {
                lowestIndex = index;
            }
        }

        PathNode lowestNode = nodes[lowestIndex];
        nodes.RemoveAt(lowestIndex);
        return lowestNode;
    }

    /// <summary>
    /// 목적지 노드의 부모를 역추적하여 실제 월드 이동 경로로 저장합니다.
    /// </summary>
    private void StorePath(PathNode goalNode)
    {
        currentPath.Clear();
        PathNode pathNode = goalNode; // 목적지부터 출발점 방향으로 역추적 중인 노드입니다.

        while (pathNode.Parent != null)
        {
            currentPath.Add(GridToWorld(pathNode.GridPosition));
            pathNode = pathNode.Parent;
        }

        currentPath.Reverse();
        currentPathIndex = 0;
    }

    /// <summary>
    /// 지정한 격자 주변에서 Wall과 겹치지 않는 가장 가까운 칸을 찾습니다.
    /// </summary>
    private Vector2Int FindNearestWalkableGrid(Vector2Int centerGrid)
    {
        if (IsGridWalkable(centerGrid))
        {
            return centerGrid;
        }

        for (int radius = 1; radius <= 8; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius)
                    {
                        continue;
                    }

                    Vector2Int candidateGrid = centerGrid + new Vector2Int(x, y); // 벽 밖인지 검사할 주변 격자입니다.
                    if (IsGridWalkable(candidateGrid))
                    {
                        return candidateGrid;
                    }
                }
            }
        }

        return centerGrid;
    }

    private bool IsGridWalkable(Vector2Int gridPosition)
    {
        return Physics2D.OverlapCircle(GridToWorld(gridPosition), pathWallClearance, wallLayerMask) == null;
    }

    private Vector2Int WorldToGrid(Vector2 worldPosition)
    {
        return new Vector2Int(Mathf.RoundToInt(worldPosition.x / pathCellSize), Mathf.RoundToInt(worldPosition.y / pathCellSize));
    }

    private Vector2 GridToWorld(Vector2Int gridPosition)
    {
        return new Vector2(gridPosition.x * pathCellSize, gridPosition.y * pathCellSize);
    }

    private int GetGridDistance(Vector2Int first, Vector2Int second)
    {
        return (Mathf.Abs(first.x - second.x) + Mathf.Abs(first.y - second.y)) * 10;
    }

    /// <summary>
    /// 이동 경로에서 Wall 레이어만 검사하여 벽을 통과하지 않는 거리를 계산합니다.
    /// </summary>
    private float GetWallLimitedDistance(Vector2 moveDirection, float requestedDistance)
    {
        if (requestedDistance <= 0f)
        {
            return 0f;
        }

        if (isLeavingSpawnWall)
        {
            int overlappingWallCount = wolfCollider.OverlapCollider(wallContactFilter, overlappingWalls); // 현재 늑대와 겹친 Wall Collider 개수입니다.

            if (overlappingWallCount > 0)
            {
                return requestedDistance;
            }

            // 최초 스폰 위치의 벽을 완전히 빠져나온 뒤부터는 다시 Wall 통과를 허용하지 않습니다.
            isLeavingSpawnWall = false;
        }

        int hitCount = wolfCollider.Cast(moveDirection, wallContactFilter, wallCastResults, requestedDistance + wallSkinWidth); // 이동 방향에 있는 Wall 충돌 개수입니다.
        float allowedDistance = requestedDistance; // 벽이 없을 때 사용할 원래 이동 거리입니다.

        for (int index = 0; index < hitCount; index++)
        {
            float distanceBeforeWall = Mathf.Max(0f, wallCastResults[index].distance - wallSkinWidth); // 벽과 간격을 남긴 안전한 이동 거리입니다.
            allowedDistance = Mathf.Min(allowedDistance, distanceBeforeWall);
        }

        return allowedDistance;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryCatchPlayer(collision.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryCatchPlayer(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCatchPlayer(other.gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryCatchPlayer(other.gameObject);
    }

    private void TryCatchPlayer(GameObject target)
    {
        if (isLoadingDeathScene || player == null
            || (playerMovement != null && currentRoom != playerMovement.CurrentRoom))
        {
            return;
        }

        Transform targetTransform = target.transform; // 늑대와 접촉한 Collider가 속한 Transform입니다.
        bool touchedPlayer = targetTransform == player
            || targetTransform.IsChildOf(player)
            || player.IsChildOf(targetTransform); // 플레이어 또는 플레이어의 자식 Collider인지 확인한 결과입니다.

        if (!touchedPlayer)
        {
            return;
        }

        isLoadingDeathScene = true;
        SceneManager.LoadScene(deathSceneName);
    }

    /// <summary>
    /// 이동 방향에서 가장 큰 축을 기준으로 방향별 걷기 애니메이션을 재생합니다.
    /// </summary>
    private void PlayWalkAnimation(Vector2 moveDirection)
    {
        if (wolfAnimator == null)
        {
            return;
        }

        string nextAnimationState; // 이번 이동 방향에 맞춰 재생할 Animator 상태 이름입니다.

        if (Mathf.Abs(moveDirection.x) > Mathf.Abs(moveDirection.y))
        {
            nextAnimationState = moveDirection.x < 0f ? walkLeftStateName : walkRightStateName;
        }
        else
        {
            // 아래쪽 전용 클립이 없으므로 세로 이동에는 위쪽 걷기 클립을 함께 사용합니다.
            nextAnimationState = walkUpStateName;
        }

        if (currentAnimationState == nextAnimationState)
        {
            return;
        }

        currentAnimationState = nextAnimationState;
        wolfAnimator.Play(currentAnimationState);
    }
}
