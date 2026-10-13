/// <summary>
/// 설명 대사가 재생되는 동안 다른 게임 시스템의 시작을 막는 공통 잠금입니다.
/// </summary>
public static class GameplayDialogueLock
{
    private static int lockCount; // 동시에 실행 중인 설명 대화의 수입니다.

    public static bool IsLocked => lockCount > 0; // 게임 진행을 기다려야 하는지 나타냅니다.

    public static void Acquire()
    {
        lockCount++;
    }

    public static void Release()
    {
        if (lockCount > 0)
        {
            lockCount--;
        }
    }
}
