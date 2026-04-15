using Fusion;
using System;
using System.Collections.Generic;
using UnityEngine;

public class VersusMatchManager : NetworkBehaviour
{
    public const int TeamCount = 2;
    public const int OrderCapacity = 6;

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
    public event EventHandler<SabotageRequestEventArgs> OnSabotageRequestResolved;

    public class TeamEventArgs : EventArgs
    {
        public int TeamIndex;
    }

    public class DeliveryAttemptEventArgs : EventArgs
    {
        public int TeamIndex;
        public bool Success;
    }

    public class SabotageRequestEventArgs : EventArgs
    {
        public int TeamIndex;
        public int TargetTeamIndex;
        public int RecipeIndex;
        public bool Success;
    }

    public struct OrderData : INetworkStruct
    {
        public int RecipeIndex;
        public int ExpirationTick;
        public NetworkBool IsSabotage;
    }

    [SerializeField] private RecipeListSO recipeListSO;

    [Header("Match Tuning")]
    [SerializeField] private int startingHearts = 3;
    [SerializeField] private int startingMoney = 0;
    [SerializeField] private int initialNaturalOrders = 2;
    [SerializeField] private float naturalOrderIntervalSeconds = 15f;
    [Tooltip("Fallback expiry duration used only when a RecipeSO's own orderExpiryDurationSeconds is 0 or less.")]
    [SerializeField] private float orderExpiryDurationSeconds = 50f;
    [SerializeField] private int wrongDeliveryMoneyPenalty = 50;

    public float OrderExpiryDurationSeconds => orderExpiryDurationSeconds;

    [Header("Anti-Snowball Tuning")]
    [SerializeField] private int heartLossMoneyBonus = 30;
    [SerializeField] private float heartLossOrderPauseSeconds = 8f;
    [SerializeField] private float heartLossSabotageShieldSeconds = 8f;
    [SerializeField] private float sabotageCooldownSeconds = 6f;

    public float HeartLossOrderPauseSeconds => heartLossOrderPauseSeconds;
    public float HeartLossSabotageShieldSeconds => heartLossSabotageShieldSeconds;
    public float SabotageCooldownSeconds => sabotageCooldownSeconds;
    public float MaxBlockDurationSeconds => Mathf.Max(heartLossSabotageShieldSeconds, sabotageCooldownSeconds);

    [Networked, Capacity(TeamCount)] public NetworkArray<int> TeamHearts => default;
    [Networked, Capacity(TeamCount)] public NetworkArray<int> TeamMoney => default;
    [Networked, Capacity(TeamCount)] public NetworkArray<int> TeamSuccessfulRecipes => default;
    [Networked, Capacity(TeamCount)] public NetworkArray<int> TeamOrdersVersion => default;
    [Networked, Capacity(TeamCount)] public NetworkArray<int> TeamOrderPauseUntilTick => default;
    [Networked, Capacity(TeamCount)] public NetworkArray<int> TeamSabotageShieldUntilTick => default;
    [Networked, Capacity(TeamCount)] public NetworkArray<int> TeamSabotageCooldownUntilTick => default;
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
                TeamHearts.Set(i, startingHearts);
                TeamMoney.Set(i, startingMoney);
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
        if (LobbyManager.GetCachedMatchMode() != LobbyManager.MatchModeVersus) return;
        if (!KitchenGameManager.IsReady) return;
        if (!KitchenGameManager.Instance.IsGamePlaying()) return;
        if (WinningTeamIndex != -1) return;

        int intervalTicks = Mathf.Max(1, Mathf.RoundToInt(naturalOrderIntervalSeconds / Runner.DeltaTime));

        if (NextOrderSpawnTick == 0)
        {
            for (int i = 0; i < initialNaturalOrders; i++)
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

    private int ComputeExpirationTickForRecipe(int recipeIndex)
    {
        float seconds = orderExpiryDurationSeconds;
        if (recipeListSO != null
            && recipeIndex >= 0
            && recipeIndex < recipeListSO.recipeSOList.Count)
        {
            float perRecipe = recipeListSO.recipeSOList[recipeIndex].orderExpiryDurationSeconds;
            if (perRecipe > 0f) seconds = perRecipe;
        }
        return Runner.Tick + Mathf.Max(1, Mathf.RoundToInt(seconds / Runner.DeltaTime));
    }

    private void SpawnNaturalOrderForBothTeams()
    {
        if (recipeListSO == null || recipeListSO.recipeSOList.Count == 0) return;

        int recipeIndex = UnityEngine.Random.Range(0, recipeListSO.recipeSOList.Count);
        int expirationTick = ComputeExpirationTickForRecipe(recipeIndex);

        OrderData order = new OrderData
        {
            RecipeIndex = recipeIndex,
            ExpirationTick = expirationTick,
            IsSabotage = false,
        };

        int nowTick = Runner.Tick;
        for (int team = 0; team < TeamCount; team++)
        {
            if (nowTick < TeamOrderPauseUntilTick[team]) continue;
            var queue = GetTeamOrders(team);
            if (queue.Count < OrderCapacity)
            {
                queue.Add(order);
                BumpOrdersVersion(team);
            }
        }
    }

    public bool IsSabotageShielded(int teamIndex)
    {
        if (teamIndex < 0 || teamIndex >= TeamCount) return false;
        return Runner.Tick < TeamSabotageShieldUntilTick[teamIndex];
    }

    public bool TryAddSabotageOrder(int targetTeamIndex, int recipeIndex)
    {
        if (!HasStateAuthority) return false;
        if (targetTeamIndex < 0 || targetTeamIndex >= TeamCount) return false;
        if (WinningTeamIndex != -1) return false;
        if (recipeListSO == null) return false;
        if (recipeIndex < 0 || recipeIndex >= recipeListSO.recipeSOList.Count) return false;
        if (IsSabotageShielded(targetTeamIndex)) return false;

        var queue = GetTeamOrders(targetTeamIndex);
        if (queue.Count >= OrderCapacity) return false;

        int expirationTick = ComputeExpirationTickForRecipe(recipeIndex);
        queue.Add(new OrderData
        {
            RecipeIndex = recipeIndex,
            ExpirationTick = expirationTick,
            IsSabotage = true,
        });
        BumpOrdersVersion(targetTeamIndex);
        return true;
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
        int remaining = current - 1;
        TeamHearts.Set(teamIndex, remaining);

        RPC_OnTeamLostHeart(teamIndex);

        if (remaining <= 0)
        {
            SetWinner(1 - teamIndex);
            return;
        }

        ApplyAntiSnowballBuffers(teamIndex);
    }

    private void ApplyAntiSnowballBuffers(int teamIndex)
    {
        if (heartLossMoneyBonus > 0) AddMoney(teamIndex, heartLossMoneyBonus);

        int nowTick = Runner.Tick;
        int tickRate = Runner.TickRate;

        if (heartLossOrderPauseSeconds > 0f)
        {
            int pauseUntil = nowTick + Mathf.CeilToInt(heartLossOrderPauseSeconds * tickRate);
            if (pauseUntil > TeamOrderPauseUntilTick[teamIndex])
                TeamOrderPauseUntilTick.Set(teamIndex, pauseUntil);
        }

        if (heartLossSabotageShieldSeconds > 0f)
        {
            int shieldUntil = nowTick + Mathf.CeilToInt(heartLossSabotageShieldSeconds * tickRate);
            if (shieldUntil > TeamSabotageShieldUntilTick[teamIndex])
                TeamSabotageShieldUntilTick.Set(teamIndex, shieldUntil);
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
        DeductMoney(teamIndex, wrongDeliveryMoneyPenalty);
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

    public RecipeListSO GetRecipeListSO() => recipeListSO;

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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnSabotageRequestResolved(PlayerRef requester, int attackerTeam, int targetTeamIndex, int recipeIndex, NetworkBool success)
    {
        if (Runner.LocalPlayer != requester) return;

        OnSabotageRequestResolved?.Invoke(this, new SabotageRequestEventArgs
        {
            TeamIndex = attackerTeam,
            TargetTeamIndex = targetTeamIndex,
            RecipeIndex = recipeIndex,
            Success = success,
        });
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestSabotageOrder(int targetTeamIndex, int recipeIndex, RpcInfo info = default)
    {
        PlayerRef requester = info.Source.IsNone ? Runner.LocalPlayer : info.Source;
        int attackerTeam = ResolvePlayerTeam(requester);
        if (attackerTeam < 0 || attackerTeam >= TeamCount)
        {
            RPC_OnSabotageRequestResolved(requester, attackerTeam, targetTeamIndex, recipeIndex, false);
            return;
        }
        if (targetTeamIndex == attackerTeam)
        {
            RPC_OnSabotageRequestResolved(requester, attackerTeam, targetTeamIndex, recipeIndex, false);
            return;
        }
        if (Runner.Tick < TeamSabotageCooldownUntilTick[attackerTeam])
        {
            RPC_OnSabotageRequestResolved(requester, attackerTeam, targetTeamIndex, recipeIndex, false);
            return;
        }

        int cost = ComputeRecipeReward(recipeIndex);
        // Reject zero-cost sabotages outright — a recipe that sums to $0 indicates
        // misconfigured ingredient moneyValues, and allowing it would let the attacker
        // sabotage for free every cooldown cycle.
        if (cost <= 0 || !TrySpendMoney(attackerTeam, cost))
        {
            RPC_OnSabotageRequestResolved(requester, attackerTeam, targetTeamIndex, recipeIndex, false);
            return;
        }

        if (!TryAddSabotageOrder(targetTeamIndex, recipeIndex))
        {
            AddMoney(attackerTeam, cost);
            RPC_OnSabotageRequestResolved(requester, attackerTeam, targetTeamIndex, recipeIndex, false);
            return;
        }

        if (sabotageCooldownSeconds > 0f)
        {
            int cooldownUntil = Runner.Tick + Mathf.CeilToInt(sabotageCooldownSeconds * Runner.TickRate);
            if (cooldownUntil > TeamSabotageCooldownUntilTick[attackerTeam])
                TeamSabotageCooldownUntilTick.Set(attackerTeam, cooldownUntil);
        }

        RPC_OnSabotageRequestResolved(requester, attackerTeam, targetTeamIndex, recipeIndex, true);
    }

    private int ResolvePlayerTeam(PlayerRef playerRef)
    {
        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (Player p in players)
        {
            if (p != null && p.Object != null && p.Object.InputAuthority == playerRef)
            {
                return p.TeamIndex;
            }
        }
        return -1;
    }
}
