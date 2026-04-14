using System;
using UnityEngine;
using UnityEngine.UI;

public class HealthUI : MonoBehaviour
{
    [SerializeField] private Image[] ownHearts;
    [SerializeField] private Image[] opponentHearts;
    [SerializeField] private Sprite fullHeartSprite;
    [SerializeField] private Sprite emptyHeartSprite;

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
            VersusMatchManager.Instance.OnTeamHeartsChanged -= VersusMatchManager_OnTeamHeartsChanged;
        }
    }

    private void VersusMatchManager_OnInstanceReady(object sender, EventArgs e) {
        SubscribeToManager();
    }

    private void SubscribeToManager() {
        if (subscribed) return;
        subscribed = true;
        VersusMatchManager.Instance.OnTeamHeartsChanged += VersusMatchManager_OnTeamHeartsChanged;
        Refresh();
    }

    private void VersusMatchManager_OnTeamHeartsChanged(object sender, EventArgs e) {
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
        int opponent = 1 - local;

        SetHearts(ownHearts, VersusMatchManager.Instance.GetTeamHearts(local));
        SetHearts(opponentHearts, VersusMatchManager.Instance.GetTeamHearts(opponent));
    }

    private void SetHearts(Image[] hearts, int heartsRemaining) {
        if (hearts == null) return;
        for (int i = 0; i < hearts.Length; i++) {
            if (hearts[i] == null) continue;
            hearts[i].sprite = i < heartsRemaining ? fullHeartSprite : emptyHeartSprite;
        }
    }
}
