using System.Collections;
using Michsky.UI.Heat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private ModalWindowManager inputNameModal;
    [SerializeField] private ModalWindowManager roomBrowserModal;
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private TextMeshProUGUI usernameDisplayText;
    [SerializeField] private GameObject invalidNameText;

    private const string PLAYER_NAME_KEY = "PlayerName";

    private Coroutine invalidNameCoroutine;

    private void Awake()
    {
        Time.timeScale = 1f;

        if (NetworkRunnerManager.Instance != null)
        {
            NetworkRunnerManager.Instance.JoinLobby();
        }

        string savedName = PlayerPrefs.GetString(PLAYER_NAME_KEY, "");
        if (usernameDisplayText != null)
        {
            usernameDisplayText.text = string.IsNullOrWhiteSpace(savedName) ? "No Name" : savedName;
        }

        if (invalidNameText != null)
        {
            invalidNameText.SetActive(false);
        }
    }

    public void OnPlayButtonClicked()
    {
        string savedName = PlayerPrefs.GetString(PLAYER_NAME_KEY, "");
        if (!string.IsNullOrWhiteSpace(savedName))
        {
            roomBrowserModal.OpenWindow();
        }
        else
        {
            inputNameModal.OpenWindow();
        }
    }

    public void OnInputNameConfirm()
    {
        string trimmedName = nameInputField.text.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            if (invalidNameCoroutine != null)
            {
                StopCoroutine(invalidNameCoroutine);
            }
            invalidNameCoroutine = StartCoroutine(ShowInvalidNameWarning());
            return;
        }

        PlayerPrefs.SetString(PLAYER_NAME_KEY, trimmedName);
        PlayerPrefs.Save();

        if (usernameDisplayText != null)
        {
            usernameDisplayText.text = trimmedName;
        }

        inputNameModal.CloseWindow();
        roomBrowserModal.OpenWindow();
    }

    public void OnInputNameCancel()
    {
        inputNameModal.CloseWindow();
    }

    private IEnumerator ShowInvalidNameWarning()
    {
        if (invalidNameText != null)
        {
            invalidNameText.SetActive(true);
            yield return new WaitForSeconds(2f);
            invalidNameText.SetActive(false);
        }
    }
}
