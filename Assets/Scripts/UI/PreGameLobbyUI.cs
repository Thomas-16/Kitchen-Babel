using Fusion;
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
    private bool hasSetName = false;

    private void Start()
    {
        NetworkRunnerManager.Instance.OnDisconnected += HandleDisconnected;
    }

    private void OnEnable()
    {
        hasSetName = false;
        lastSeenVersion = -1;
    }

    private void Update()
    {
        if (LobbyManager.Instance == null)
        {
            return;
        }

        if (!hasSetName)
        {
            hasSetName = true;
            string savedName = PlayerPrefs.GetString("PlayerName", "");
            if (!string.IsNullOrWhiteSpace(savedName))
            {
                NetworkRunner runner = NetworkRunnerManager.Instance.Runner;
                if (runner != null)
                {
                    LobbyManager.Instance.RPC_SetPlayerName(runner.LocalPlayer, savedName);
                }
            }
        }

        if (LobbyManager.Instance.PlayersVersion == lastSeenVersion) return;
        Debug.Log($"[PreGameLobbyUI] Version changed: {lastSeenVersion} -> {LobbyManager.Instance.PlayersVersion}");
        lastSeenVersion = LobbyManager.Instance.PlayersVersion;
        RefreshPlayerList();
    }

    private void OnDestroy()
    {
        if (NetworkRunnerManager.Instance != null)
        {
            NetworkRunnerManager.Instance.OnDisconnected -= HandleDisconnected;
        }
    }

    private void HandleDisconnected()
    {
        hasSetName = false;
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

        NetworkRunner runner = NetworkRunnerManager.Instance.Runner;
        bool isHost = runner != null && runner.IsServer;
        PlayerRef localPlayer = runner != null ? runner.LocalPlayer : default;

        Debug.Log($"[PreGameLobbyUI] RefreshPlayerList: {LobbyManager.Instance.Players.Count} players, isHost={isHost}");
        foreach (LobbyPlayerData data in LobbyManager.Instance.Players)
        {
            Debug.Log($"[PreGameLobbyUI]   Player: {data.Player}, Name: {data.PlayerName}");
            GameObject entry = Instantiate(playerTemplatePrefab, playerListContent);
            entry.SetActive(true);
            PlayerEntryUI entryUI = entry.GetComponent<PlayerEntryUI>();
            entryUI.Setup(data, isHost, localPlayer);
        }

        UpdateStartButton();
    }

    private void UpdateStartButton()
    {
        if (startButtonObject == null) return;

        NetworkRunner runner = NetworkRunnerManager.Instance.Runner;
        bool isHost = runner != null && runner.IsServer;
        bool allReady = LobbyManager.Instance != null && LobbyManager.Instance.AreAllPlayersReady();

        startButtonObject.SetActive(isHost && allReady);
    }

    public void OnReadyUpClicked()
    {
        if (LobbyManager.Instance == null) return;

        NetworkRunner runner = NetworkRunnerManager.Instance.Runner;
        if (runner != null)
        {
            LobbyManager.Instance.RPC_ToggleReady(runner.LocalPlayer);
        }
    }

    public void OnLeaveClicked()
    {
        if (NetworkRunnerManager.Instance.IsBusy) return;
        NetworkRunnerManager.Instance.LeaveSession();
    }

    public void OnStartClicked()
    {
        if (NetworkRunnerManager.Instance.IsBusy) return;
        NetworkRunnerManager.Instance.StartGame();
    }
}
