using UnityEngine;

public class WebGLAudioUnlock : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    private bool unlocked;

    private void Awake()
    {
        AudioListener.pause = true;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (unlocked) return;
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0)
        {
            AudioListener.pause = false;
            unlocked = true;
        }
    }
#endif
}
