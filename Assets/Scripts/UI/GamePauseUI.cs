using UnityEngine;
using UnityEngine.UI;
using System;

public class GamePauseUI : MonoBehaviour
{
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button optionsButton;

    private void Awake() {
        resumeButton.onClick.AddListener(() => {
            if(KitchenGameManager.Instance != null)
                KitchenGameManager.Instance.TogglePauseGame();
        });
        mainMenuButton.onClick.AddListener(() =>{
            Loader.Load(Loader.Scene.MainMenuScene);
        });
        optionsButton.onClick.AddListener(() =>{
            OptionsUI.Instance.Show();
        });
    }

    private void Start() {
        KitchenGameManager.OnInstanceReady += KitchenGameManager_OnInstanceReady;
        if(KitchenGameManager.Instance != null){
            SubscribeToManager();
        }
        Hide();
    }
    private void KitchenGameManager_OnInstanceReady(object sender, EventArgs e){
        SubscribeToManager();
    }
    private void SubscribeToManager(){
        KitchenGameManager.Instance.OnGamePaused += KitchenGameManager_OnGamePaused;
        KitchenGameManager.Instance.OnGameUnpaused += KitchenGameManager_OnGameUnpaused;
    }
    private void KitchenGameManager_OnGameUnpaused(object sender, EventArgs e){
        Hide();
    }
    private void KitchenGameManager_OnGamePaused(object sender, EventArgs e){
        Show();
    }

    private void Show(){
        gameObject.SetActive(true);
    }
    private void Hide(){
        gameObject.SetActive(false);
    }
}
