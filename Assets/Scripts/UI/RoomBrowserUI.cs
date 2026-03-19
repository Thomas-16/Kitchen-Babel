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
    [SerializeField] private ModalWindowManager roomBrowserModal;
    [SerializeField] private ModalWindowManager preGameLobbyModal;
    [SerializeField] private ModalWindowManager createRoomModal;

    private void Start()
    {
        if (searchInput != null)
            searchInput.onValueChanged.AddListener(_ => RefreshRoomList());

        NetworkRunnerManager.Instance.SessionListUpdated += HandleSessionListUpdated;
        NetworkRunnerManager.Instance.OnJoinedSession += HandleJoinedSession;
        NetworkRunnerManager.Instance.OnJoinFailed += HandleJoinFailed;
    }

    private void OnEnable()
    {
        RefreshRoomList();
    }

    private void OnDestroy()
    {
        if (NetworkRunnerManager.Instance != null)
        {
            NetworkRunnerManager.Instance.SessionListUpdated -= HandleSessionListUpdated;
            NetworkRunnerManager.Instance.OnJoinedSession -= HandleJoinedSession;
            NetworkRunnerManager.Instance.OnJoinFailed -= HandleJoinFailed;
        }
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

        foreach (SessionInfo session in NetworkRunnerManager.Instance.CachedSessionList)
        {
            if (!string.IsNullOrEmpty(filter) && !session.Name.ToLower().Contains(filter))
                continue;

            GameObject entry = Instantiate(roomTemplatePrefab, roomListContent);
            entry.SetActive(true);
            RoomEntryUI entryUI = entry.GetComponent<RoomEntryUI>();
            entryUI.Setup(session, this);
        }
    }

    public void TryJoinRoom(SessionInfo session, string password)
    {
        if (NetworkRunnerManager.Instance.IsBusy) return;
        if (!NetworkRunnerManager.Instance.ValidatePassword(session, password))
        {
            ShowIncorrectPWNotification();
            return;
        }

        NetworkRunnerManager.Instance.JoinRoom(session.Name, password);
    }

    public void OnCreateRoomClicked()
    {
        if (NetworkRunnerManager.Instance.IsBusy) return;
        if (createRoomModal != null) createRoomModal.OpenWindow();
    }

    private void HandleJoinedSession()
    {
        if (roomBrowserModal != null) roomBrowserModal.CloseWindow();
        if (preGameLobbyModal != null) preGameLobbyModal.OpenWindow();
    }

    private void HandleJoinFailed(string reason)
    {
        // Room browser stays open, notification already shown by TryJoinRoom
    }

    public void OnRefreshClicked()
    {
        if (NetworkRunnerManager.Instance.IsBusy) return;
        NetworkRunnerManager.Instance.JoinLobby();
    }

    private void ShowIncorrectPWNotification()
    {
        if (incorrectPWNotification != null)
            incorrectPWNotification.ExpandNotification();
    }
}
