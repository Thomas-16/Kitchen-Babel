using Fusion;
using Michsky.UI.Heat;
using UnityEngine;
using UnityEngine.UI;

public class PreGameLobbyUI : MonoBehaviour
{
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerTemplatePrefab;
    [SerializeField] private GameObject startButtonObject;
    [SerializeField] private ModalWindowManager preGameLobbyModal;
    [SerializeField] private ModalWindowManager roomBrowserModal;
    [SerializeField] private Image characterPreviewImage;
    [SerializeField] private HorizontalSelector colorSelector;
    [SerializeField] private PlayerColorDataSO playerColorDataSO;

    private const string ColorPrefsKey = "HorizontalSelector_PlayerColor";

    private int lastSeenVersion = -1;
    private bool hasSetName = false;
    private int previousColorIndex = -1;

    private void Start()
    {
        NetworkRunnerManager.Instance.OnDisconnected += HandleDisconnected;
    }

    private void OnEnable()
    {
        hasSetName = false;
        lastSeenVersion = -1;
        int savedColor = PlayerPrefs.GetInt(ColorPrefsKey, 0);
        UpdatePreviewImage(savedColor);
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

            int savedColor = PlayerPrefs.GetInt(ColorPrefsKey, 0);
            OnColorChanged(savedColor);
        }

        if (LobbyManager.Instance.PlayersVersion == lastSeenVersion) return;
        Debug.Log($"[PreGameLobbyUI] Version changed: {lastSeenVersion} -> {LobbyManager.Instance.PlayersVersion}");
        lastSeenVersion = LobbyManager.Instance.PlayersVersion;
        RefreshPlayerList();
        SyncLocalColorSelector();
    }

    private void SyncLocalColorSelector()
    {
        if (colorSelector == null || LobbyManager.Instance == null) return;

        NetworkRunner runner = NetworkRunnerManager.Instance.Runner;
        if (runner == null) return;

        int serverColor = LobbyManager.Instance.GetPlayerColorIndex(runner.LocalPlayer);
        if (colorSelector.index != serverColor)
        {
            colorSelector.index = serverColor;
            colorSelector.UpdateUI();
            UpdatePreviewImage(serverColor);
            PlayerPrefs.SetInt(ColorPrefsKey, serverColor);
        }
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
            entryUI.Setup(data, isHost, localPlayer, playerColorDataSO);
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

    public void OnColorChanged(int index)
    {
        if (LobbyManager.Instance != null && colorSelector != null)
        {
            NetworkRunner runner = NetworkRunnerManager.Instance.Runner;
            if (runner != null)
            {
                PlayerRef localPlayer = runner.LocalPlayer;
                if (LobbyManager.Instance.IsColorTaken(index, localPlayer))
                {
                    bool forward = previousColorIndex == -1
                        || index > previousColorIndex
                        || (previousColorIndex == colorSelector.items.Count - 1 && index == 0);

                    index = forward
                        ? LobbyManager.Instance.GetNextAvailableColor(index + 1, localPlayer)
                        : LobbyManager.Instance.GetPrevAvailableColor(index - 1, localPlayer);

                    colorSelector.index = index;
                    colorSelector.UpdateUI();
                }
            }
        }

        previousColorIndex = index;
        UpdatePreviewImage(index);
        PlayerPrefs.SetInt(ColorPrefsKey, index);

        if (LobbyManager.Instance == null) return;
        NetworkRunner r = NetworkRunnerManager.Instance.Runner;
        if (r != null)
        {
            LobbyManager.Instance.RPC_SetPlayerColor(r.LocalPlayer, index);
        }
    }

    private void UpdatePreviewImage(int index)
    {
        if (characterPreviewImage != null && playerColorDataSO != null)
        {
            characterPreviewImage.sprite = playerColorDataSO.GetByIndex(index).previewSprite;
        }
    }
}
