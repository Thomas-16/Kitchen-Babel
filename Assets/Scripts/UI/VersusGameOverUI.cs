using System;
using TMPro;
using UnityEngine;

public class VersusGameOverUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI ourScoreText;
    [SerializeField] private TextMeshProUGUI enemyScoreText;

    private void Start() {
        KitchenGameManager.OnInstanceReady += KitchenGameManager_OnInstanceReady;
        if (KitchenGameManager.Instance != null) {
            KitchenGameManager.Instance.OnStateChanged += KitchenGameManager_OnStateChanged;
        }
        Hide();
    }

    private void OnDestroy() {
        KitchenGameManager.OnInstanceReady -= KitchenGameManager_OnInstanceReady;
        if (KitchenGameManager.Instance != null) {
            KitchenGameManager.Instance.OnStateChanged -= KitchenGameManager_OnStateChanged;
        }
    }

    private void KitchenGameManager_OnInstanceReady(object sender, EventArgs e) {
        KitchenGameManager.Instance.OnStateChanged += KitchenGameManager_OnStateChanged;
    }

    private void KitchenGameManager_OnStateChanged(object sender, EventArgs e) {
        if (!KitchenGameManager.Instance.IsGameOver()) {
            Hide();
            return;
        }

        if (MapManager.Instance == null || !MapManager.Instance.IsVersus) {
            Hide();
            return;
        }

        int localTeam = Player.LocalInstance != null ? Player.LocalInstance.TeamIndex : -1;
        int winner = KitchenGameManager.Instance.VersusWinningTeamIndex;

        if (winner == -1) {
            titleText.text = "DRAW";
        } else if (winner == localTeam) {
            titleText.text = "VICTORY!";
        } else {
            titleText.text = "DEFEAT";
        }

        int ours = 0;
        int theirs = 0;
        if (VersusMatchManager.IsReady && localTeam != -1) {
            ours = VersusMatchManager.Instance.GetTeamSuccessfulRecipes(localTeam);
            theirs = VersusMatchManager.Instance.GetTeamSuccessfulRecipes(1 - localTeam);
        }
        ourScoreText.text = $"Your team: {ours}";
        enemyScoreText.text = $"Enemy team: {theirs}";

        Show();
    }

    private void Show() {
        gameObject.SetActive(true);
    }

    private void Hide() {
        gameObject.SetActive(false);
    }
}
