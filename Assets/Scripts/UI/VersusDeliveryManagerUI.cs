using System;
using Fusion;
using UnityEngine;

public class VersusDeliveryManagerUI : MonoBehaviour
{
    [SerializeField] private Transform container;
    [SerializeField] private Transform recipeTemplate;

    private bool subscribed;
    private int lastKnownLocalTeam = -1;

    private void Awake() {
        recipeTemplate.gameObject.SetActive(false);
    }

    private void Start() {
        if (MapManager.Instance != null && !MapManager.Instance.IsVersus) {
            gameObject.SetActive(false);
            return;
        }

        VersusMatchManager.OnInstanceReady += VersusMatchManager_OnInstanceReady;
        if (VersusMatchManager.Instance != null) {
            SubscribeToManager();
        }
    }

    private void OnDestroy() {
        VersusMatchManager.OnInstanceReady -= VersusMatchManager_OnInstanceReady;
        if (subscribed && VersusMatchManager.Instance != null) {
            VersusMatchManager.Instance.OnTeamOrdersChanged -= VersusMatchManager_OnTeamOrdersChanged;
        }
    }

    private void VersusMatchManager_OnInstanceReady(object sender, EventArgs e) {
        SubscribeToManager();
    }

    private void SubscribeToManager() {
        if (subscribed) return;
        subscribed = true;
        VersusMatchManager.Instance.OnTeamOrdersChanged += VersusMatchManager_OnTeamOrdersChanged;
        UpdateVisual();
    }

    private void VersusMatchManager_OnTeamOrdersChanged(object sender, VersusMatchManager.TeamEventArgs e) {
        int localTeam = GetLocalTeamIndex();
        if (localTeam == -1) return;
        if (e.TeamIndex != localTeam) return;
        UpdateVisual();
    }

    private void Update() {
        if (!subscribed) return;
        int localTeam = GetLocalTeamIndex();
        if (localTeam != lastKnownLocalTeam) {
            lastKnownLocalTeam = localTeam;
            if (localTeam != -1) UpdateVisual();
        }
    }

    private int GetLocalTeamIndex() {
        return Player.LocalInstance != null ? Player.LocalInstance.TeamIndex : -1;
    }

    private NetworkRunner GetRunner() {
        return NetworkRunnerManager.Instance != null ? NetworkRunnerManager.Instance.Runner : null;
    }

    private void UpdateVisual() {
        if (!VersusMatchManager.IsReady) return;

        int localTeam = GetLocalTeamIndex();
        if (localTeam == -1) return;

        foreach (Transform child in container) {
            if (child == recipeTemplate) continue;
            Destroy(child.gameObject);
        }

        NetworkRunner runner = GetRunner();

        var orders = VersusMatchManager.Instance.GetTeamOrders(localTeam);
        foreach (VersusMatchManager.OrderData order in orders) {
            RecipeSO recipeSO = VersusMatchManager.Instance.GetRecipeSO(order.RecipeIndex);
            if (recipeSO == null) continue;

            float seconds = recipeSO.orderExpiryDurationSeconds > 0f
                ? recipeSO.orderExpiryDurationSeconds
                : VersusMatchManager.Instance.OrderExpiryDurationSeconds;
            if (order.IsSabotage) seconds *= VersusMatchManager.Instance.SabotageExpiryMultiplier;
            int totalDurationTicks = runner != null
                ? Mathf.Max(1, Mathf.RoundToInt(seconds / runner.DeltaTime))
                : 1;

            Transform recipeTransform = Instantiate(recipeTemplate, container);
            recipeTransform.gameObject.SetActive(true);
            DeliveryManagerSingleUI singleUI = recipeTransform.GetComponent<DeliveryManagerSingleUI>();
            singleUI.SetOrder(recipeSO, order.ExpirationTick, totalDurationTicks, order.IsSabotage, runner);
        }
    }
}
