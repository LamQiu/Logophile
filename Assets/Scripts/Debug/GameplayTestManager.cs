using UnityEngine;

/// <summary>
/// Optional dev/test hooks (place in bootstrap or menu scene). Enable flags in the Inspector only — no runtime UI.
/// </summary>
public class GameplayTestManager : MonoBehaviour
{
    public static GameplayTestManager Instance { get; private set; }

    [Header("Lobby / session")]
    [Tooltip("When Use Set Room Word is enabled, creating a session uses this name instead of PickUniqueRoomWordAsync.")]
    [SerializeField] string _presetRoomWord = "TESTROOM";

    [Tooltip("If enabled, a single client marking ready starts the match (server treats both slots as ready).")]
    [SerializeField] bool _skipBothPlayerReady;

    [Tooltip("If enabled, Create Session uses Preset Room Word without querying for a unique random word.")]
    [SerializeField] bool _useSetRoomWord;

    public bool SkipBothPlayerReady => _skipBothPlayerReady;
    public bool UseSetRoomWord => _useSetRoomWord;

    /// <summary>Trimmed preset session name / room word (uppercasing is done at call sites that need it).</summary>
    public string PresetRoomWord => string.IsNullOrWhiteSpace(_presetRoomWord) ? string.Empty : _presetRoomWord.Trim();

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
