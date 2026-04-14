using Fusion;
using System;
using System.Collections.Generic;
using UnityEngine;

public class VersusMatchManager : NetworkBehaviour
{
    public const int TeamCount = 2;
    public const int StartingHearts = 3;
    public const int StartingMoney = 0;

    public const int OrderCapacity = 6;
    public const int InitialNaturalOrders = 2;
    public const float NaturalOrderIntervalSeconds = 15f;
    public const float OrderExpiryDurationSeconds = 50f;
    public const int WrongDeliveryMoneyPenalty = 50;

    public static VersusMatchManager Instance { get; private set; }
    public static event EventHandler OnInstanceReady;

    public static bool IsReady =>
        Instance != null && Instance.Object != null && Instance.Object.IsValid;

    public event EventHandler OnTeamHeartsChanged;
    public event EventHandler OnTeamMoneyChanged;
    public event EventHandler OnGameWon;
    public event EventHandler<TeamEventArgs> OnTeamLostHeart;
    public event EventHandler<TeamEventArgs> OnTeamOrdersChanged;
    public event EventHandler<TeamEventArgs> OnTeamSuccessfulRecipesChanged;
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

    public struct OrderData : INetworkStruct
    {
        public int RecipeIndex;
        public int ExpirationTick;
        public NetworkBool IsSabotage;
    }

    [SerializeField] private RecipeListSO recipeListSO;

    [Networked, Capacity(TeamCount)] public NetworkArray<int> TeamHearts => default;
    [Networked, Capacity(TeamCount)] public NetworkArray<int> TeamMoney => default;
    [Networked, Capacity(TeamCount)] public NetworkArray<int> TeamSuccessfulRecipes => default;
    [Networked, Capacity(TeamCount)] public NetworkArray<int> TeamOrdersVersion => default;
    [Networked] public int WinningTeamIndex { get; set; } = -1;
    [Networked] private NetworkBool IsInitialized { get; set; }
    [Networked] private int NextOrderSpawnTick { get; set; }

    [Networked, Capacity(OrderCapacity)] public NetworkLinkedList<OrderData> TeamAOrders => default;
    [Networked, Capacity(OrderCapacity)] public NetworkLinkedList<OrderData> TeamBOrders => default;

    private ChangeDetector changeDetector;
    private readonly int[] lastKnownOrdersVersion = new int[TeamCount];

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
                TeamSuccessfulRecipes.Set(i, 0);
            }
            WinningTeamIndex = -1;
            NextOrderSpawnTick = 0;
            IsInitialized = true;
        }

        for (int i = 0; i < TeamCount; i++)
        {
            lastKnownOrdersVersion[i] = TeamOrdersVersion[i];
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
                case nameof(TeamSuccessfulRecipes):
                    OnTeamSuccessfulRecipesChanged?.Invoke(this, new TeamEventArgs { TeamIndex = -1 });
                    break;
                case nameof(WinningTeamIndex):
                    if (WinningTeamIndex != -1)
                        OnGameWon?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }

        for (int i = 0; i < TeamCount; i++)
        {
            int version = TeamOrdersVersion[i];
            if (version != lastKnownOrdersVersion[i])
            {
                lastKnownOrdersVersion[i] = version;
                OnTeamOrdersChanged?.Invoke(this, new TeamEventArgs { TeamIndex = i });
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (!KitchenGameManager.IsReady) return;
        if (!KitchenGameManager.Instance.IsGamePlaying()) return;
        if (WinningTeamIndex != -1) return;

        int intervalTicks = Mathf.Max(1, Mathf.RoundToInt(NaturalOrderIntervalSeconds / Runner.DeltaTime));

        if (NextOrderSpawnTick == 0)
        {
            for (int i = 0; i < InitialNaturalOrders; i++)
            {
                SpawnNaturalOrderForBothTeams();
            }
            NextOrderSpawnTick = Runner.Tick + intervalTicks;
        }

        if (Runner.Tick >= NextOrderSpawnTick)
        {
            SpawnNaturalOrderForBothTeams();
            NextOrderSpawnTick = Runner.Tick + intervalTicks;
        }

        SweepExpiredOrders(0);
        SweepExpiredOrders(1);
    }

    private void SpawnNaturalOrderForBothTeams()
    {
        if (recipeListSO == null || recipeListSO.recipeSOList.Count == 0) return;

        int recipeIndex = UnityEngine.Random.Range(0, recipeListSO.recipeSOList.Count);
        int expirationTick = Runner.Tick + Mathf.RoundToInt(OrderExpiryDurationSeconds / Runner.DeltaTime);

        OrderData order = new OrderData
        {
            RecipeIndex = recipeIndex,
            ExpirationTick = expirationTick,
            IsSabotage = false,
        };

        for (int team = 0; team < TeamCount; team++)
        {
            var queue = GetTeamOrders(team);
            if (queue.Count < OrderCapacity)
            {
                queue.Add(order);
                BumpOrdersVersion(team);
            }
        }
    }

    private void BumpOrdersVersion(int teamIndex)
    {
        TeamOrdersVersion.Set(teamIndex, TeamOrdersVersion[teamIndex] + 1);
    }

    private void SweepExpiredOrders(int teamIndex)
    {
        if (WinningTeamIndex != -1) return;
        var queue = GetTeamOrders(teamIndex);
        if (queue.Count == 0) return;

        List<OrderData> expired = null;
        for (int i = 0; i < queue.Count; i++)
        {
            if (queue[i].ExpirationTick <= Runner.Tick)
            {
                (expired ??= new List<OrderData>()).Add(queue[i]);
            }
        }
        if (expired == null) return;

        foreach (OrderData order in expired)
        {
            queue.Remove(order);
            BumpOrdersVersion(teamIndex);
            int penalty = ComputeRecipeReward(order.RecipeIndex) / 2;
            if (penalty > 0) DeductMoney(teamIndex, penalty);
            DeductHeart(teamIndex);
            if (WinningTeamIndex != -1) return;
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

    public void DeductMoney(int teamIndex, int amount)
    {
        if (!HasStateAuthority) return;
        if (teamIndex < 0 || teamIndex >= TeamCount) return;
        if (amount <= 0) return;
        int current = TeamMoney[teamIndex];
        int next = Mathf.Max(0, current - amount);
        TeamMoney.Set(teamIndex, next);
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

    public void DeliverRecipe(int teamIndex, PlateKitchenObject plateKitchenObject)
    {
        if (!HasStateAuthority) return;
        if (teamIndex < 0 || teamIndex >= TeamCount)
        {
            RPC_OnDeliveryAttempted(teamIndex, false);
            return;
        }
        if (WinningTeamIndex != -1) return;

        var queue = GetTeamOrders(teamIndex);
        List<KitchenObjectSO> plateContents = plateKitchenObject.GetKitchenObjectSOList();

        for (int i = 0; i < queue.Count; i++)
        {
            OrderData order = queue[i];
            RecipeSO recipeSO = recipeListSO.recipeSOList[order.RecipeIndex];

            if (recipeSO.kitchenObjectSOList.Count != plateContents.Count) continue;

            bool allMatched = true;
            foreach (KitchenObjectSO recipeSOIngredient in recipeSO.kitchenObjectSOList)
            {
                bool found = false;
                foreach (KitchenObjectSO plateIngredient in plateContents)
                {
                    if (plateIngredient == recipeSOIngredient) { found = true; break; }
                }
                if (!found) { allMatched = false; break; }
            }

            if (!allMatched) continue;

            queue.Remove(order);
            BumpOrdersVersion(teamIndex);
            int reward = ComputeRecipeReward(order.RecipeIndex);
            if (reward > 0) AddMoney(teamIndex, reward);
            TeamSuccessfulRecipes.Set(teamIndex, TeamSuccessfulRecipes[teamIndex] + 1);
            RPC_OnDeliveryAttempted(teamIndex, true);
            return;
        }

        // No match — wrong delivery.
        DeductMoney(teamIndex, WrongDeliveryMoneyPenalty);
        DeductHeart(teamIndex);
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

    public int GetTeamSuccessfulRecipes(int teamIndex)
    {
        if (teamIndex < 0 || teamIndex >= TeamCount) return 0;
        return TeamSuccessfulRecipes[teamIndex];
    }

    public int GetWinningTeamIndex() => WinningTeamIndex;

    public NetworkLinkedList<OrderData> GetTeamOrders(int teamIndex)
    {
        return teamIndex == 0 ? TeamAOrders : TeamBOrders;
    }

    public RecipeSO GetRecipeSO(int recipeIndex)
    {
        if (recipeListSO == null) return null;
        if (recipeIndex < 0 || recipeIndex >= recipeListSO.recipeSOList.Count) return null;
        return recipeListSO.recipeSOList[recipeIndex];
    }

    public int ComputeRecipeReward(int recipeIndex)
    {
        RecipeSO recipeSO = GetRecipeSO(recipeIndex);
        if (recipeSO == null) return 0;
        int sum = 0;
        foreach (KitchenObjectSO ingredient in recipeSO.kitchenObjectSOList)
        {
            sum += ingredient.moneyValue;
        }
        return sum;
    }

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
