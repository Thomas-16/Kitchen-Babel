using System;
using Fusion;
using UnityEngine;

public class SabotageCounter : BaseCounter, IHasProgress
{
    public static event Action<SabotageCounter> OnAnyLocalOpen;
    public event EventHandler<IHasProgress.OnProgressChangedEventArgs> OnProgressChanged;

    private float lastEmittedNormalized = -1f;

    public override void Interact(Player player)
    {
        if (player == null || player.Object == null) return;
        RPC_OpenForPlayer(player.Object.InputAuthority);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OpenForPlayer(PlayerRef target)
    {
        if (Runner.LocalPlayer == target)
        {
            OnAnyLocalOpen?.Invoke(this);
        }
    }

    private void Update()
    {
        float normalized = ComputeBlockedNormalized();
        if (!Mathf.Approximately(normalized, lastEmittedNormalized))
        {
            lastEmittedNormalized = normalized;
            OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs
            {
                progressNormalized = normalized,
            });
        }
    }

    private float ComputeBlockedNormalized()
    {
        if (TeamIndex < 0 || TeamIndex >= VersusMatchManager.TeamCount) return 0f;
        if (!VersusMatchManager.IsReady) return 0f;

        VersusMatchManager manager = VersusMatchManager.Instance;
        NetworkRunner runner = manager.Runner;
        if (runner == null) return 0f;

        int opponentTeam = 1 - TeamIndex;
        int nowTick = runner.Tick;

        int localRemaining = Mathf.Max(0, manager.TeamSabotageCooldownUntilTick[TeamIndex] - nowTick);
        int shieldRemaining = Mathf.Max(0, manager.TeamSabotageShieldUntilTick[opponentTeam] - nowTick);
        int remainingTicks = Mathf.Max(localRemaining, shieldRemaining);
        if (remainingTicks <= 0) return 0f;

        int totalTicks = 0;
        if (localRemaining >= shieldRemaining)
        {
            totalTicks = Mathf.CeilToInt(manager.SabotageCooldownSeconds * runner.TickRate);
        }
        else
        {
            totalTicks = Mathf.CeilToInt(manager.HeartLossSabotageShieldSeconds * runner.TickRate);
        }
        if (totalTicks <= 0) return 0f;

        return Mathf.Clamp((float)remainingTicks / totalTicks, 0f, 0.9999f);
    }
}
