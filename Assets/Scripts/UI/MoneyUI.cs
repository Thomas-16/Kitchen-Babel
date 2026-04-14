using System;
using TMPro;
using UnityEngine;

public class MoneyUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI moneyText;

    private bool subscribed;
    private bool modeResolved;
    private int lastKnownLocalTeam = -1;

    private void Start() {
        TryResolveMode();
        if (!gameObject.activeSelf) return;

        VersusMatchManager.OnInstanceReady += VersusMatchManager_OnInstanceReady;
        if (VersusMatchManager.Instance != null) {
            SubscribeToManager();
        }
    }

    private void TryResolveMode() {
        if (modeResolved) return;
        if (MapManager.Instance == null) return;
        modeResolved = true;
        if (!MapManager.Instance.IsVersus) {
            gameObject.SetActive(false);
        }
    }

    private void OnDestroy() {
        VersusMatchManager.OnInstanceReady -= VersusMatchManager_OnInstanceReady;
        if (subscribed && VersusMatchManager.Instance != null) {
            VersusMatchManager.Instance.OnTeamMoneyChanged -= VersusMatchManager_OnTeamMoneyChanged;
        }
    }

    private void VersusMatchManager_OnInstanceReady(object sender, EventArgs e) {
        SubscribeToManager();
    }

    private void SubscribeToManager() {
        if (subscribed) return;
        subscribed = true;
        VersusMatchManager.Instance.OnTeamMoneyChanged += VersusMatchManager_OnTeamMoneyChanged;
        Refresh();
    }

    private void VersusMatchManager_OnTeamMoneyChanged(object sender, EventArgs e) {
        Refresh();
    }

    private void Update() {
        if (!modeResolved) {
            TryResolveMode();
            if (!gameObject.activeSelf) return;
        }
        if (!subscribed) return;
        int localTeam = GetLocalTeamIndex();
        if (localTeam != lastKnownLocalTeam) {
            lastKnownLocalTeam = localTeam;
            if (localTeam != -1) Refresh();
        }
    }

    private int GetLocalTeamIndex() {
        return Player.LocalInstance != null ? Player.LocalInstance.TeamIndex : -1;
    }

    private void Refresh() {
        if (!VersusMatchManager.IsReady) return;
        int local = GetLocalTeamIndex();
        if (local == -1) return;
        moneyText.text = $"${VersusMatchManager.Instance.GetTeamMoney(local)}";
    }
}
