using Fusion;
using TMPro;
using UnityEngine;

public class RoomEntryUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI roomNameText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private GameObject joinButtonObject;
    [SerializeField] private GameObject confirmButtonObject;

    private SessionInfo session;
    private RoomBrowserUI roomBrowserUI;

    public void Setup(SessionInfo session, RoomBrowserUI roomBrowserUI)
    {
        this.session = session;
        this.roomBrowserUI = roomBrowserUI;

        roomNameText.text = session.Name;
        bool isPrivate = NetworkRunnerManager.Instance.SessionHasPassword(session);
        string privacyLabel = isPrivate ? "Private" : "Public";
        bool isInGame = NetworkRunnerManager.Instance.SessionIsInGame(session);
        string inGameLabel = isInGame ? " -- Game In Session" : "";
        playerCountText.text = $"{privacyLabel} | Player Count: {session.PlayerCount}/{session.MaxPlayers}{inGameLabel}";

        // Start with join visible, pw + confirm hidden (disabled if game in session)
        if (joinButtonObject != null) joinButtonObject.SetActive(!isInGame);
        if (passwordInputField != null) passwordInputField.gameObject.SetActive(false);
        if (confirmButtonObject != null) confirmButtonObject.SetActive(false);
    }

    public void OnJoinClicked()
    {
        if (NetworkRunnerManager.Instance.IsBusy) return;
        if (!NetworkRunnerManager.Instance.SessionHasPassword(session))
        {
            roomBrowserUI.TryJoinRoom(session, "");
            return;
        }

        // Show password input + confirm, hide join
        if (joinButtonObject != null) joinButtonObject.SetActive(false);
        if (passwordInputField != null) passwordInputField.gameObject.SetActive(true);
        if (confirmButtonObject != null) confirmButtonObject.SetActive(true);
    }

    public void OnConfirmClicked()
    {
        if (NetworkRunnerManager.Instance.IsBusy) return;
        string password = passwordInputField != null ? passwordInputField.text : "";
        roomBrowserUI.TryJoinRoom(session, password);
    }
}
