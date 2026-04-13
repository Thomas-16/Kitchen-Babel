using Fusion;
using Michsky.UI.Heat;
using UnityEngine;

public class VersusPregameLobbyUI : MonoBehaviour
{
    [SerializeField] private Transform teamAContent;
    [SerializeField] private Transform teamBContent;
    [SerializeField] private GameObject playerTemplatePrefab;
    [SerializeField] private GameObject startButtonObject;
    [SerializeField] private ModalWindowManager versusPregameLobbyModal;
    [SerializeField] private ModalWindowManager roomBrowserModal;
    [SerializeField] private HorizontalSelector colorSelector;
    [SerializeField] private PlayerColorDataSO playerColorDataSO;
    [SerializeField] private Michsky.UI.Heat.Dropdown mapDropdown;
    [SerializeField] private GameObject mapSelectorText;
    [SerializeField] private MapListSO mapList;

    private int lastSeenVersion = -1;
    private int lastSeenMapIndex = -2;
    private bool hasSetName = false;
    private int previousColorIndex = -1;

    private void Start()
    {
        NetworkRunnerManager.Instance.OnDisconnected += HandleDisconnected;
        LobbyUIHelpers.PopulateMapDropdown(mapDropdown, mapList);
    }

    private void OnEnable()
    {
        hasSetName = false;
        lastSeenVersion = -1;
        lastSeenMapIndex = -2;
    }

    private void Update()
    {
        if (LobbyManager.Instance == null) return;

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

            int savedColor = PlayerPrefs.GetInt(LobbyUIHelpers.ColorPrefsKey, 0);
            OnColorChanged(savedColor);
        }

        if (LobbyManager.Instance.SelectedMapIndex != lastSeenMapIndex)
        {
            lastSeenMapIndex = LobbyManager.Instance.SelectedMapIndex;
            LobbyUIHelpers.SyncMapDropdown(mapDropdown, lastSeenMapIndex);
        }

        if (LobbyManager.Instance.PlayersVersion == lastSeenVersion) return;
        lastSeenVersion = LobbyManager.Instance.PlayersVersion;
        RefreshPlayerLists();
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
            PlayerPrefs.SetInt(LobbyUIHelpers.ColorPrefsKey, serverColor);
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
        if (versusPregameLobbyModal != null) versusPregameLobbyModal.CloseWindow();
        if (roomBrowserModal != null) roomBrowserModal.OpenWindow();
    }

    private void RefreshPlayerLists()
    {
        ClearContainer(teamAContent);
        ClearContainer(teamBContent);

        if (LobbyManager.Instance == null) return;

        NetworkRunner runner = NetworkRunnerManager.Instance.Runner;
        bool isHost = runner != null && runner.IsServer;
        PlayerRef localPlayer = runner != null ? runner.LocalPlayer : default;

        foreach (LobbyPlayerData data in LobbyManager.Instance.Players)
        {
            Transform parent = data.TeamIndex == 1 ? teamBContent : teamAContent;
            if (parent == null) continue;

            GameObject entry = Instantiate(playerTemplatePrefab, parent);
            entry.SetActive(true);
            PlayerEntryUI entryUI = entry.GetComponent<PlayerEntryUI>();
            entryUI.Setup(data, isHost, localPlayer, playerColorDataSO);
        }

        UpdateStartButton();
    }

    private static void ClearContainer(Transform container)
    {
        if (container == null) return;
        foreach (Transform child in container) Destroy(child.gameObject);
    }

    private void UpdateStartButton()
    {
        NetworkRunner runner = NetworkRunnerManager.Instance.Runner;
        bool isHost = runner != null && runner.IsServer;
        bool allReady = LobbyManager.Instance != null && LobbyManager.Instance.AreAllPlayersReady();
        bool teamsValid = LobbyManager.Instance != null && LobbyManager.Instance.AreTeamsValid();

        if (startButtonObject != null)
        {
            startButtonObject.SetActive(isHost && allReady && teamsValid);
        }

        if (mapDropdown != null)
        {
            mapDropdown.gameObject.SetActive(isHost);
        }

        if (mapSelectorText != null)
        {
            mapSelectorText.SetActive(isHost);
        }
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

    public void OnSwitchTeamClicked()
    {
        if (LobbyManager.Instance == null) return;
        NetworkRunner runner = NetworkRunnerManager.Instance.Runner;
        if (runner != null)
        {
            LobbyManager.Instance.RPC_SwitchTeam(runner.LocalPlayer);
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

    public void OnMapChanged(int dropdownIndex)
    {
        int mapIndex = dropdownIndex <= 0 ? -1 : dropdownIndex - 1;
        if (LobbyManager.Instance != null && LobbyManager.Instance.HasStateAuthority)
        {
            LobbyManager.Instance.SelectedMapIndex = mapIndex;
        }
    }

    public void OnColorChanged(int index)
    {
        int resolved = LobbyUIHelpers.ResolveColorChange(colorSelector, index, previousColorIndex);
        previousColorIndex = resolved;
    }
}
