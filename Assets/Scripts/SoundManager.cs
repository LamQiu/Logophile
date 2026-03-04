using FMOD.Studio;
using FMODUnity;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SoundManager : NetworkBehaviour
{
    public static SoundManager Instance;

    private List<EventInstance> snapshotEventInstances;

    public AudioSource bgmSource;
    public AudioSource sfxSource;

    public AudioClip titleBgmClip;
    public AudioClip gameBgmClip;
    public AudioClip typingClip;
    public AudioClip submitClip;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        //PlayTitleBgm();
    }

    public void PlayTitleBgm()
    {
        if (bgmSource.clip == titleBgmClip && bgmSource.isPlaying) return;

        //musicEventInstance = CreateInstance(musicMainMenu);
        //musicEventInstance.start();
    }

    public void PlayGameBgm()
    {
        if (bgmSource.clip == gameBgmClip && bgmSource.isPlaying) return;

        //musicEventInstance = CreateInstance(musicTyping);
        //musicEventInstance.start();
    }

    public void StopBgm()
    {
        bgmSource.Stop();
    }

    public void PlayTypingSfx()
    {
        //sfxSource.PlayOneShot(typingClip);
    }

    [Rpc(SendTo.Server)]
    public void PlaySubmitSfxServerRpc()
    {
        PlaySubmitSfxClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlaySubmitSfxClientRpc()
    {
        sfxSource.PlayOneShot(submitClip);
    }
}