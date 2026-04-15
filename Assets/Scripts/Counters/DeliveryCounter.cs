using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class DeliveryCounter : BaseCounter
{
    private static readonly Dictionary<int, DeliveryCounter> countersByTeam = new Dictionary<int, DeliveryCounter>();

    public static void ResetStaticData()
    {
        countersByTeam.Clear();
    }

    public static DeliveryCounter GetForTeam(int teamIndex)
    {
        countersByTeam.TryGetValue(teamIndex, out DeliveryCounter counter);
        return counter;
    }

    private int registeredTeamIndex = -1;

    public override void Spawned()
    {
        base.Spawned();
        TryRegisterForTeam();
    }

    public override void Render()
    {
        // On non-host clients, TeamIndex is -1 when Spawned() fires because the
        // networked property hasn't been replicated yet. Re-check every render
        // until the real value arrives so sound-lookups via GetForTeam() work.
        if (registeredTeamIndex == -1 && TeamIndex >= 0)
        {
            TryRegisterForTeam();
        }
    }

    private void TryRegisterForTeam()
    {
        if (TeamIndex < 0) return;
        countersByTeam[TeamIndex] = this;
        registeredTeamIndex = TeamIndex;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (registeredTeamIndex >= 0 && countersByTeam.TryGetValue(registeredTeamIndex, out DeliveryCounter existing) && existing == this)
        {
            countersByTeam.Remove(registeredTeamIndex);
        }
        registeredTeamIndex = -1;
        base.Despawned(runner, hasState);
    }

    public override void Interact(Player player)
    {
        if(player.HasKitchenObject()){
            if(player.GetKitchenObject().TryGetPlate(out PlateKitchenObject plateKitchenObject)){
                if (!IsPlayerAllowed(player)) return;

                if(TeamIndex == -1){
                    DeliveryManager.Instance.DeliverRecipe(this, plateKitchenObject);
                }else if(VersusMatchManager.IsReady){
                    VersusMatchManager.Instance.DeliverRecipe(TeamIndex, plateKitchenObject);
                }

                player.GetKitchenObject().DestroySelf();
            }
        }
    }
}
