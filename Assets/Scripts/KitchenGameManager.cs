using Fusion;
using UnityEngine;
using System;

public class KitchenGameManager : NetworkBehaviour
{
    public static KitchenGameManager Instance {get; private set;}

    public static event EventHandler OnInstanceReady;

    public event EventHandler OnStateChanged;
    public event EventHandler OnGamePaused;
    public event EventHandler OnGameUnpaused;

    private enum State{
        WaitingToStart,
        CountdownToStart,
        GamePlaying,
        GameOver,
    }

    [Networked] public int NetworkedState { get; set; }
    [Networked] public float CountdownTimer { get; set; }
    [Networked] public float GamePlayingTimer { get; set; }
    [Networked] public NetworkBool IsPaused { get; set; }
    [Networked] private NetworkBool IsInitialized { get; set; }

    private float gamePlayingTimerMax = 60f;
    private ChangeDetector changeDetector;
    private bool localIsPaused;

    public static void ResetStaticData(){
        OnInstanceReady = null;
    }

    public override void Spawned(){
        Instance = this;
        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        OnInstanceReady?.Invoke(this, EventArgs.Empty);

        // Only initialize state on fresh game start, not during host migration
        // (during migration, [Networked] properties are already restored from snapshot)
        if(HasStateAuthority && !IsInitialized){
            NetworkedState = (int)State.WaitingToStart;
            CountdownTimer = 3f;
            IsInitialized = true;
        }

        // Only host listens to pause input
        if(HasStateAuthority){
            GameInput.Instance.OnPausedAction += GameInput_OnPausedAction;
        }
    }

    private void GameInput_OnPausedAction(object sender, EventArgs e){
        TogglePauseGame();
    }

    public override void Render(){
        foreach(string propertyName in changeDetector.DetectChanges(this)){
            switch(propertyName){
                case nameof(NetworkedState):
                    OnStateChanged?.Invoke(this, EventArgs.Empty);
                    break;
                case nameof(IsPaused):
                    if(IsPaused){
                        OnGamePaused?.Invoke(this, EventArgs.Empty);
                    }else{
                        OnGameUnpaused?.Invoke(this, EventArgs.Empty);
                    }
                    break;
            }
        }
    }

    public override void FixedUpdateNetwork(){
        if(!HasStateAuthority) return;
        if(IsPaused) return;

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
                GamePlayingTimer -= Runner.DeltaTime;
                if(GamePlayingTimer < 0){
                    NetworkedState = (int)State.GameOver;
                }
                break;
            case State.GameOver:
                break;
        }
    }

    public void TryStartGame(){
        if(HasStateAuthority && (State)NetworkedState == State.WaitingToStart){
            NetworkedState = (int)State.CountdownToStart;
        }
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
    public void TogglePauseGame(){
        if(!HasStateAuthority) return;
        IsPaused = !IsPaused;
    }
}
