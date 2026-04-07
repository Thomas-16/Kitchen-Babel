using TMPro;
using UnityEngine;

public class PlayerNameDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshPro nameText;

    private Player player;
    private string lastDisplayedName;

    private void Awake()
    {
        player = GetComponent<Player>();
    }

    private void LateUpdate()
    {
        if (player == null) return;

        string currentName = player.PlayerName.ToString();
        if (currentName != lastDisplayedName)
        {
            nameText.text = currentName;
            lastDisplayedName = currentName;
        }
    }
}
