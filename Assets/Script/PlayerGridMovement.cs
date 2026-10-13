using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// 방향키 또는 WASD를 누르면 플레이어를 한 칸 이동시키고, 계속 누르면 칸 단위로 연속 이동시키는 클래스입니다.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(AudioSource))]
public class PlayerGridMovement : MonoBehaviour
{
    [Header("현재 방")]
    [SerializeField] private string currentRoom = "복도"; // 시작 시 플레이어가 있는 방 이름입니다.
    public string CurrentRoom => currentRoom; // 외부에서 읽을 수 있는 현재 방 이름입니다.
    public event System.Action<string> RoomChanged; // 방이 변경될 때 구독자에게 알립니다.
    private int teleportFrame = -1; // 순간이동한 프레임의 일반 이동 입력을 막습니다.

    /// <summary>현재 방 이름을 설정하고 변경 이벤트를 전달합니다.</summary>
    public void SetCurrentRoom(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
        {
            Debug.LogWarning("방 이름은 비워 둘 수 없습니다.", this);
            return;
        }
        string normalizedRoom = roomName.Trim(); // 앞뒤 공백을 제거한 방 이름입니다.
        if (currentRoom == normalizedRoom) return;
        currentRoom = normalizedRoom;
        RoomChanged?.Invoke(currentRoom);
    }

    /// <summary>격자 이동을 중단하고 같은 씬의 지정 좌표와 방으로 순간이동합니다.</summary>
    public bool TryTeleport(Vector2 destination, string roomName)
    {
        if (!isActiveAndEnabled || !acceptsPlayerInput || Time.timeScale <= 0f
            || teleportFrame == Time.frameCount || string.IsNullOrWhiteSpace(roomName))
            return false;

        StopAllCoroutines();
        isMoving = false;
        activeInputDirection = Vector2.zero;
        teleportFrame = Time.frameCount;
        Vector3 destinationPosition = new Vector3(destination.x, destination.y, transform.position.z); // 기존 Z를 유지하는 도착 좌표입니다.
        Rigidbody2D playerBody = GetComponent<Rigidbody2D>(); // 물리 위치도 함께 변경할 플레이어 본체입니다.
        if (playerBody != null)
        {
            playerBody.velocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
            playerBody.position = destination;
        }
        transform.position = destinationPosition;
        Physics2D.SyncTransforms();
        StopWalkingSound();
        PlayIdleAnimation();
        SetCurrentRoom(roomName);
        return true;
    }
    private enum FacingDirection // 플레이어가 현재 바라보는 네 방향을 구분하는 열거형입니다.
    {
        Down,
        Left,
        Right,
        Up
    }

    [Header("격자 이동 설정")]
    [SerializeField, Min(0.01f)] private float tileSize = 0.5f; // 입력 한 번에 이동하는 한 칸의 월드 좌표 거리입니다.
    [SerializeField, Min(0.01f)] private float moveSpeed = 2f; // 한 칸을 이동할 때 사용하는 초당 이동 거리입니다.

    [Header("벽 충돌 설정")]
    [SerializeField] private LayerMask obstacleLayers; // 플레이어가 통과할 수 없는 벽 오브젝트의 레이어입니다.
    [SerializeField] private Vector2 collisionCheckSize = new Vector2(0.4f, 0.4f); // 다음 칸에 벽이 있는지 검사할 사각형의 크기입니다.

    [Header("시작 방향")]
    [SerializeField] private FacingDirection startDirection = FacingDirection.Down; // 게임 시작 시 플레이어가 바라볼 방향입니다.

    [Header("걷기 애니메이션")]
    [SerializeField] private AnimationClip walkDown; // 아래쪽으로 이동할 때 반복 재생할 걷기 애니메이션입니다.
    [SerializeField] private AnimationClip walkLeft; // 왼쪽으로 이동할 때 반복 재생할 걷기 애니메이션입니다.
    [SerializeField] private AnimationClip walkRight; // 오른쪽으로 이동할 때 반복 재생할 걷기 애니메이션입니다.
    [SerializeField] private AnimationClip walkUp; // 위쪽으로 이동할 때 반복 재생할 걷기 애니메이션입니다.

    [Header("대기 애니메이션")]
    [SerializeField] private AnimationClip idleDown; // 아래쪽을 바라보며 멈췄을 때 재생할 대기 애니메이션입니다.
    [SerializeField] private AnimationClip idleLeft; // 왼쪽을 바라보며 멈췄을 때 재생할 대기 애니메이션입니다.
    [SerializeField] private AnimationClip idleRight; // 오른쪽을 바라보며 멈췄을 때 재생할 대기 애니메이션입니다.
    [SerializeField] private AnimationClip idleUp; // 위쪽을 바라보며 멈췄을 때 재생할 대기 애니메이션입니다.

    [Header("걷기 소리")]
    [SerializeField] private AudioClip walkingSound; // 캐릭터가 실제로 이동하는 동안 반복 재생할 발소리 파일입니다.
    [SerializeField, Range(0f, 1f)] private float walkingSoundVolume = 1f; // 발소리의 재생 음량입니다.

    private Animator playerAnimator; // 방향별 걷기 및 대기 애니메이션을 재생하는 Animator입니다.
    private AudioSource walkingAudioSource; // 걷는 동안 발소리를 반복 재생하는 AudioSource입니다.
    private bool isMoving; // 현재 한 칸을 이동 중인지 나타내어 이동 입력이 겹치는 것을 막습니다.
    private bool acceptsPlayerInput = true; // Stage 오프닝 같은 연출 중 일반 이동 입력을 받을 수 있는지 나타냅니다.
    private FacingDirection facingDirection; // 이동이 끝난 뒤에도 유지할 마지막 바라보기 방향입니다.
    private Vector2 activeInputDirection; // 동시에 여러 키가 눌렸을 때 가장 최근에 누른 이동 방향입니다.
    private PlayableGraph animationGraph; // 지정한 AnimationClip을 Animator에 직접 출력하는 재생 그래프입니다.
    private AnimationClipPlayable currentAnimation; // 현재 재생 중인 애니메이션 클립의 Playable입니다.
    private AnimationPlayableOutput animationOutput; // 재생 그래프의 애니메이션을 플레이어 Animator로 전달하는 출력입니다.

    /// <summary>
    /// 필요한 컴포넌트를 찾고 시작 방향을 저장합니다.
    /// </summary>
    private void Awake()
    {
        playerAnimator = GetComponent<Animator>();
        walkingAudioSource = GetComponent<AudioSource>();
        facingDirection = startDirection;

        if (walkingAudioSource == null)
        {
            walkingAudioSource = gameObject.AddComponent<AudioSource>();
        }

        walkingAudioSource.playOnAwake = false;
        walkingAudioSource.loop = true;
        walkingAudioSource.clip = walkingSound;
        walkingAudioSource.volume = walkingSoundVolume;

        animationGraph = PlayableGraph.Create("Player Grid Movement Animation");
        animationGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        animationOutput = AnimationPlayableOutput.Create(animationGraph, "Player Animation", playerAnimator);
        animationGraph.Play();
    }

    /// <summary>
    /// 오브젝트가 활성화될 때 시작 방향의 대기 애니메이션을 재생합니다.
    /// </summary>
    private void Start()
    {
        PlayIdleAnimation();
    }

    /// <summary>
    /// 오브젝트가 제거될 때 직접 만든 애니메이션 재생 그래프를 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        if (animationGraph.IsValid())
        {
            animationGraph.Destroy();
        }
    }

    /// <summary>
    /// 이동 중이 아닐 때 현재 누르고 있는 방향키 또는 WASD 입력을 확인합니다.
    /// </summary>
    private void Update()
    {
        if (!acceptsPlayerInput || teleportFrame == Time.frameCount)
        {
            return;
        }

        UpdateActiveInputDirection();

        if (isMoving)
        {
            return;
        }

        Vector2 inputDirection = activeInputDirection; // 가장 최근에 눌렸으며 현재 유지 중인 이동 방향입니다.

        if (inputDirection == Vector2.zero)
        {
            return;
        }

        FacingDirection inputFacingDirection = GetFacingDirection(inputDirection); // 현재 입력이 가리키는 바라보기 방향입니다.
        bool directionChanged = inputFacingDirection != facingDirection; // 벽 앞에서 방향만 바뀌었는지 확인하는 값입니다.
        facingDirection = inputFacingDirection;

        Vector3 firstTargetPosition = transform.position + (Vector3)(inputDirection * tileSize); // 입력 방향으로 이동할 첫 번째 칸의 좌표입니다.

        if (IsBlocked(firstTargetPosition))
        {
            if (directionChanged)
            {
                PlayIdleAnimation();
            }

            return;
        }

        StartCoroutine(MoveWhileHeld(inputDirection));
    }

    /// <summary>
    /// 키를 계속 누르면 다음 칸으로 이어서 이동하되, 대각선 이동을 막기 위해 한 방향만 반환합니다.
    /// </summary>
    private Vector2 ReadHeldDirectionInput()
    {
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W))
        {
            return Vector2.up;
        }

        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S))
        {
            return Vector2.down;
        }

        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A))
        {
            return Vector2.left;
        }

        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D))
        {
            return Vector2.right;
        }

        return Vector2.zero;
    }

    /// <summary>
    /// 이동 중에도 새로 누른 방향을 감지하여 기존에 누르고 있던 방향보다 우선하도록 저장합니다.
    /// </summary>
    private void UpdateActiveInputDirection()
    {
        Vector2 newInputDirection = ReadNewDirectionInput(); // 이번 프레임에 가장 새로 눌린 방향입니다.

        if (newInputDirection != Vector2.zero)
        {
            activeInputDirection = newInputDirection;
            return;
        }

        if (activeInputDirection == Vector2.zero || !IsDirectionHeld(activeInputDirection))
        {
            activeInputDirection = ReadHeldDirectionInput();
        }
    }

    /// <summary>
    /// 이번 프레임에 새로 눌린 방향을 확인하여 먼저 누르고 있던 키보다 높은 우선순위로 반환합니다.
    /// </summary>
    private Vector2 ReadNewDirectionInput()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            return Vector2.up;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            return Vector2.down;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            return Vector2.left;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            return Vector2.right;
        }

        return Vector2.zero;
    }

    /// <summary>
    /// 저장된 방향에 해당하는 방향키 또는 WASD가 아직 눌려 있는지 확인합니다.
    /// </summary>
    private bool IsDirectionHeld(Vector2 direction)
    {
        if (direction == Vector2.up)
        {
            return Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
        }

        if (direction == Vector2.down)
        {
            return Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
        }

        if (direction == Vector2.left)
        {
            return Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
        }

        if (direction == Vector2.right)
        {
            return Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
        }

        return false;
    }

    /// <summary>
    /// 키를 누르는 동안 걷기 애니메이션을 끊지 않고 정확한 칸 단위로 연속 이동합니다.
    /// </summary>
    private IEnumerator MoveWhileHeld(Vector2 inputDirection)
    {
        isMoving = true;
        PlayWalkAnimation();
        StartWalkingSound();

        while (inputDirection != Vector2.zero)
        {
            Vector3 startPosition = transform.position; // 현재 한 칸 이동을 시작한 월드 좌표입니다.
            Vector3 targetPosition = startPosition + (Vector3)(inputDirection * tileSize); // 반드시 도착해야 하는 다음 칸의 월드 좌표입니다.

            if (IsBlocked(targetPosition))
            {
                break;
            }

            float moveDuration = tileSize / moveSpeed; // 설정한 속도로 한 칸을 이동하는 데 걸리는 시간입니다.
            float elapsedTime = 0f; // 현재 한 칸 이동에 사용된 누적 시간입니다.

            while (elapsedTime < moveDuration)
            {
                elapsedTime += Time.deltaTime;
                float moveRatio = Mathf.Clamp01(elapsedTime / moveDuration); // 시작점부터 도착점까지 진행한 0~1 비율입니다.
                transform.position = Vector3.Lerp(startPosition, targetPosition, moveRatio);
                yield return null;
            }

            transform.position = targetPosition;

            Vector2 nextInputDirection = activeInputDirection; // 한 칸 도착 후 가장 최근에 누르고 있는 다음 이동 방향입니다.

            if (nextInputDirection == Vector2.zero)
            {
                break;
            }

            FacingDirection nextFacingDirection = GetFacingDirection(nextInputDirection); // 다음 칸에서 사용할 바라보기 방향입니다.

            if (nextFacingDirection != facingDirection)
            {
                facingDirection = nextFacingDirection;
                PlayWalkAnimation();
            }

            inputDirection = nextInputDirection;
        }

        isMoving = false;
        StopWalkingSound();
        PlayIdleAnimation();
    }

    /// <summary>
    /// 다음 칸 위치에 벽 레이어의 Collider2D가 있는지 검사하여 이동 가능 여부를 결정합니다.
    /// </summary>
    private bool IsBlocked(Vector3 targetPosition)
    {
        Collider2D obstacle = Physics2D.OverlapBox(
            targetPosition,
            collisionCheckSize,
            0f,
            obstacleLayers); // 다음 칸에서 발견된 벽 Collider2D입니다.

        return obstacle != null;
    }

    /// <summary>
    /// Scene 창에서 다음 칸의 벽 검사 크기를 쉽게 확인할 수 있도록 표시합니다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, collisionCheckSize);
    }

    /// <summary>
    /// 이동 벡터를 애니메이션 이름에 사용할 바라보기 방향으로 변환합니다.
    /// </summary>
    private FacingDirection GetFacingDirection(Vector2 inputDirection)
    {
        if (inputDirection == Vector2.up)
        {
            return FacingDirection.Up;
        }

        if (inputDirection == Vector2.down)
        {
            return FacingDirection.Down;
        }

        if (inputDirection == Vector2.left)
        {
            return FacingDirection.Left;
        }

        return FacingDirection.Right;
    }

    /// <summary>
    /// 현재 바라보는 방향의 걷기 애니메이션 상태를 즉시 재생합니다.
    /// </summary>
    private void PlayWalkAnimation()
    {
        PlayAnimation(GetWalkClip());
    }

    /// <summary>
    /// 현재 바라보는 방향의 대기 애니메이션 상태를 즉시 재생합니다.
    /// </summary>
    private void PlayIdleAnimation()
    {
        PlayAnimation(GetIdleClip());
    }

    /// <summary>
    /// Stage 오프닝에서 Player를 위로 올리는 동안 위쪽 걷기 애니메이션을 재생합니다.
    /// </summary>
    public void PlayOpeningWalkUp()
    {
        facingDirection = FacingDirection.Up;
        PlayWalkAnimation();
        StartWalkingSound();
    }

    /// <summary>
    /// Stage 오프닝 이동이 끝난 뒤 위쪽 대기 애니메이션을 재생합니다.
    /// </summary>
    public void PlayOpeningIdleUp()
    {
        facingDirection = FacingDirection.Up;
        StopWalkingSound();
        PlayIdleAnimation();
    }

    /// <summary>
    /// 걷기 소리가 설정되어 있을 때 현재 위치부터 반복 재생을 시작합니다.
    /// </summary>
    private void StartWalkingSound()
    {
        if (walkingSound == null || walkingAudioSource.isPlaying)
        {
            return;
        }

        walkingAudioSource.clip = walkingSound;
        walkingAudioSource.volume = walkingSoundVolume;
        walkingAudioSource.Play();
    }

    /// <summary>
    /// 이동이 끝나거나 벽에 막혔을 때 반복 중인 걷기 소리를 즉시 정지합니다.
    /// </summary>
    private void StopWalkingSound()
    {
        if (walkingAudioSource.isPlaying)
        {
            walkingAudioSource.Stop();
        }
    }

    /// <summary>
    /// 이동 컴포넌트와 애니메이션은 유지하면서 사용자의 방향 입력만 허용하거나 차단합니다.
    /// </summary>
    public void SetPlayerInputEnabled(bool isEnabled)
    {
        acceptsPlayerInput = isEnabled;
        activeInputDirection = Vector2.zero;
    }

    /// <summary>
    /// 현재 방향에 Inspector에서 직접 지정한 걷기 애니메이션을 반환합니다.
    /// </summary>
    private AnimationClip GetWalkClip()
    {
        switch (facingDirection)
        {
            case FacingDirection.Down:
                return walkDown;
            case FacingDirection.Left:
                return walkLeft;
            case FacingDirection.Right:
                return walkRight;
            case FacingDirection.Up:
                return walkUp;
            default:
                return null;
        }
    }

    /// <summary>
    /// 현재 방향에 Inspector에서 직접 지정한 대기 애니메이션을 반환합니다.
    /// </summary>
    private AnimationClip GetIdleClip()
    {
        switch (facingDirection)
        {
            case FacingDirection.Down:
                return idleDown;
            case FacingDirection.Left:
                return idleLeft;
            case FacingDirection.Right:
                return idleRight;
            case FacingDirection.Up:
                return idleUp;
            default:
                return null;
        }
    }

    /// <summary>
    /// 이름 검색 없이 Inspector에 지정된 AnimationClip을 Animator에서 처음부터 직접 재생합니다.
    /// </summary>
    private void PlayAnimation(AnimationClip animationClip)
    {
        if (animationClip == null)
        {
            Debug.LogWarning($"{facingDirection} 방향 애니메이션이 설정되지 않았습니다.", this);
            return;
        }

        if (currentAnimation.IsValid())
        {
            animationGraph.DestroyPlayable(currentAnimation);
        }

        currentAnimation = AnimationClipPlayable.Create(animationGraph, animationClip);
        currentAnimation.SetApplyFootIK(false);
        currentAnimation.SetApplyPlayableIK(false);
        animationOutput.SetSourcePlayable(currentAnimation);
    }
}
