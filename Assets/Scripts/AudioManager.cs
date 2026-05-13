using FMOD.Studio;
using FMODUnity;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class AudioManager : Singleton<AudioManager>
{
    [field: Header("Event Instances")]
    [field: SerializeField] private EventInstance musicEventInstance;

    [Header("Event Instances Management")]
    private List<EventInstance> eventInstances;
    private List<StudioEventEmitter> eventEmitters;

    protected override void Awake()
    {
        base.Awake();
        eventInstances = new List<EventInstance>();
        eventEmitters = new List<StudioEventEmitter>();
    }

    #region AudioManagerFunctions
    public EventInstance CreateInstance(EventReference eventReference)
    {
        EventInstance eventInstance = RuntimeManager.CreateInstance(eventReference);
        eventInstances.Add(eventInstance);
        return eventInstance;
    }

    public void CleanUp()
    {
        foreach (EventInstance eventInstance in eventInstances)
        {
            eventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            eventInstance.release();
        }

        eventInstances.Clear();

        foreach (StudioEventEmitter emitter in eventEmitters)
        {
            emitter.Stop();
        }

        eventEmitters.Clear();
    }

    private void OnDestroy()
    {
        CleanUp();
    }

    #endregion

    #region PlaySoundFunctions

    public void StopMusic()
    {
        musicEventInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        musicEventInstance.release();
    }

    /// <summary>
    /// Entering Prompt Showcase: placeholder hook (currently stops active music). Replace with dedicated FMOD logic later if needed.
    /// Gameplay still starts typing music via <see cref="PlayTypingMusic"/>.
    /// </summary>
    public void StopMusicForPromptShowcase()
    {
        StopMusic();
    }

    public void PlayMainMenuMusic()
    {
        StopMusic();
        musicEventInstance = CreateInstance(FMODEvents.Instance.musicMainMenu);
        musicEventInstance.start();
    }

    public void PlayTypingMusic()
    {
        StopMusic();
        musicEventInstance = CreateInstance(FMODEvents.Instance.musicTyping);
        musicEventInstance.start();
    }

    public void PlayWaitingMusic()
    {
        StopMusic();
        musicEventInstance = CreateInstance(FMODEvents.Instance.musicWaiting);
        musicEventInstance.start();
    }

    public void PlayTypingSFX()
    {
        RuntimeManager.PlayOneShot(FMODEvents.Instance.playerType);
    }

    [Rpc(SendTo.Server)]
    public void PlaySubmitSfxServerRpc()
    {
        PlaySubmitSfxClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlaySubmitSfxClientRpc()
    {
        RuntimeManager.PlayOneShot(FMODEvents.Instance.playerConfirm);
    }

    #endregion
}
