using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerEntryUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private GameObject kickButtonObject;
    [SerializeField] private GameObject readyTextObject;
    [SerializeField] private Image colorIconImage;

    private PlayerRef playerRef;

    public void Setup(LobbyPlayerData data, bool isHost, PlayerRef localPlayer, PlayerColorDataSO colorData)
    {
        playerRef = data.Player;
        playerNameText.text = data.PlayerName.ToString();

        bool showKick = isHost && data.Player != localPlayer;
        if (kickButtonObject != null)
            kickButtonObject.SetActive(showKick);

        if (readyTextObject != null)
            readyTextObject.SetActive(data.IsReady);

        if (colorIconImage != null && colorData != null)
        {
            colorIconImage.color = colorData.GetByIndex(data.ColorIndex).tintColor;
        }
    }

    public void OnKickClicked()
    {
        NetworkRunnerManager.Instance.KickPlayer(playerRef);
    }
}
