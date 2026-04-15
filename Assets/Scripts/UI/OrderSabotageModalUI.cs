using System;
using System.Collections.Generic;
using Fusion;
using Michsky.UI.Heat;
using TMPro;
using UnityEngine;

public class OrderSabotageModalUI : MonoBehaviour
{
    [SerializeField] private ModalWindowManager modalWindow;
    [SerializeField] private TextMeshProUGUI bannerText;
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private Transform recipeListContainer;
    [SerializeField] private RecipeEntryUI recipeEntryTemplate;
    [SerializeField] private NotificationManager sentNotification;

    private readonly List<RecipeEntryUI> entries = new();
    private bool subscribed;
    private bool isOpen;

    private void Awake()
    {
        if (recipeEntryTemplate != null) recipeEntryTemplate.gameObject.SetActive(false);

        SabotageCounter.OnAnyLocalOpen += SabotageCounter_OnAnyLocalOpen;

        if (modalWindow != null)
        {
            modalWindow.onOpen.AddListener(OnModalOpened);
            modalWindow.onClose.AddListener(OnModalClosed);
        }

        VersusMatchManager.OnInstanceReady += VersusMatchManager_OnInstanceReady;
        if (VersusMatchManager.Instance != null) SubscribeToManager();
    }

    private void OnDestroy()
    {
        SabotageCounter.OnAnyLocalOpen -= SabotageCounter_OnAnyLocalOpen;
        VersusMatchManager.OnInstanceReady -= VersusMatchManager_OnInstanceReady;
        if (modalWindow != null)
        {
            modalWindow.onOpen.RemoveListener(OnModalOpened);
            modalWindow.onClose.RemoveListener(OnModalClosed);
        }
        if (subscribed && VersusMatchManager.Instance != null)
        {
            VersusMatchManager.Instance.OnTeamMoneyChanged -= VersusMatchManager_OnTeamMoneyChanged;
            VersusMatchManager.Instance.OnTeamOrdersChanged -= VersusMatchManager_OnTeamOrdersChanged;
            VersusMatchManager.Instance.OnSabotageRequestResolved -= VersusMatchManager_OnSabotageRequestResolved;
        }
    }

    private void VersusMatchManager_OnInstanceReady(object sender, EventArgs e) => SubscribeToManager();

    private void SubscribeToManager()
    {
        if (subscribed) return;
        subscribed = true;
        VersusMatchManager.Instance.OnTeamMoneyChanged += VersusMatchManager_OnTeamMoneyChanged;
        VersusMatchManager.Instance.OnTeamOrdersChanged += VersusMatchManager_OnTeamOrdersChanged;
        VersusMatchManager.Instance.OnSabotageRequestResolved += VersusMatchManager_OnSabotageRequestResolved;
    }

    private void VersusMatchManager_OnTeamMoneyChanged(object sender, EventArgs e)
    {
        if (isOpen) Refresh();
    }

    private void VersusMatchManager_OnTeamOrdersChanged(object sender, VersusMatchManager.TeamEventArgs e)
    {
        if (isOpen) Refresh();
    }

    private void VersusMatchManager_OnSabotageRequestResolved(object sender, VersusMatchManager.SabotageRequestEventArgs e)
    {
        if (!e.Success) return;

        if (sentNotification != null) sentNotification.ExpandNotification();
        if (isOpen) Refresh();
    }

    private void SabotageCounter_OnAnyLocalOpen(SabotageCounter counter)
    {
        if (MapManager.Instance != null && !MapManager.Instance.IsVersus) return;
        if (!VersusMatchManager.IsReady) return;
        if (GetLocalTeamIndex() == -1) return;
        Show();
    }

    private void Show()
    {
        if (modalWindow != null) modalWindow.OpenWindow();
    }

    private void Hide()
    {
        if (modalWindow != null) modalWindow.CloseWindow();
    }

    private void OnModalOpened()
    {
        BuildList();
        Refresh();
        isOpen = true;
    }

    private void OnModalClosed()
    {
        isOpen = false;
    }

    private void Update()
    {
        if (isOpen) RefreshBanner();
    }

    private void BuildList()
    {
        foreach (RecipeEntryUI entry in entries)
        {
            if (entry != null) Destroy(entry.gameObject);
        }
        entries.Clear();

        if (!VersusMatchManager.IsReady) return;

        var recipeListSO = VersusMatchManager.Instance.GetRecipeListSO();
        if (recipeListSO == null) return;

        int localTeam = GetLocalTeamIndex();
        int money = localTeam >= 0 ? VersusMatchManager.Instance.GetTeamMoney(localTeam) : 0;

        for (int i = 0; i < recipeListSO.recipeSOList.Count; i++)
        {
            RecipeSO recipeSO = recipeListSO.recipeSOList[i];
            int cost = VersusMatchManager.Instance.ComputeRecipeReward(i);
            bool canAfford = money >= cost;

            RecipeEntryUI entry = Instantiate(recipeEntryTemplate, recipeListContainer);
            entry.gameObject.SetActive(true);
            entry.Setup(recipeSO, i, cost, canAfford, OnOrderClicked);
            entries.Add(entry);
        }
    }

    private void Refresh()
    {
        if (!VersusMatchManager.IsReady) return;

        int localTeam = GetLocalTeamIndex();
        if (localTeam == -1) return;

        int money = VersusMatchManager.Instance.GetTeamMoney(localTeam);
        if (moneyText != null) moneyText.text = $"${money}";

        bool canOrderNow = ComputeBanner(out _, out _);

        var recipeListSO = VersusMatchManager.Instance.GetRecipeListSO();
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] == null) continue;
            int cost = recipeListSO != null && i < recipeListSO.recipeSOList.Count
                ? VersusMatchManager.Instance.ComputeRecipeReward(i)
                : 0;
            bool canAfford = money >= cost;
            entries[i].SetAffordable(canAfford && canOrderNow);
        }

        RefreshBanner();
    }

    private void RefreshBanner()
    {
        if (bannerText == null) return;
        ComputeBanner(out string text, out _);
        bannerText.text = text;
    }

    private bool ComputeBanner(out string text, out bool hasBlock)
    {
        text = string.Empty;
        hasBlock = false;

        if (!VersusMatchManager.IsReady) return false;
        int localTeam = GetLocalTeamIndex();
        if (localTeam == -1) return false;

        int opponentTeam = 1 - localTeam;
        VersusMatchManager manager = VersusMatchManager.Instance;

        var opponentOrders = manager.GetTeamOrders(opponentTeam);
        if (opponentOrders.Count >= VersusMatchManager.OrderCapacity)
        {
            text = "Opponent Order Queue Full";
            hasBlock = true;
            return false;
        }

        NetworkRunner runner = manager.Runner;
        if (runner == null) return true;

        int nowTick = runner.Tick;
        float dt = runner.DeltaTime;

        int shieldUntil = manager.TeamSabotageShieldUntilTick[opponentTeam];
        if (nowTick < shieldUntil)
        {
            float seconds = (shieldUntil - nowTick) * dt;
            text = $"Shielded - {seconds:0.0}s";
            hasBlock = true;
            return false;
        }

        int cooldownUntil = manager.TeamSabotageCooldownUntilTick[localTeam];
        if (nowTick < cooldownUntil)
        {
            float seconds = (cooldownUntil - nowTick) * dt;
            text = $"Cooldown - {seconds:0.0}s";
            hasBlock = true;
            return false;
        }

        return true;
    }

    private void OnOrderClicked(int recipeIndex)
    {
        if (!VersusMatchManager.IsReady) return;
        int localTeam = GetLocalTeamIndex();
        if (localTeam == -1) return;

        int opponentTeam = 1 - localTeam;
        VersusMatchManager.Instance.RPC_RequestSabotageOrder(opponentTeam, recipeIndex);
    }

    private static int GetLocalTeamIndex()
    {
        return Player.LocalInstance != null ? Player.LocalInstance.TeamIndex : -1;
    }
}
