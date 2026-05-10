using UnityEngine;

/// <summary>
/// Optional dev/test hooks (place in bootstrap or menu scene). Enable flags in the Inspector only — no runtime UI.
/// Match tuning (max HP, round time, post-submit timer multiplier, resolution phase time) lives here;
/// <see cref="GameManager"/> and <see cref="RoundManager"/> read these when an instance exists, otherwise production fallbacks apply.
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameplayTestManager : MonoBehaviour
{
    public static GameplayTestManager Instance { get; private set; }

    const int kFallbackMaxPlayerHp = 20;
    const float kFallbackRoundTimeLimitSeconds = 15f;
    const float kFallbackRoundTimeSpeedMultiplierAfterAnySubmit = 3f;
    const float kFallbackResolutionTimeLimitSeconds = 12f;

    [Header("Lobby / session")]
    [Tooltip("When Use Set Room Word is enabled, creating a session uses this name instead of PickUniqueRoomWordAsync.")]
    [SerializeField] string _presetRoomWord = "TESTROOM";

    [Tooltip("If enabled, a single client marking ready starts the match (server treats both slots as ready).")]
    [SerializeField] bool _skipBothPlayerReady;

    [Tooltip("If enabled, Create Session uses Preset Room Word without querying for a unique random word.")]
    [SerializeField] bool _useSetRoomWord;

    [Header("Match tuning")]
    [Tooltip("Max HP per player at match start (replaces former GameManager.MaxPlayerHp).")]
    [SerializeField] int _maxPlayerHp = 20;

    [Tooltip("Round phase countdown in seconds (replaces former RoundManager.RoundTimeLimitInSeconds).")]
    [SerializeField] float _roundTimeLimitInSeconds = 15f;

    [Tooltip("Multiplier on round timer delta after any player submits (replaces former RoundManager.RoundTimeSpeedMultiplierAfterAnySubmit).")]
    [SerializeField] float _roundTimeSpeedMultiplierAfterAnySubmit = 3f;

    [Tooltip("Resolution phase countdown in seconds (replaces former RoundManager.ResolutionTimeLimitInSeconds).")]
    [SerializeField] float _resolutionTimeLimitInSeconds = 12f;

    public bool SkipBothPlayerReady => _skipBothPlayerReady;
    public bool UseSetRoomWord => _useSetRoomWord;

    /// <summary>Trimmed preset session name / room word (uppercasing is done at call sites that need it).</summary>
    public string PresetRoomWord => string.IsNullOrWhiteSpace(_presetRoomWord) ? string.Empty : _presetRoomWord.Trim();

    public static int EffectiveMaxPlayerHp =>
        Instance != null ? Instance._maxPlayerHp : kFallbackMaxPlayerHp;

    public static float EffectiveRoundTimeLimitInSeconds =>
        Instance != null ? Instance._roundTimeLimitInSeconds : kFallbackRoundTimeLimitSeconds;

    public static float EffectiveRoundTimeSpeedMultiplierAfterAnySubmit =>
        Instance != null ? Instance._roundTimeSpeedMultiplierAfterAnySubmit : kFallbackRoundTimeSpeedMultiplierAfterAnySubmit;

    public static float EffectiveResolutionTimeLimitInSeconds =>
        Instance != null ? Instance._resolutionTimeLimitInSeconds : kFallbackResolutionTimeLimitSeconds;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("GameplayTestManager: duplicate instance; destroying this one.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
