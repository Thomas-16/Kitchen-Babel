using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class SoundManager : MonoBehaviour
{
    private const string PLAYER_PREFS_SOUND_EFFECTS_VOLUME = "SoundEffectsVolume";

    public static SoundManager Instance {get; private set;}

    [SerializeField] private AudioClipRefsSO audioClipRefsSO;

    private float volume = .8f;

    private void Awake() {
        Instance = this;

        volume = PlayerPrefs.GetFloat(PLAYER_PREFS_SOUND_EFFECTS_VOLUME, volume);
    }
    private void Start() {
        DeliveryManager.Instance.OnRecipeSuccess += DeliveryManager_OnRecipeSuccess;
        DeliveryManager.Instance.OnRecipeFailed += DeliveryManager_OnRecipeFailed;
        CuttingCounter.OnAnyCut += CuttingCounter_OnAnyCut;
        Player.Instance.OnPickedSomething += Player_OnPickedSomething;
        BaseCounter.OnAnyObjectPlacedHere += BaseCounter_OnAnyObjectPlacedHere;
        TrashCounter.OnAnyObjectTrashed += TrashCounter_OnAnyObjectTrashed;
    }

    private void TrashCounter_OnAnyObjectTrashed(object sender, EventArgs e){
        PlaySound(audioClipRefsSO.trash, (sender as TrashCounter).transform.position);
    }
    private void BaseCounter_OnAnyObjectPlacedHere(object sender, EventArgs e){
        PlaySound(audioClipRefsSO.objectDrop, (sender as BaseCounter).transform.position);
    }
    private void Player_OnPickedSomething(object sender, EventArgs e){
        PlaySound(audioClipRefsSO.objectPickup, Player.Instance.transform.position);
    }
    private void CuttingCounter_OnAnyCut(object sender, EventArgs e){
        PlaySound(audioClipRefsSO.chop, (sender as CuttingCounter).transform.position);
    }
    private void DeliveryManager_OnRecipeFailed(object sender, EventArgs e){
        DeliveryCounter deliveryCounter = DeliveryCounter.Instance;
        PlaySound(audioClipRefsSO.deliveryFailed, deliveryCounter.transform.position);
    }
    private void DeliveryManager_OnRecipeSuccess(object sender, EventArgs e){
        DeliveryCounter deliveryCounter = DeliveryCounter.Instance;
        PlaySound(audioClipRefsSO.deliverySuccess, deliveryCounter.transform.position);
    }
    private void PlaySound(AudioClip[] audioClipArray, Vector3 position, float volume = 1f){
        PlaySound(audioClipArray[UnityEngine.Random.Range(0,audioClipArray.Length)], position, volume);
    }
    private void PlaySound(AudioClip audioClip, Vector3 position, float volumeMultiplier = 1f){
        AudioSource.PlayClipAtPoint(audioClip, position, volumeMultiplier * this.volume);
    }

    public void PlayFootstepSound(Vector3 position, float volume){
        PlaySound(audioClipRefsSO.footstep, position, volume);
    }
    public void PlayCountDownSound(){
        PlaySound(audioClipRefsSO.warning, Vector3.zero);
    }
    public void PlayWarningSound(Vector3 position){
        PlaySound(audioClipRefsSO.warning, position);
    }

    public void ChangeVolume(){
        volume += .1f;
        if(volume > 1.05f) volume = 0f;

        PlayerPrefs.SetFloat(PLAYER_PREFS_SOUND_EFFECTS_VOLUME, volume);
        PlayerPrefs.Save();
    }
    public float GetVolume(){
        return volume;
    }
}
