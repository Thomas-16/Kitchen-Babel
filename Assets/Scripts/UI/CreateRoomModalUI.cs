using System.Collections;
using Michsky.UI.Heat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateRoomModalUI : MonoBehaviour
{
    [SerializeField] private ModalWindowManager createRoomModal;
    [SerializeField] private ModalWindowManager roomBrowserModal;
    [SerializeField] private GameObject passwordInputContainer;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private GameObject invalidPasswordText;
    [SerializeField] private RectTransform layoutRoot;
    [SerializeField] private Michsky.UI.Heat.Dropdown maxPlayersDropdown;

    private int selectedMaxPlayers = 2;
    private int selectedGameMode = 0;
    private bool requirePassword = false;
    private bool isCreating = false;
    private Coroutine invalidPasswordCoroutine;

    private const string PLAYER_NAME_KEY = "PlayerName";

    private void Start()
    {
        NetworkRunnerManager.Instance.OnJoinedSession += HandleJoinedSession;
        NetworkRunnerManager.Instance.OnJoinFailed += HandleJoinFailed;

        ResetState();
    }

    private void OnDestroy()
    {
        if (NetworkRunnerManager.Instance != null)
        {
            NetworkRunnerManager.Instance.OnJoinedSession -= HandleJoinedSession;
            NetworkRunnerManager.Instance.OnJoinFailed -= HandleJoinFailed;
        }
    }

    public void OnMaxPlayersChanged(int index)
    {
        selectedMaxPlayers = index + 2;
    }

    public void OnGameModeChanged(int index)
    {
        selectedGameMode = index;
        LobbyUIHelpers.PopulateMaxPlayersDropdown(maxPlayersDropdown, selectedGameMode);

        int maxAllowed = selectedGameMode == LobbyManager.MatchModeVersus
            ? LobbyUIHelpers.VersusMaxPlayers
            : LobbyUIHelpers.CoopMaxPlayers;
        if (selectedMaxPlayers > maxAllowed)
            selectedMaxPlayers = maxAllowed;

        if (maxPlayersDropdown != null)
            maxPlayersDropdown.SetDropdownIndex(selectedMaxPlayers - 2);
    }

    public void OnRequirePasswordChanged(bool value)
    {
        requirePassword = value;
        if (passwordInputContainer != null) passwordInputContainer.SetActive(value);
        if (!value && passwordInputField != null) passwordInputField.text = "";
        if (layoutRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
    }

    public void OnCancelClicked()
    {
        if (createRoomModal != null) createRoomModal.CloseWindow();
        if (roomBrowserModal != null) roomBrowserModal.OpenWindow();
        ResetState();
    }

    public void OnConfirmClicked()
    {
        if (isCreating) return;

        if (requirePassword && string.IsNullOrWhiteSpace(passwordInputField != null ? passwordInputField.text : ""))
        {
            if (invalidPasswordCoroutine != null) StopCoroutine(invalidPasswordCoroutine);
            invalidPasswordCoroutine = StartCoroutine(ShowInvalidPassword());
            return;
        }

        isCreating = true;

        string playerName = PlayerPrefs.GetString(PLAYER_NAME_KEY, "Player");
        string roomName = playerName + "'s Room";
        string password = requirePassword && passwordInputField != null ? passwordInputField.text : "";

        NetworkRunnerManager.Instance.CreateRoom(roomName, password, selectedMaxPlayers, selectedGameMode);
    }

    private void HandleJoinedSession()
    {
        if (createRoomModal != null) createRoomModal.CloseWindow();
        ResetState();
    }

    private void HandleJoinFailed(string reason)
    {
        isCreating = false;
    }

    private void ResetState()
    {
        selectedMaxPlayers = 2;
        selectedGameMode = 0;
        requirePassword = false;
        isCreating = false;
        LobbyUIHelpers.PopulateMaxPlayersDropdown(maxPlayersDropdown, selectedGameMode);
        if (maxPlayersDropdown != null) maxPlayersDropdown.SetDropdownIndex(0);
        if (passwordInputContainer != null) passwordInputContainer.SetActive(false);
        if (passwordInputField != null) passwordInputField.text = "";
        if (invalidPasswordText != null) invalidPasswordText.SetActive(false);
    }

    private IEnumerator ShowInvalidPassword()
    {
        if (invalidPasswordText != null)
        {
            invalidPasswordText.SetActive(true);
            yield return new WaitForSeconds(2f);
            invalidPasswordText.SetActive(false);
        }
    }
}
