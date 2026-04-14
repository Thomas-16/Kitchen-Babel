using Fusion;
using System;
using UnityEngine;

public class VersusMatchManager : NetworkBehaviour
{
    public const int TeamCount = 2;
    public const int StartingHearts = 3;
    public const int StartingMoney = 0;

    public static VersusMatchManager Instance { get; private set; }
    public static event EventHandler OnInstanceReady;

    public static bool IsReady =>
        Instance != null && Instance.Object != null && Instance.Object.IsValid;

    public event EventHandler OnTeamHeartsChanged;
    public event EventHandler OnTeamMoneyChanged;
    public event EventHandler OnGameWon;
    public event EventHandler<TeamEventArgs> OnTeamLostHeart;
    public event EventHandler<DeliveryAttemptEventArgs> OnDeliveryAttempted;

    public class TeamEventArgs : EventArgs
    {
        public int TeamIndex;
    }

    public class DeliveryAttemptEventArgs : EventArgs
    {
        public int TeamIndex;
        public bool Success;
    }

    [Networked, Capacity(TeamCount)] public NetworkArray<int> TeamHearts => default;
    [Networked, Capacity(TeamCount)] public NetworkArray<int> TeamMoney => default;
    [Networked] public int WinningTeamIndex { get; set; } = -1;
    [Networked] private NetworkBool IsInitialized { get; set; }

    // Per-team order queues — scaffolded for section #4 to populate.
    [Networked, Capacity(8)] public NetworkLinkedList<int> TeamAOrders => default;
    [Networked, Capacity(8)] public NetworkLinkedList<int> TeamBOrders => default;

    private ChangeDetector changeDetector;

    public static void ResetStaticData()
    {
        OnInstanceReady = null;
    }

    public override void Spawned()
    {
        Instance = this;
        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority && !IsInitialized)
        {
            for (int i = 0; i < TeamCount; i++)
            {
                TeamHearts.Set(i, StartingHearts);
                TeamMoney.Set(i, StartingMoney);
            }
            WinningTeamIndex = -1;
            IsInitialized = true;
        }

        OnInstanceReady?.Invoke(this, EventArgs.Empty);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;
    }

    public override void Render()
    {
        if (changeDetector == null) return;
        foreach (string propertyName in changeDetector.DetectChanges(this))
        {
            switch (propertyName)
            {
                case nameof(TeamHearts):
                    OnTeamHeartsChanged?.Invoke(this, EventArgs.Empty);
                    break;
                case nameof(TeamMoney):
                    OnTeamMoneyChanged?.Invoke(this, EventArgs.Empty);
                    break;
                case nameof(WinningTeamIndex):
                    if (WinningTeamIndex != -1)
                        OnGameWon?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
    }

    // --- Authority-side API ---

    public void DeductHeart(int teamIndex)
    {
        if (!HasStateAuthority) return;
        if (teamIndex < 0 || teamIndex >= TeamCount) return;
        if (WinningTeamIndex != -1) return;

        int current = TeamHearts[teamIndex];
        if (current <= 0) return;
        TeamHearts.Set(teamIndex, current - 1);

        RPC_OnTeamLostHeart(teamIndex);

        if (current - 1 <= 0)
        {
            SetWinner(1 - teamIndex);
        }
    }

    public void AddMoney(int teamIndex, int amount)
    {
        if (!HasStateAuthority) return;
        if (teamIndex < 0 || teamIndex >= TeamCount) return;
        if (amount <= 0) return;
        TeamMoney.Set(teamIndex, TeamMoney[teamIndex] + amount);
    }

    public bool TrySpendMoney(int teamIndex, int amount)
    {
        if (!HasStateAuthority) return false;
        if (teamIndex < 0 || teamIndex >= TeamCount) return false;
        if (amount <= 0) return false;
        int current = TeamMoney[teamIndex];
        if (current < amount) return false;
        TeamMoney.Set(teamIndex, current - amount);
        return true;
    }

    // Stub: section #5 will implement real per-team delivery scoring.
    // Kept here so DeliveryCounter can route versus deliveries through a single entry point today.
    public void DeliverRecipe(int teamIndex, PlateKitchenObject plateKitchenObject)
    {
        if (!HasStateAuthority) return;
        // TODO(section #5): match against the team's order queue, pay out on success,
        // deduct heart/money on failure, fire success/fail RPC for HUD/SFX.
        RPC_OnDeliveryAttempted(teamIndex, false);
    }

    public void ForceWin(int winningTeamIndex)
    {
        if (!HasStateAuthority) return;
        SetWinner(winningTeamIndex);
    }

    private void SetWinner(int winningTeamIndex)
    {
        if (WinningTeamIndex != -1) return;
        WinningTeamIndex = winningTeamIndex;

        if (KitchenGameManager.IsReady)
        {
            KitchenGameManager.Instance.ForceGameOver(winningTeamIndex);
        }
    }

    // --- Read API ---

    public int GetTeamHearts(int teamIndex)
    {
        if (teamIndex < 0 || teamIndex >= TeamCount) return 0;
        return TeamHearts[teamIndex];
    }

    public int GetTeamMoney(int teamIndex)
    {
        if (teamIndex < 0 || teamIndex >= TeamCount) return 0;
        return TeamMoney[teamIndex];
    }

    public int GetWinningTeamIndex() => WinningTeamIndex;

    // --- RPCs for client feedback hooks ---

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnTeamLostHeart(int teamIndex)
    {
        OnTeamLostHeart?.Invoke(this, new TeamEventArgs { TeamIndex = teamIndex });
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnDeliveryAttempted(int teamIndex, NetworkBool success)
    {
        OnDeliveryAttempted?.Invoke(this, new DeliveryAttemptEventArgs
        {
            TeamIndex = teamIndex,
            Success = success,
        });
    }
}
