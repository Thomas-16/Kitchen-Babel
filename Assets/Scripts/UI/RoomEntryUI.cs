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
        playerCountText.text = $"{session.PlayerCount}/{session.MaxPlayers}";

        // Start with join visible, pw + confirm hidden
        if (joinButtonObject != null) joinButtonObject.SetActive(true);
        if (passwordInputField != null) passwordInputField.gameObject.SetActive(false);
        if (confirmButtonObject != null) confirmButtonObject.SetActive(false);
    }

    public void OnJoinClicked()
    {
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
        string password = passwordInputField != null ? passwordInputField.text : "";
        roomBrowserUI.TryJoinRoom(session, password);
    }
}
