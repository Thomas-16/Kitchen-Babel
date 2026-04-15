using UnityEngine;
using UnityEngine.UI;

public class GamePlayingClockUI : MonoBehaviour
{
    [SerializeField] private Image timerImage;

    private void Update() {
        if(KitchenGameManager.Instance == null) return;
        if(MapManager.Instance != null && MapManager.Instance.IsVersus){
            if(gameObject.activeSelf) gameObject.SetActive(false);
            return;
        }
        if(KitchenGameManager.Instance.IsGameOver()){
            timerImage.fillAmount = 0f;
            return;
        }

        if(!KitchenGameManager.Instance.IsGamePlaying()){
            timerImage.fillAmount = 1f;
            return;
        }

        timerImage.fillAmount = KitchenGameManager.Instance.GetGamePlayingTimerNormalized();
    }
}
