using UnityEngine;
using UnityEngine.InputSystem;
using Michsky.UI.Heat;

public class SettingsWiring : MonoBehaviour
{
    private const string PLAYER_PREFS_BINDINGS = "InputBindings";

    [Header("Visuals")]
    [SerializeField] private GraphicsManager graphicsManager;
    [SerializeField] private HorizontalSelector windowModeSelector;
    [SerializeField] private HorizontalSelector frameRateSelector;
    [SerializeField] private SwitchManager vSyncSwitch;

    [Header("Controls")]
    [SerializeField] private Transform[] bindingItems; // 7 items indexed by GameInput.Binding enum order
    [SerializeField] private GameObject cameraSensitivityItem;

    private PlayerInputActions playerInputActions;
    private ButtonManager[] bindingButtons;

    private static readonly string[] bindingNames = {
        "Move Up", "Move Down", "Move Left", "Move Right",
        "Interact", "Interact Alternate"
    };

    private static readonly int[] frameRateValues = { -1, 30, 60, 144, 240 };

    private void Start()
    {
        WireVisualsTab();
        WireControlsTab();
    }

    private void OnDestroy()
    {
        if (playerInputActions != null)
        {
            playerInputActions.Dispose();
            playerInputActions = null;
        }
    }

    #region Visuals Tab

    private void WireVisualsTab()
    {
        if (windowModeSelector != null)
        {
            windowModeSelector.onValueChanged.AddListener(OnWindowModeChanged);
            ApplyWindowMode(windowModeSelector.index);
        }

        if (frameRateSelector != null)
        {
            frameRateSelector.onValueChanged.AddListener(OnFrameRateChanged);
            ApplyFrameRate(frameRateSelector.index);
        }

        if (vSyncSwitch != null)
        {
            vSyncSwitch.onValueChanged.AddListener(OnVSyncChanged);
            ApplyVSync(vSyncSwitch.isOn);
        }
    }

    private void OnWindowModeChanged(int index)
    {
        ApplyWindowMode(index);
    }

    private void ApplyWindowMode(int index)
    {
        if (graphicsManager == null) return;

        switch (index)
        {
            case 0: graphicsManager.SetWindowBorderless(); break;
            case 1: graphicsManager.SetWindowFullscreen(); break;
            case 2: graphicsManager.SetWindowWindowed(); break;
        }
    }

    private void OnFrameRateChanged(int index)
    {
        ApplyFrameRate(index);
    }

    private void ApplyFrameRate(int index)
    {
        if (graphicsManager == null) return;
        if (index >= 0 && index < frameRateValues.Length)
        {
            graphicsManager.SetFrameRate(frameRateValues[index]);
        }
    }

    private void OnVSyncChanged(bool value)
    {
        ApplyVSync(value);
    }

    private void ApplyVSync(bool value)
    {
        if (graphicsManager == null) return;
        graphicsManager.SetVSync(value);
    }

    #endregion

    #region Controls Tab

    private void WireControlsTab()
    {
        if (cameraSensitivityItem != null)
        {
            cameraSensitivityItem.SetActive(false);
        }

        if (bindingItems == null || bindingItems.Length == 0) return;

        playerInputActions = new PlayerInputActions();
        if (PlayerPrefs.HasKey(PLAYER_PREFS_BINDINGS))
        {
            playerInputActions.LoadBindingOverridesFromJson(PlayerPrefs.GetString(PLAYER_PREFS_BINDINGS));
        }

        bindingButtons = new ButtonManager[bindingItems.Length];

        for (int i = 0; i < bindingItems.Length; i++)
        {
            if (bindingItems[i] == null) continue;

            ButtonManager binding1 = bindingItems[i].Find("Binding 1")?.GetComponent<ButtonManager>();
            ButtonManager binding2 = bindingItems[i].Find("Binding 2")?.GetComponent<ButtonManager>();

            if (binding2 != null)
            {
                binding2.gameObject.SetActive(false);
            }

            if (binding1 != null)
            {
                bindingButtons[i] = binding1;
                GameInput.Binding binding = (GameInput.Binding)i;
                binding1.SetText(GetBindingDisplayText(binding));

                int capturedIndex = i;
                binding1.onClick.AddListener(() => StartRebind(capturedIndex));
            }
        }
    }

    private void StartRebind(int index)
    {
        GameInput.Binding binding = (GameInput.Binding)index;
        ButtonManager button = bindingButtons[index];

        if (button != null)
        {
            button.SetText("...");
        }

        InputAction inputAction;
        int bindingIndex;
        GetInputActionAndIndex(binding, out inputAction, out bindingIndex);

        playerInputActions.Player.Disable();
        inputAction.PerformInteractiveRebinding(bindingIndex)
            .OnComplete(callback =>
            {
                callback.Dispose();
                playerInputActions.Player.Enable();

                PlayerPrefs.SetString(PLAYER_PREFS_BINDINGS, playerInputActions.SaveBindingOverridesAsJson());
                PlayerPrefs.Save();

                if (button != null)
                {
                    button.SetText(GetBindingDisplayText(binding));
                }
            })
            .Start();
    }

    private string GetBindingDisplayText(GameInput.Binding binding)
    {
        InputAction inputAction;
        int bindingIndex;
        GetInputActionAndIndex(binding, out inputAction, out bindingIndex);
        return inputAction.bindings[bindingIndex].ToDisplayString();
    }

    private void GetInputActionAndIndex(GameInput.Binding binding, out InputAction inputAction, out int bindingIndex)
    {
        switch (binding)
        {
            default:
            case GameInput.Binding.Move_Up:
                inputAction = playerInputActions.Player.Move;
                bindingIndex = 1;
                break;
            case GameInput.Binding.Move_Down:
                inputAction = playerInputActions.Player.Move;
                bindingIndex = 2;
                break;
            case GameInput.Binding.Move_Left:
                inputAction = playerInputActions.Player.Move;
                bindingIndex = 3;
                break;
            case GameInput.Binding.Move_Right:
                inputAction = playerInputActions.Player.Move;
                bindingIndex = 4;
                break;
            case GameInput.Binding.Interact:
                inputAction = playerInputActions.Player.Interact;
                bindingIndex = 0;
                break;
            case GameInput.Binding.InteractAlternate:
                inputAction = playerInputActions.Player.InteractAlternate;
                bindingIndex = 0;
                break;
        }
    }

    #endregion
}
