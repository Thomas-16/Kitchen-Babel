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
        timerImage.fillAmount = KitchenGameManager.Instance.GetGamePlayingTimerNormalized();
    }
}
