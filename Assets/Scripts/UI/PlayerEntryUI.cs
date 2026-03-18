using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerEntryUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private GameObject kickButtonObject;

    private PlayerRef playerRef;

    public void Setup(LobbyPlayerData data, bool isHost, PlayerRef localPlayer)
    {
        playerRef = data.Player;
        playerNameText.text = data.PlayerName.ToString();

        bool showKick = isHost && data.Player != localPlayer;
        if (kickButtonObject != null)
            kickButtonObject.SetActive(showKick);
    }

    public void OnKickClicked()
    {
        NetworkRunnerManager.Instance.KickPlayer(playerRef);
    }
}
