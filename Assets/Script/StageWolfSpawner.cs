using UnityEngine;

/// <summary>
/// 밤이 되었고 카운터에서 늑대를 들였다면 추격 늑대를 스테이지에 생성합니다.
/// </summary>
public class StageWolfSpawner : MonoBehaviour
{
    [SerializeField] private GameObject wolfPrefab; // 플레이어를 추격할 늑대 Prefab입니다.

    [SerializeField] private string wolfSpawnRoom = "복도"; // 체크인한 늑대가 처음 생성되는 방 이름입니다.

    private readonly Vector3 wolfSpawnPosition = new Vector3(-7.5f, 4.5f, 0f); // 늑대가 나타날 고정 월드 좌표입니다.
    private bool hasSpawnedWolf; // 같은 스포너가 늑대를 두 번 생성하지 않도록 기록하는 값입니다.

    private void Start()
    {
        if (GameTimeManager.Instance == null)
        {
            Debug.LogError("씬에 GameTimeManager가 필요합니다.", this);
            return;
        }

        GameTimeManager.Instance.TimeChanged += HandleTimeChanged;
        TrySpawnWolf();
    }

    private void OnDestroy()
    {
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.TimeChanged -= HandleTimeChanged;
        }
    }

    /// <summary>
    /// 시간이 변경되면 밤 스폰 조건을 다시 확인합니다.
    /// </summary>
    private void HandleTimeChanged(GameTimeManager.TimePeriod changedTime)
    {
        TrySpawnWolf();
    }

    /// <summary>
    /// 밤이고 카운터에서 늑대를 받아들였다면 늑대 Prefab을 한 번 생성합니다.
    /// </summary>
    private void TrySpawnWolf()
    {
        if (hasSpawnedWolf)
        {
            return;
        }

        bool isNight = GameTimeManager.Instance.CurrentTime == GameTimeManager.TimePeriod.Night; // 현재 시간이 밤인지 확인한 결과입니다.
        bool wolfEntered = GameTimeManager.Instance.WolfEntered; // 카운터에서 늑대를 받아들였는지 확인한 결과입니다.

        if (!isNight || !wolfEntered)
        {
            Debug.Log($"늑대 스폰 대기: 밤={isNight}, 늑대 체크인={wolfEntered}", this);
            return;
        }

        if (wolfPrefab == null)
        {
            Debug.LogError("StageWolfSpawner에 늑대 Prefab을 연결해 주세요.", this);
            return;
        }

        GameObject spawnedWolf = Instantiate(wolfPrefab, wolfSpawnPosition, Quaternion.identity); // 체크인 조건으로 생성한 늑대입니다.
        WolfHunter2D hunter = spawnedWolf.GetComponent<WolfHunter2D>(); // 현재 방과 추격 경로를 관리하는 컴포넌트입니다.
        if (hunter != null) hunter.SetCurrentRoom(wolfSpawnRoom);
        else Debug.LogError("Wolf Prefab에 WolfHunter2D가 필요합니다.", spawnedWolf);
        hasSpawnedWolf = true;
        Debug.Log($"늑대를 {wolfSpawnPosition} 위치에 생성했습니다.", this);
    }
}
