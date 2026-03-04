using Audio;
using FMODUnity;
using UnityEngine;

public class FMODEvents : Singleton<FMODEvents>
{
    [field: Header("FMOD Events")]

    [field: Header("Music")]
    [field: SerializeField] public EventReference musicMainMenu;
    [field: SerializeField] public EventReference musicTyping;
    [field: SerializeField] public EventReference musicWaiting;

    [field: Header("SFX")]
    [field: SerializeField] public EventReference playerType;
    [field: SerializeField] public EventReference playerConfirm;
}
