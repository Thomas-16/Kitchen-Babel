using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{

    private void Awake()
    {
        Time.timeScale = 1f;

        if (NetworkRunnerManager.Instance != null)
        {
            NetworkRunnerManager.Instance.JoinLobby();
        }
    }
}
