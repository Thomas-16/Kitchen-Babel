using UnityEngine;
using UnityEngine.Audio;
using System;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance {get; private set;}

    [SerializeField] private AudioClipRefsSO audioClipRefsSO;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    private void Awake() {
        Instance = this;
    }
    private void Start() {
        DeliveryManager.OnInstanceReady += DeliveryManager_OnInstanceReady;
        if(DeliveryManager.Instance != null){
            SubscribeToDeliveryManager();
        }

        CuttingCounter.OnAnyCut += CuttingCounter_OnAnyCut;
        Player.OnAnyPickedSomething += Player_OnAnyPickedSomething;
        BaseCounter.OnAnyObjectPlacedHere += BaseCounter_OnAnyObjectPlacedHere;
        TrashCounter.OnAnyObjectTrashed += TrashCounter_OnAnyObjectTrashed;
    }
    private void DeliveryManager_OnInstanceReady(object sender, EventArgs e){
        SubscribeToDeliveryManager();
    }
    private void SubscribeToDeliveryManager(){
        DeliveryManager.Instance.OnRecipeSuccess += DeliveryManager_OnRecipeSuccess;
        DeliveryManager.Instance.OnRecipeFailed += DeliveryManager_OnRecipeFailed;
    }

    private void TrashCounter_OnAnyObjectTrashed(object sender, EventArgs e){
        PlaySound(audioClipRefsSO.trash, (sender as TrashCounter).transform.position);
    }
    private void BaseCounter_OnAnyObjectPlacedHere(object sender, EventArgs e){
        PlaySound(audioClipRefsSO.objectDrop, (sender as BaseCounter).transform.position);
    }
    private void Player_OnAnyPickedSomething(object sender, EventArgs e){
        Player player = sender as Player;
        if(player != null){
            PlaySound(audioClipRefsSO.objectPickup, player.transform.position);
        }
    }
    private void CuttingCounter_OnAnyCut(object sender, EventArgs e){
        PlaySound(audioClipRefsSO.chop, (sender as CuttingCounter).transform.position);
    }
    private void DeliveryManager_OnRecipeFailed(object sender, DeliveryManager.DeliveryEventArgs e){
        DeliveryCounter deliveryCounter = e.DeliveryCounter;
        if(deliveryCounter == null) return;
        PlaySound(audioClipRefsSO.deliveryFailed, deliveryCounter.transform.position);
    }
    private void DeliveryManager_OnRecipeSuccess(object sender, DeliveryManager.DeliveryEventArgs e){
        DeliveryCounter deliveryCounter = e.DeliveryCounter;
        if(deliveryCounter == null) return;
        PlaySound(audioClipRefsSO.deliverySuccess, deliveryCounter.transform.position);
    }
    private void PlaySound(AudioClip[] audioClipArray, Vector3 position, float volumeMultiplier = 1f){
        PlaySound(audioClipArray[UnityEngine.Random.Range(0,audioClipArray.Length)], position, volumeMultiplier);
    }
    private void PlaySound(AudioClip audioClip, Vector3 position, float volumeMultiplier = 1f){
        GameObject soundObject = new GameObject("SFX_OneShot");
        soundObject.transform.position = position;
        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.clip = audioClip;
        source.volume = volumeMultiplier;
        source.spatialBlend = 1f;
        if (sfxMixerGroup != null)
        {
            source.outputAudioMixerGroup = sfxMixerGroup;
        }
        source.Play();
        Destroy(soundObject, audioClip.length + 0.1f);
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
}
