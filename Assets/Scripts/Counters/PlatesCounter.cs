using Fusion;
using UnityEngine;
using System;

public class PlatesCounter : BaseCounter
{
    public event EventHandler OnPlateSpawned;
    public event EventHandler OnPlateRemoved;

    [SerializeField] private KitchenObjectSO plateKitchenObjectSO;

    [Networked] public int PlatesSpawnAmount { get; set; }

    private float spawnPlateTimer;
    private float spawnPlateTimerMax = 4f;
    private int platesSpawnAmountMax = 4;
    private ChangeDetector changeDetector;
    private int lastKnownPlateCount;

    public override void Spawned(){
        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        lastKnownPlateCount = PlatesSpawnAmount;
    }

    public override void Render(){
        foreach(string propertyName in changeDetector.DetectChanges(this)){
            switch(propertyName){
                case nameof(PlatesSpawnAmount):
                    if(PlatesSpawnAmount > lastKnownPlateCount){
                        OnPlateSpawned?.Invoke(this, EventArgs.Empty);
                    }else if(PlatesSpawnAmount < lastKnownPlateCount){
                        OnPlateRemoved?.Invoke(this, EventArgs.Empty);
                    }
                    lastKnownPlateCount = PlatesSpawnAmount;
                    break;
            }
        }
    }

    public override void FixedUpdateNetwork(){
        if(!HasStateAuthority) return;

        spawnPlateTimer += Runner.DeltaTime;
        if(spawnPlateTimer > spawnPlateTimerMax){
            spawnPlateTimer = 0f;

            if(KitchenGameManager.Instance.IsGamePlaying() && PlatesSpawnAmount < platesSpawnAmountMax){
                PlatesSpawnAmount++;
            }
        }
    }

    public override void Interact(Player player)
    {
        if(!player.HasKitchenObject()){
            if(PlatesSpawnAmount > 0){
                PlatesSpawnAmount--;

                KitchenObject.SpawnKitchenObject(plateKitchenObjectSO, player);
            }
        }
    }
}
