using Michsky.UI.Heat;
using UnityEngine;

public class PreGameLobbyUI : MonoBehaviour
{
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerTemplatePrefab;
    [SerializeField] private GameObject startButtonObject;
    [SerializeField] private ModalWindowManager preGameLobbyModal;
    [SerializeField] private ModalWindowManager roomBrowserModal;

    private int lastSeenVersion = -1;

    private void Start()
    {
        NetworkRunnerManager.Instance.OnDisconnected += HandleDisconnected;
    }

    private void Update()
    {
        if (LobbyManager.Instance == null) return;
        if (LobbyManager.Instance.PlayersVersion == lastSeenVersion) return;
        lastSeenVersion = LobbyManager.Instance.PlayersVersion;
        RefreshPlayerList();
    }

    private void OnDestroy()
    {
        if (NetworkRunnerManager.Instance != null)
            NetworkRunnerManager.Instance.OnDisconnected -= HandleDisconnected;
    }

    private void HandleDisconnected()
    {
        if (preGameLobbyModal != null) preGameLobbyModal.CloseWindow();
        if (roomBrowserModal != null) roomBrowserModal.OpenWindow();
    }

    private void RefreshPlayerList()
    {
        foreach (Transform child in playerListContent)
        {
            Destroy(child.gameObject);
        }

        if (LobbyManager.Instance == null) return;

        var runner = NetworkRunnerManager.Instance.Runner;
        bool isHost = runner != null && runner.IsServer;
        var localPlayer = runner != null ? runner.LocalPlayer : default;

        foreach (var data in LobbyManager.Instance.Players)
        {
            var entry = Instantiate(playerTemplatePrefab, playerListContent);
            entry.SetActive(true);
            var entryUI = entry.GetComponent<PlayerEntryUI>();
            entryUI.Setup(data, isHost, localPlayer);
        }

        UpdateStartButton();
    }

    private void UpdateStartButton()
    {
        if (startButtonObject == null) return;

        var runner = NetworkRunnerManager.Instance.Runner;
        bool isHost = runner != null && runner.IsServer;
        bool allReady = LobbyManager.Instance != null && LobbyManager.Instance.AreAllPlayersReady();

        startButtonObject.SetActive(isHost && allReady);
    }

    public void OnReadyUpClicked()
    {
        if (LobbyManager.Instance == null) return;

        var runner = NetworkRunnerManager.Instance.Runner;
        if (runner != null)
        {
            LobbyManager.Instance.RPC_ToggleReady(runner.LocalPlayer);
        }
    }

    public void OnLeaveClicked()
    {
        NetworkRunnerManager.Instance.LeaveSession();
    }

    public void OnStartClicked()
    {
        NetworkRunnerManager.Instance.StartGame();
    }
}
