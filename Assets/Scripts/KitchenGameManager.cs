using Fusion;
using UnityEngine;
using System;

public class KitchenGameManager : NetworkBehaviour
{
    public static KitchenGameManager Instance {get; private set;}

    public static event EventHandler OnInstanceReady;

    public static bool IsReady =>
        Instance != null && Instance.Object != null && Instance.Object.IsValid;

    public event EventHandler OnStateChanged;

    private enum State{
        WaitingToStart,
        CountdownToStart,
        GamePlaying,
        GameOver,
    }

    [Networked] public int NetworkedState { get; set; }
    [Networked] public float CountdownTimer { get; set; }
    [Networked] public float GamePlayingTimer { get; set; }
    [Networked] private NetworkBool IsInitialized { get; set; }
    [Networked] private float ReturnToLobbyTimer { get; set; }
    [Networked] public int VersusWinningTeamIndex { get; set; } = -1;

    private float gamePlayingTimerMax = 30f;
    private float returnToLobbyDelay = 5f;
    private ChangeDetector changeDetector;

    public static void ResetStaticData(){
        OnInstanceReady = null;
    }

    public override void Spawned(){
        Instance = this;
        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        // Only initialize state on fresh game start, not during host migration
        // (during migration, [Networked] properties are already restored from snapshot)
        if(HasStateAuthority && !IsInitialized){
            NetworkedState = (int)State.WaitingToStart;
            CountdownTimer = 3f;
            IsInitialized = true;
        }

        // Fire after initialization so subscribers (like MapManager.TryStartGame) don't get overwritten
        OnInstanceReady?.Invoke(this, EventArgs.Empty);
    }

    public override void Despawned(NetworkRunner runner, bool hasState){
        if(Instance == this) Instance = null;
    }

    public override void Render(){
        foreach(string propertyName in changeDetector.DetectChanges(this)){
            switch(propertyName){
                case nameof(NetworkedState):
                    OnStateChanged?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
    }

    public override void FixedUpdateNetwork(){
        if(!HasStateAuthority) return;

        State currentState = (State)NetworkedState;
        switch(currentState){
            case State.WaitingToStart:
                break;
            case State.CountdownToStart:
                CountdownTimer -= Runner.DeltaTime;
                if(CountdownTimer < 0){
                    NetworkedState = (int)State.GamePlaying;
                    GamePlayingTimer = gamePlayingTimerMax;
                }
                break;
            case State.GamePlaying:
                if(LobbyManager.GetCachedMatchMode() == LobbyManager.MatchModeVersus){
                    // Versus has no time limit — match ends only when a team loses all hearts.
                    break;
                }
                GamePlayingTimer -= Runner.DeltaTime;
                if(GamePlayingTimer < 0){
                    NetworkedState = (int)State.GameOver;
                    ReturnToLobbyTimer = returnToLobbyDelay;
                }
                break;
            case State.GameOver:
                ReturnToLobbyTimer -= Runner.DeltaTime;
                if(ReturnToLobbyTimer < 0){
                    NetworkRunnerManager.Instance.ReturnToLobby();
                }
                break;
        }
    }

    public void TryStartGame(){
        if(HasStateAuthority && (State)NetworkedState == State.WaitingToStart){
            NetworkedState = (int)State.CountdownToStart;
        }
    }

    public void ForceGameOver(int winningTeamIndex){
        if(!HasStateAuthority) return;
        if((State)NetworkedState == State.GameOver) return;
        VersusWinningTeamIndex = winningTeamIndex;
        NetworkedState = (int)State.GameOver;
        ReturnToLobbyTimer = returnToLobbyDelay;
    }

    public bool IsGamePlaying(){
        return (State)NetworkedState == State.GamePlaying;
    }
    public bool IsWaitingToStart(){
        return (State)NetworkedState == State.WaitingToStart;
    }
    public bool IsCountdownToStartActive(){
        return (State)NetworkedState == State.CountdownToStart;
    }
    public float GetCountdownToStartTimer(){
        return CountdownTimer;
    }
    public bool IsGameOver(){
        return (State)NetworkedState == State.GameOver;
    }
    public float GetGamePlayingTimerNormalized(){
        return 1 - GamePlayingTimer / gamePlayingTimerMax;
    }
}
