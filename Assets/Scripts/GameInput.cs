using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameInput : MonoBehaviour
{
    private const string PLAYERS_PREFS_BINDINGS = "InputBindings";

    public static GameInput Instance {get; private set;}

    public event EventHandler OnInteractAction;
    public event EventHandler OnInteractAlternateAction;
    public event EventHandler OnDashAction;
    public event EventHandler OnBindingRebind;

    public enum Binding{
        Move_Up,
        Move_Down,
        Move_Left,
        Move_Right,
        Interact,
        InteractAlternate,
        Dash,
    }

    private PlayerInputActions playerInputActions;
    private bool interactPressed;
    private bool interactAlternatePressed;
    private bool dashPressed;

    private void Awake() {
        Instance = this;

        playerInputActions = new PlayerInputActions();

        if(PlayerPrefs.HasKey(PLAYERS_PREFS_BINDINGS)){
            playerInputActions.LoadBindingOverridesFromJson(PlayerPrefs.GetString(PLAYERS_PREFS_BINDINGS));
        }

        playerInputActions.Player.Enable();

        playerInputActions.Player.Interact.performed += Interact_performed;
        playerInputActions.Player.InteractAlternate.performed += InteractAlternate_performed;
        playerInputActions.Player.Dash.performed += Dash_performed;
    }
    private void OnDestroy() {
        playerInputActions.Player.Interact.performed -= Interact_performed;
        playerInputActions.Player.InteractAlternate.performed -= InteractAlternate_performed;
        playerInputActions.Player.Dash.performed -= Dash_performed;

        playerInputActions.Dispose();
    }
    private void Dash_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj){
        OnDashAction?.Invoke(this, EventArgs.Empty);
        dashPressed = true;
    }
    private void InteractAlternate_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj){
        OnInteractAlternateAction?.Invoke(this, EventArgs.Empty);
        interactAlternatePressed = true;
    }
    private void Interact_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj){
        OnInteractAction?.Invoke(this, EventArgs.Empty);
        interactPressed = true;
    }

    public bool GetInteractPressed(){
        bool pressed = interactPressed;
        interactPressed = false;
        return pressed;
    }

    public bool GetInteractAlternatePressed(){
        bool pressed = interactAlternatePressed;
        interactAlternatePressed = false;
        return pressed;
    }

    public bool GetDashPressed(){
        bool pressed = dashPressed;
        dashPressed = false;
        return pressed;
    }

    public Vector2 GetMovementVectorNormalized(){
        Vector2 inputVector = playerInputActions.Player.Move.ReadValue<Vector2>();

        inputVector = inputVector.normalized;
        return inputVector;
    }

    public string GetBindingText(Binding binding){
        switch (binding)
        {
            default:
            case Binding.Move_Up:
                return playerInputActions.Player.Move.bindings[1].ToDisplayString();
            case Binding.Move_Down:
                return playerInputActions.Player.Move.bindings[2].ToDisplayString();
            case Binding.Move_Left:
                return playerInputActions.Player.Move.bindings[3].ToDisplayString();
            case Binding.Move_Right:
                return playerInputActions.Player.Move.bindings[4].ToDisplayString();
            case Binding.Interact:
                return playerInputActions.Player.Interact.bindings[0].ToDisplayString();
            case Binding.InteractAlternate:
                return playerInputActions.Player.InteractAlternate.bindings[0].ToDisplayString();
            case Binding.Dash:
                return playerInputActions.Player.Dash.bindings[0].ToDisplayString();
        }
    }
    public void RebindBinding(Binding binding, Action onActionRebound){
        playerInputActions.Player.Disable();

        InputAction inputAction;
        int bindingIndex;

        switch(binding){
            default:
            case Binding.Move_Up:
                inputAction = playerInputActions.Player.Move;
                bindingIndex = 1;
                break;
            case Binding.Move_Down:
                inputAction = playerInputActions.Player.Move;
                bindingIndex = 2;
                break;
            case Binding.Move_Left:
                inputAction = playerInputActions.Player.Move;
                bindingIndex = 3;
                break;
            case Binding.Move_Right:
                inputAction = playerInputActions.Player.Move;
                bindingIndex = 4;
                break;
            case Binding.Interact:
                inputAction = playerInputActions.Player.Interact;
                bindingIndex = 0;
                break;
            case Binding.InteractAlternate:
                inputAction = playerInputActions.Player.InteractAlternate;
                bindingIndex = 0;
                break;
            case Binding.Dash:
                inputAction = playerInputActions.Player.Dash;
                bindingIndex = 0;
                break;
        }
        inputAction.PerformInteractiveRebinding(bindingIndex)
            .OnComplete(callback => {
                callback.Dispose();
                playerInputActions.Player.Enable();
                onActionRebound();

                PlayerPrefs.SetString(PLAYERS_PREFS_BINDINGS, playerInputActions.SaveBindingOverridesAsJson());
                PlayerPrefs.Save();

                OnBindingRebind?.Invoke(this, EventArgs.Empty);
            })
            .Start();
    }
}
