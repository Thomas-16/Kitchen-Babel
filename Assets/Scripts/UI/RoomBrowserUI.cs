using System.Collections.Generic;
using Fusion;
using Michsky.UI.Heat;
using UnityEngine;
using TMPro;

public class RoomBrowserUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private Transform roomListContent;
    [SerializeField] private GameObject roomTemplatePrefab;
    [SerializeField] private NotificationManager incorrectPWNotification;

    private void Start()
    {
        if (searchInput != null)
            searchInput.onValueChanged.AddListener(_ => RefreshRoomList());

        NetworkRunnerManager.Instance.SessionListUpdated += HandleSessionListUpdated;
    }

    private void OnEnable()
    {
        RefreshRoomList();
    }

    private void OnDestroy()
    {
        if (NetworkRunnerManager.Instance != null)
            NetworkRunnerManager.Instance.SessionListUpdated -= HandleSessionListUpdated;
    }

    private void HandleSessionListUpdated(List<SessionInfo> sessions)
    {
        RefreshRoomList();
    }

    public void RefreshRoomList()
    {
        foreach (Transform child in roomListContent)
        {
            Destroy(child.gameObject);
        }

        string filter = searchInput != null ? searchInput.text.ToLower() : "";

        foreach (var session in NetworkRunnerManager.Instance.CachedSessionList)
        {
            if (!string.IsNullOrEmpty(filter) && !session.Name.ToLower().Contains(filter))
                continue;

            var entry = Instantiate(roomTemplatePrefab, roomListContent);
            entry.SetActive(true);
            var entryUI = entry.GetComponent<RoomEntryUI>();
            entryUI.Setup(session, this);
        }
    }

    public void TryJoinRoom(SessionInfo session, string password)
    {
        if (!NetworkRunnerManager.Instance.ValidatePassword(session, password))
        {
            ShowIncorrectPWNotification();
            return;
        }

        NetworkRunnerManager.Instance.JoinRoom(session.Name, password);
    }

    public void OnCreateRoomClicked()
    {
        NetworkRunnerManager.Instance.CreateRoom("Player's Room", "", 4);
    }

    private void ShowIncorrectPWNotification()
    {
        if (incorrectPWNotification != null)
            incorrectPWNotification.ExpandNotification();
    }
}
