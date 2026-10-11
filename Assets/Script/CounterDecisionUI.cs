using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 들여보내기와 내보내기 버튼 및 현재 정원 표시를 관리합니다.
/// </summary>
public class CounterDecisionUI : MonoBehaviour
{
    [SerializeField] private GameObject buttonGroup; // 두 선택 버튼을 함께 켜고 끄는 부모 오브젝트입니다.
    [SerializeField] private Button checkInButton; // 방문객을 들여보내는 버튼입니다.
    [SerializeField] private Button sendAwayButton; // 방문객을 내보내는 버튼입니다.
    [SerializeField] private Text capacityText; // 현재 체크인 인원을 표시하는 Legacy UI Text입니다.

    public void Initialize(Action onCheckIn, Action onSendAway)
    {
        checkInButton.onClick.RemoveAllListeners();
        sendAwayButton.onClick.RemoveAllListeners();
        checkInButton.onClick.AddListener(() => onCheckIn());
        sendAwayButton.onClick.AddListener(() => onSendAway());
        SetButtonsVisible(false);
    }

    public void SetButtonsVisible(bool visible)
    {
        buttonGroup.SetActive(visible);
        checkInButton.interactable = visible;
        sendAwayButton.interactable = visible;
    }

    public void UpdateCapacity(int currentGuests, int maximumGuests)
    {
        capacityText.text = $"정원 {currentGuests} / {maximumGuests}";
    }
}
