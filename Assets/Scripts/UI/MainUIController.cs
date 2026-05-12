using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using DG.Tweening;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class MainUIController : MonoBehaviour
{
    const float EquilateralTriangleAltitude = 0.8660254f;

    const string PromptSharedGroupName = "PromptSharedGroup";
    const string DesignOverlayGroupName = "Design";
    const string PromptCalibrationOverlayName = "PromptCalibrationOverlay";
    const string DeprecatedPromptShowcaseRootName = "PromptShowcaseRoot";
    const string GameplayElementsGroupName = "GameplayElementsGroup";
    const string DeprecatedGameplayRootName = "GameplayRoot";
    const string RoundResultElementsGroupName = "RoundResultElementsGroup";
    const int RoundResultLayoutVersion = 17;

#if UNITY_EDITOR
    const string MainUIPrefabPath = "Assets/Prefabs/UI/MainUI.prefab";
    static bool s_buildingLoadedPrefabAsset;
    static bool s_prefabBuildScheduled;

    [UnityEditor.InitializeOnLoadMethod]
    static void ScheduleMainUIPrefabBuildAfterReload()
    {
        ScheduleMainUIPrefabBuild();
    }
#endif

    public enum MainUIState
    {
        Start,
        Tutorial,
        RoomId,
        Waiting,
        Loading,
        PromptShowcase,
        Gameplay,
        RoundResult,
        GameEnd
    }

    [System.Serializable]
    public class StateCanvasGroupSet
    {
        public MainUIState state;
        public CanvasGroup[] visibleGroups;
        public CanvasGroup[] manuallyRevealedGroups;
    }

    [System.Serializable]
    public class StateDesignReference
    {
        public MainUIState state;
        public int referenceImageIndex = -1;
    }

    [System.Serializable]
    public class LineTarget
    {
        public RectTransform rect;
        public Vector2 tutorialAnchoredPos;
        public Vector2 tutorialSizeDelta;
        public Vector2 roomIdAnchoredPos;
        public Vector2 roomIdSizeDelta;
        public Vector2 waitingAnchoredPos;
        public Vector2 waitingSizeDelta;
        [HideInInspector] public Vector2 initialAnchoredPos;
        [HideInInspector] public Vector2 initialSizeDelta;
    }

    [System.Serializable]
    public class RectTransformTweenTarget
    {
        public RectTransform rect;
        public bool tweenAnchoredPosition = true;
        public Vector2 anchoredPosition;
        public bool tweenSizeDelta = true;
        public Vector2 sizeDelta;
    }

    [System.Serializable]
    public class CanvasGroupTweenTarget
    {
        public CanvasGroup group;
        public float targetAlpha = 1f;
        public bool interactable = true;
        public bool blocksRaycasts = true;
    }

    [System.Serializable]
    public class TypewriterRevealTarget
    {
        public TypewriterEffect typewriter;
        public CanvasGroup group;
        public float delay;
    }

    [System.Serializable]
    public class StateAnimationSet
    {
        public MainUIState state;
        public RectTransformTweenTarget[] rectTargets;
        public CanvasGroupTweenTarget[] canvasGroupTargets;
        public TypewriterRevealTarget[] typewriterTargets;
    }

    [Header("Intro Sequence")]
    [SerializeField] TypewriterEffect _titleTypewriter;
    [SerializeField] TypewriterEffect _hintTypewriter;
    [SerializeField] HintCycler _hintCycler;
    [SerializeField] string _startHintText = "Type \"create\" to create room";
    [SerializeField] float _introGapSeconds = 0.3f;
    [SerializeField] bool _playIntroOnStart = true;

    [Header("CMYK Bar - References")]
    [SerializeField] RectTransform _cmykBar;
    [SerializeField] LayoutElement _layoutM;
    [SerializeField] LayoutElement _layoutY;
    [SerializeField] LayoutElement _layoutC;
    [SerializeField] LayoutElement _layoutK;
    [SerializeField] ParallelogramGraphic _graphicM;
    [SerializeField] ParallelogramGraphic _graphicY;
    [SerializeField] ParallelogramGraphic _graphicC;
    [SerializeField] ParallelogramGraphic _graphicK;

    [Header("Tutorial Target Values")]
    [SerializeField] Vector2 _barTutorialAnchoredPos = new Vector2(0f, 0f);
    [SerializeField] Vector2 _barTutorialSize = new Vector2(1600f, 600f);
    [SerializeField] float _stripeNarrowWidth = 12f;
    [SerializeField] float _cmykShapePhaseRatio = 0.48f;
    // K auto-fills remaining width via LayoutElement.flexibleWidth on the K GameObject.

    [Header("Animation")]
    [SerializeField] float _duration = 0.8f;
    [SerializeField] Ease _ease = Ease.InOutQuad;

    [Header("Start To Join Transition")]
    [SerializeField] float _startJoinSweepOutDuration = 0.45f;
    [SerializeField] float _startJoinSweepReturnDuration = 0.35f;
    [SerializeField] float _startJoinSweepExtraLeft = 24f;
    [SerializeField] Ease _startJoinSweepEase = Ease.InOutCubic;

    [Header("Resolution Lock")]
    [SerializeField] bool _lockResolution = true;
    [SerializeField] Vector2Int _lockedResolution = new Vector2Int(1920, 1080);
    [SerializeField] FullScreenMode _lockedFullScreenMode = FullScreenMode.Windowed;

    [Header("State Visibility")]
    [SerializeField] StateCanvasGroupSet[] _stateGroups;
    [SerializeField] MainUIState _currentState = MainUIState.Start;
    [SerializeField] bool _forceSingleLineTextOverflow = true;
    [Header("Standard Spacing")]
    [SerializeField] float _standardUiGap = 14f;
    [SerializeField] float _playerIndicatorDownOffset = 8f;

    /// <summary>Lobby / flow state for the single MainUI canvas (e.g. <see cref="UIManager"/> gating main-menu commands).</summary>
    public MainUIState CurrentState => _currentState;
    [SerializeField, HideInInspector] int _roundResultLayoutVersion;
    [SerializeField] float _fadeOutDuration = 0.4f;

    [Header("Post-Waiting State Animations")]
    [SerializeField] StateAnimationSet[] _stateAnimations;
    [SerializeField] float _postWaitingRevealDelayAfterMotion = 0.1f;
    [SerializeField] float _postWaitingRevealStagger = 0.1f;

    [Header("Tutorial Transition - Input Field")]
    [SerializeField] TMP_InputField _inputField;
    /// <summary>Same field used for room code flow and gameplay; wire <see cref="UI.UIManager"/> here when using Main UI for gameplay.</summary>
    public TMP_InputField SharedAnswerInputField => _inputField;
    [SerializeField] RectTransform _inputFieldRect;
    [SerializeField] CanvasGroup _inputFieldContentGroup;
    [SerializeField] float _inputFieldTutorialHeight = 12f;

    [Header("Tutorial Transition - Decorative Lines")]
    [SerializeField] LineTarget[] _decorativeLines;

    [Header("Tutorial Transition - Fade In")]
    [SerializeField] TypewriterEffect _tutorialTitleTypewriter;
    [SerializeField] CanvasGroup _tutorialTitleGroup;
    [SerializeField] CanvasGroup _pressSpaceGroup;
    [SerializeField] float _pressSpaceFadeDuration = 0.3f;
    [SerializeField] float _tutorialTitleDelay = 0.4f;
    [SerializeField] float _pressSpaceGapAfterTitle = 0.2f;

    [Header("Room ID Transition - CMYK Bar")]
    [SerializeField] Vector2 _barRoomIdAnchoredPos;
    [SerializeField] Vector2 _barRoomIdSize;
    [SerializeField] float _roomIdSkew = 60f;
    [SerializeField] float _roomIdMWidth;
    [SerializeField] float _roomIdYWidth;
    [SerializeField] float _roomIdCWidth;

    [Header("Room ID Transition - Input Field")]
    [SerializeField] Vector2 _inputFieldRoomIdSize;
    [SerializeField] TMP_Text _inputFieldPlaceholderText;
    [SerializeField] string _roomIdPlaceholder = "create / join";
    [SerializeField] Color _inputFieldPlaceholderColor = new Color(0.25882354f, 0.25490198f, 0.25490198f, 0.5f);

    [Header("Room ID Transition - Fade In")]
    [SerializeField] TypewriterEffect _roomIdTitleTypewriter;
    [SerializeField] CanvasGroup _roomIdTitleGroup;
    [SerializeField] TypewriterEffect _roomIdHintTypewriter;
    [SerializeField] CanvasGroup _roomIdHintGroup;
    [SerializeField] string _roomIdHintText = "Type room id to join";
    [SerializeField] float _roomIdTitleDelay = 0.4f;
    [SerializeField] float _roomIdHintGapAfterTitle = 0.2f;

    [Header("Waiting Transition - InputField")]
    [SerializeField] string _waitingPlaceholder = "ready";

    [Header("Waiting Transition - Black Panel")]
    [SerializeField] RectTransform _waitingPanel;
    [SerializeField] Vector2 _waitingPanelStartAnchoredPos;
    [SerializeField] Vector2 _waitingPanelStartSize;
    [SerializeField] Vector2 _waitingPanelTargetSize;
    [SerializeField] float _waitingPanelYOffset = 24f;
    [SerializeField] float _waitingPanelRevealDelay = 0.15f;
    [SerializeField] float _waitingPanelRevealDuration = 0.3f;

    [Header("Waiting Transition - Display Content")]
    [SerializeField] TypewriterEffect _waitingTitleTypewriter;
    [SerializeField] CanvasGroup _waitingTitleGroup;
    [SerializeField] TypewriterEffect _waitingRoomIdTypewriter;
    [SerializeField] CanvasGroup _waitingRoomIdGroup;
    [SerializeField] TypewriterEffect _waitingHintTypewriter;
    [SerializeField] CanvasGroup _waitingHintGroup;
    [SerializeField] CanvasGroup _waitingP1Group;
    [SerializeField] CanvasGroup _waitingP2Group;
    [SerializeField] float _waitingContentGapAfterReveal = 0.1f;
    [SerializeField] float _waitingContentStagger = 0.1f;
    [SerializeField] float _waitingContentFadeDuration = 0.3f;

    [Header("Loading Transition - White Field")]
    [SerializeField] RectTransform _loadingScreenRect;
    [SerializeField] Image _loadingScreenImage;
    [SerializeField] CanvasGroup _loadingScreenGroup;
    [SerializeField] float _loadingWipeDuration = 1.2f;
    [SerializeField] Ease _loadingWipeEase = Ease.OutQuad;
    [SerializeField] float _loadingAutoPromptDelay = 2f;

    [Header("Shared Prompt Elements")]
    [FormerlySerializedAs("_promptShowcaseRoot")]
    [FormerlySerializedAs("_promptSharedRoot")]
    [SerializeField] RectTransform _promptSharedGroupRect;
    [FormerlySerializedAs("_promptShowcaseGroup")]
    [SerializeField] CanvasGroup _promptSharedGroup;
    [FormerlySerializedAs("_promptShowcaseBackground")]
    [SerializeField] Image _promptSharedBackground;
    [SerializeField] RectTransform _promptPromptMask;
    [SerializeField] RectTransform _promptBannedMask;
    [SerializeField] TMP_Text _promptTitleText;
    [SerializeField] TMP_Text _promptBannedText;
    [SerializeField] string _promptText = "start with \"a\"";
    [SerializeField] string _promptMaskText = "start with";
    [SerializeField] string _promptMaskBannedTextValue = "banned letter";
    [SerializeField] string _promptBannedLetters = "i";
    bool _awaitingPromptFromServerWhileLoading;
    bool _promptReceivedFromServer;
    bool _promptShowcaseTransitionStarted;
    [SerializeField] float _minLoadingHoldSecondsBeforePromptShowcase = 0.15f;
    float _loadingHoldStartUnscaledTime = -1f;
    Coroutine _deferredPromptShowcaseCoroutine;
    bool _promptShowcaseFinishedNotified;
    /// <summary>Set during <see cref="PreparePromptShowcaseStart"/> for the active Loading→Showcase tween + <see cref="SetPromptTextForReveal"/>.</summary>
    bool _showBannedLettersInActivePromptShowcase;

    [Tooltip("Legacy hold value for the old resolution Loading screen; Gameplay now stays visible until the input-field wipe starts.")]
    [SerializeField] float _minLoadingHoldSecondsBeforeRoundResult = 0.15f;
    const float k_resolutionHpSyncTimeoutSeconds = 5f;
    bool _awaitingResolutionScoresWhileLoading;
    string _pendingResolutionHostAnswer;
    string _pendingResolutionClientAnswer;
    int _pendingResolutionHostHpTarget;
    int _pendingResolutionClientHpTarget;
    bool _pendingResolutionHostAnswerLetterEligible;
    bool _pendingResolutionClientAnswerLetterEligible;
    Coroutine _deferredResolutionRoundResultCoroutine;
    [Header("Debug")]
    [SerializeField] bool _debugSharedCommandInput;
    [SerializeField] bool _debugPromptFlow;
    [Tooltip("Logs shared answer TMP input CanvasGroups (shell + Text Area), parent-chain CanvasGroup alphas, and visibility formula when entering Gameplay / configuring input. Enable on MainUI prefab instance while reproducing invisible input.")]
    [SerializeField] bool _debugGameplaySharedInputVisibility;
    [SerializeField] Color _promptPaperColor = new Color(1f, 0.9882353f, 0.96862745f, 1f);
    [SerializeField] Color _promptInkColor = new Color(0.14509805f, 0.14509805f, 0.14509805f, 1f);
    [SerializeField] Color _promptMaskTitleColor = new Color(0.93333334f, 0.91764706f, 0.89411765f, 1f);
    [SerializeField] Color _promptMaskBannedTextColor = new Color(1f, 0.9882353f, 0.96862745f, 1f);
    [SerializeField] Color _promptBannedLetterColor = new Color(0.92156863f, 0f, 0.54509807f, 1f);
    [SerializeField] float _promptMaskEnterDuration = 0.8f;
    [SerializeField] float _promptTextFadeDuration = 0.35f;
    [SerializeField] float _promptHoldBeforeRevealSeconds = 2f;
    [SerializeField] float _promptMaskRevealDuration = 1f;
    [SerializeField] Ease _promptMaskRevealEase = Ease.OutCubic;
    [SerializeField] float _promptAutoGameplayDelay = 2f;

    [Header("Gameplay-Only Elements")]
    [FormerlySerializedAs("_gameplayRoot")]
    [FormerlySerializedAs("_gameplayElementsRoot")]
    [SerializeField] RectTransform _gameplayElementsGroupRect;
    [FormerlySerializedAs("_gameplayGroup")]
    [SerializeField] CanvasGroup _gameplayElementsGroup;
    [SerializeField] Image _gameplayBackground;
    [SerializeField] RectTransform _gameplayTimerBar;
    [SerializeField] string _gameplayInputPlaceholder = "";
    [SerializeField] TMP_Text _gameplayP1Text;
    [SerializeField] TMP_Text _gameplayP2Text;
    [SerializeField] private TMP_Text _gameplayHintText;
    [SerializeField] Image _gameplayP1Box;
    [SerializeField] Image _gameplayP2Box;
    [SerializeField] RectTransform _gameplayP1LetterGroup;
    [SerializeField] RectTransform _gameplayP2LetterGroup;
    [SerializeField] Vector2 _gameplayLetterBlockSize = new Vector2(21f, 50f);
    [SerializeField] float _gameplayLetterBlockSpacing = 42f;
    [SerializeField] Color _gameplayLetterNeutralColor = new Color(0.52156866f, 0.52156866f, 0.52156866f, 1f);
    [SerializeField] Color _gameplayP1LetterColor = new Color(1f, 0.94509804f, 0.015686275f, 1f);
    [SerializeField] Color _gameplayP2LetterColor = new Color(0f, 0.68235296f, 0.93333334f, 1f);
    [SerializeField] Color _gameplayBannedFlashColor = new Color(1f, 0f, 0f, 1f);
    [SerializeField] float _gameplayBannedFlashDuration = 0.35f;
    [SerializeField] float _gameplayFadeDuration = 0.35f;
    [SerializeField] float _gameplaySlideOffset = 36f;
    [SerializeField] float _gameplayTimerDrainPreviewDuration = 10f;
    [SerializeField] float _gameplayTimerPreviewWidth = 0f;
    [SerializeField] Color _gameplayTimerBarNormalColor = new Color(0.52156866f, 0.52156866f, 0.52156866f, 1f);
    [SerializeField] Color _gameplayTimerBarAcceleratedColor = new Color(1f, 0.45f, 0.2f, 1f);
    string _gameplayP1Word = "";
    string _gameplayP2Word = "";
    int _gameplayP1SyncedLetterCount = -1;
    int _gameplayP2SyncedLetterCount = -1;
    bool _gameplayInputListenerRegistered;
    RoundManager _roundManagerAccelVisualSubscription;

    const float GameplayTimerBarFullWidth = 1620f;
    const float GameplayLetterRowOwnerScaleY = 2.5f;

    [Header("Round Result Elements")]
    [SerializeField] RectTransform _roundResultElementsGroupRect;
    [SerializeField] CanvasGroup _roundResultElementsGroup;
    [SerializeField] Image _roundResultPanel;
    [SerializeField] TMP_Text _roundResultP1WordText;
    [SerializeField] TMP_Text _roundResultP2WordText;
    [SerializeField] TMP_Text _roundResultDeathLabelText;
    [SerializeField] TMP_Text _roundResultP1ScoreText;
    [SerializeField] TMP_Text _roundResultP2ScoreText;
    [SerializeField] RectTransform _roundResultP1ScoreBar;
    [SerializeField] RectTransform _roundResultP2ScoreBar;
    [SerializeField] RectTransform _roundResultDeathLineGroup;
    [SerializeField] string _roundResultP1Word = "average";
    [SerializeField] string _roundResultP2Word = "aromatic";
    [SerializeField] int _roundResultP1Score = 21;
    [SerializeField] int _roundResultP2Score = 18;
    bool _roundResultHostAnswerLetterEligible = true;
    bool _roundResultClientAnswerLetterEligible = true;
    [SerializeField] Color _roundResultTextColor = new Color(0.93333334f, 0.91764706f, 0.89411765f, 1f);
    [SerializeField] Color _roundResultMutedTextColor = new Color(0.53333336f, 0.5254902f, 0.5137255f, 1f);
    [SerializeField] Color _roundResultTopStripeColor = new Color(0.38823533f, 0.3803922f, 0.37647063f, 1f);
    [SerializeField] Color _roundResultMiddleStripeColor = new Color(0.53333336f, 0.52156866f, 0.5019608f, 1f);
    [SerializeField] Color _roundResultBottomStripeColor = new Color(0.74509805f, 0.73333335f, 0.7137255f, 1f);
    [SerializeField] float _roundResultTransitionDuration = 0.45f;
    [SerializeField] float _roundResultFadeGameplayDuration = 0.3f;
    [SerializeField] float _roundResultPanelMorphDuration = 0.55f;
    [SerializeField] float _roundResultStripeRevealDuration = 0.35f;
    [SerializeField] float _roundResultContentFadeDuration = 0.35f;
    [SerializeField] float _roundResultPromptCharactersPerSecond = 30f;
    [Tooltip("Bar width when HP equals GameManager.MaxPlayerHp (P1/P2 share the same scale).")]
    [SerializeField] float _roundResultScoreBarFullWidth = 217f;
    [SerializeField] float _roundResultScoreBarHeight = 45f;
    [Tooltip("Round result HP label: left edge X = score bar right + x; center Y = score bar center + y (same space as bar anchoredPosition).")]
    [SerializeField] Vector2 _roundResultScoreTextOffsetFromBarEnd = new Vector2(13f, 0f);
    [SerializeField] float _roundResultContentStagger = 0.05f;

    [Header("Game End Elements")]
    [SerializeField] string _gameEndTitle = "you win";

    [Header("Design Calibration Overlay")]
    [SerializeField] bool _showPromptCalibrationOverlay;
    [SerializeField] bool _showExistingDesignReferenceImages = true;
    [SerializeField] bool _autoSelectDesignReferenceForCurrentState = true;
    [SerializeField] int _promptCalibrationReferenceImageIndex;
    [SerializeField] StateDesignReference[] _stateDesignReferences;
    [SerializeField] Color _promptTitleBoundsOverlayColor = new Color(1f, 0.92f, 0f, 0.18f);
    [SerializeField] Color _promptBannedBoundsOverlayColor = new Color(0f, 0.7f, 1f, 0.18f);
    [SerializeField] RectTransform _designOverlayRoot;
    [SerializeField] RectTransform _promptCalibrationOverlayRoot;
    [SerializeField] RectTransform _promptTitleVisualBoundsOverlay;
    [SerializeField] RectTransform _promptBannedVisualBoundsOverlay;

    [Header("Initial State (capture via context menu)")]
    [SerializeField] Vector2 _initBarPos;
    [SerializeField] Vector2 _initBarSize;
    [SerializeField] float _initMWidth;
    [SerializeField] float _initYWidth;
    [SerializeField] float _initCWidth;
    [SerializeField] float _initSkew = 60f;
    [SerializeField] Vector2 _initInputFieldAnchoredPos;
    [SerializeField] Vector2 _initInputFieldSize;
    [SerializeField] bool _initialCaptured;

    bool _waitingP2LobbyRevealCompleted;
    bool _waitingLobbyCallbackRegistered;
    bool _waitingCommandListenerRegistered;
    Vector2 _startTitleInitialAnchoredPosition;
    bool _hasStartTitleInitialAnchoredPosition;
    Color _startHintInitialColor;
    bool _hasStartHintInitialColor;

    /// <summary>Forces lowercase on all copy driven through Main UI (prompts, placeholders, round copy, hints).</summary>
    static string MainUiDisplayText(string value)
    {
        if (value == null) return string.Empty;
        return value.ToLowerInvariant();
    }

    void Awake()
    {
        ApplyResolutionLock();
        DisableSceneOwnedGeneratedUiOrphans();
        ApplySingleLineOverflowToOwnedText();
        ApplyLowercaseToInitialPrefabCopy();
        if (!_initialCaptured) CaptureInitialState();
        EnsureInitialInputFieldPositionCaptured();
        SetStateVisibilityImmediate(_currentState);
        if (_currentState == MainUIState.Start)
        {
            ApplyStartDecorativeLineLayoutImmediate();
            ApplyStartHintText();
            ApplyStartInputPlaceholderState();
        }
    }

    void ApplyResolutionLock()
    {
        if (!_lockResolution || !Application.isPlaying)
            return;

        Screen.SetResolution(_lockedResolution.x, _lockedResolution.y, _lockedFullScreenMode);
    }

    void EnsureInitialInputFieldPositionCaptured()
    {
        if (_inputFieldRect != null && _initInputFieldAnchoredPos == Vector2.zero)
            _initInputFieldAnchoredPos = _inputFieldRect.anchoredPosition;
    }

    void ApplySingleLineOverflowToOwnedText()
    {
        if (!_forceSingleLineTextOverflow)
            return;

        foreach (var text in GetComponentsInChildren<TMP_Text>(true))
            ApplySingleLineOverflow(text);
    }

    /// <summary>Lowercase serialized/prefab strings on TMP under this hierarchy (excludes the shared input field's live answer text).</summary>
    void ApplyLowercaseToInitialPrefabCopy()
    {
        TMP_Text skipTypingDisplay = _inputField != null ? _inputField.textComponent : null;
        foreach (var t in GetComponentsInChildren<TMP_Text>(true))
        {
            if (t == null || t == skipTypingDisplay) continue;
            if (SkipMainUiLowercaseForPlayerIconIdTmp(t)) continue;
            t.text = MainUiDisplayText(t.text);
        }
    }

    /// <summary>P1/P2 slot labels stay uppercase; prefab copy would otherwise be lowercased in <see cref="ApplyLowercaseToInitialPrefabCopy"/>.</summary>
    static bool SkipMainUiLowercaseForPlayerIconIdTmp(TMP_Text t)
    {
        var s = t.text?.Trim();
        if (!string.IsNullOrEmpty(s) &&
            (string.Equals(s, "P1", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(s, "P2", StringComparison.OrdinalIgnoreCase)))
            return true;

        for (var tr = t.transform; tr != null; tr = tr.parent)
        {
            if (tr.name.IndexOf("PlayerIcon", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    void ApplySingleLineOverflow(TMP_Text text)
    {
        if (text == null || !_forceSingleLineTextOverflow)
            return;
        if (IsInputFieldOwnedText(text))
            return;

        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    bool IsInputFieldOwnedText(TMP_Text text)
    {
        if (text == null || _inputField == null)
            return false;

        return ReferenceEquals(text, _inputField.textComponent)
               || ReferenceEquals(text, _inputField.placeholder);
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    static void ScheduleMainUIPrefabBuild()
    {
        if (s_prefabBuildScheduled) return;

        s_prefabBuildScheduled = true;
        UnityEditor.EditorApplication.delayCall += BuildMainUIPrefabAssetIfNeeded;
    }

    static void BuildMainUIPrefabAssetIfNeeded()
    {
        s_prefabBuildScheduled = false;
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating)
        {
            ScheduleMainUIPrefabBuild();
            return;
        }

        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(MainUIPrefabPath);
        var assetController = prefab != null ? prefab.GetComponent<MainUIController>() : null;
        if (assetController == null) return;

        var needsSharedPromptAndGameplay = !assetController.HasPrefabOwnedSharedPromptAndGameplayUi();
        var needsRoundResult = !assetController.HasPrefabOwnedRoundResultUi();
        var needsRoundResultLayout = !needsRoundResult && assetController._roundResultLayoutVersion != RoundResultLayoutVersion;
        var needsPromptCalibrationOverlay = !assetController.HasPrefabOwnedPromptCalibrationOverlay();
        if (!needsSharedPromptAndGameplay && !needsRoundResult && !needsRoundResultLayout && !needsPromptCalibrationOverlay) return;

        var prefabRoot = UnityEditor.PrefabUtility.LoadPrefabContents(MainUIPrefabPath);
        if (prefabRoot == null) return;

        try
        {
            var controller = prefabRoot.GetComponent<MainUIController>();
            if (controller == null) return;

            s_buildingLoadedPrefabAsset = true;
            if (needsSharedPromptAndGameplay)
                controller.BuildPrefabOwnedSharedPromptAndGameplayUi();
            if (needsRoundResult || needsRoundResultLayout)
                controller.BuildPrefabOwnedRoundResultUi();
            if (needsPromptCalibrationOverlay)
                controller.BuildPromptCalibrationOverlayUi();
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(prefabRoot, MainUIPrefabPath);
        }
        finally
        {
            s_buildingLoadedPrefabAsset = false;
            UnityEditor.PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    void OnValidate()
    {
        if (Application.isPlaying) return;
        if (!CanEditPrefabAssetStructure()) return;

        ScheduleMainUIPrefabBuild();
    }

    [ContextMenu("Build Prefab-Owned Shared Prompt/Game Elements")]
    void BuildPrefabOwnedSharedPromptAndGameplayUi()
    {
        if (!CanEditPrefabAssetStructure())
        {
            Debug.LogError("Open MainUI.prefab in Prefab Mode before building shared prompt/gameplay UI children.");
            return;
        }

        EnsurePromptSharedView();
        EnsureGameplayElementsView();
        EnsurePromptCalibrationOverlayView(true);
        PrepareGameplayStart();
        SyncGeneratedStateGroupsForInspector();
        SetStateVisibilityImmediate(_currentState);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("Build Prefab-Owned Round Result Elements")]
    void BuildPrefabOwnedRoundResultUi()
    {
        if (!CanEditPrefabAssetStructure())
        {
            Debug.LogError("Open MainUI.prefab in Prefab Mode before building round result UI children.");
            return;
        }

        EnsureRoundResultElementsView();
        PrepareRoundResultStart();
        _roundResultLayoutVersion = RoundResultLayoutVersion;
        SyncGeneratedStateGroupsForInspector();
        SetStateVisibilityImmediate(_currentState);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("Build Prompt Calibration Overlay")]
    void BuildPromptCalibrationOverlayUi()
    {
        if (!CanEditPrefabAssetStructure())
        {
            BuildPromptCalibrationOverlayOnPrefabAsset();
            return;
        }

        EnsurePromptCalibrationOverlayView(true);
        RefreshPromptCalibrationOverlay();
        UnityEditor.EditorUtility.SetDirty(this);
    }

    void BuildPromptCalibrationOverlayOnPrefabAsset()
    {
        var prefabRoot = UnityEditor.PrefabUtility.LoadPrefabContents(MainUIPrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"Could not load {MainUIPrefabPath} to build prompt calibration overlay.");
            return;
        }

        try
        {
            var controller = prefabRoot.GetComponent<MainUIController>();
            if (controller == null)
            {
                Debug.LogError($"{MainUIPrefabPath} does not contain a MainUIController.");
                return;
            }

            s_buildingLoadedPrefabAsset = true;
            controller.EnsurePromptCalibrationOverlayView(true);
            controller.RefreshPromptCalibrationOverlay();
            UnityEditor.EditorUtility.SetDirty(controller);
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(prefabRoot, MainUIPrefabPath);
        }
        finally
        {
            s_buildingLoadedPrefabAsset = false;
            UnityEditor.PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    bool HasPrefabOwnedSharedPromptAndGameplayUi()
    {
        var promptRoot = _promptSharedGroupRect != null ? _promptSharedGroupRect : FindPromptSharedRoot(false);
        var gameplayRoot = _gameplayElementsGroupRect != null ? _gameplayElementsGroupRect : FindGameplayElementsRoot(false);

        return promptRoot != null
            && gameplayRoot != null
            && promptRoot.GetComponent<CanvasGroup>() != null
            && gameplayRoot.GetComponent<CanvasGroup>() != null
            && HasImageChild(promptRoot, "PromptMainBlackMask")
            && HasImageChild(promptRoot, "PromptBannedBlackMask")
            && HasTextChild(promptRoot, "PromptTitleText")
            && HasTextChild(promptRoot, "PromptBannedText")
            && HasImageChild(gameplayRoot, "GameplayTimerBar")
            && FindChildRect(gameplayRoot, "GameplayP1LetterGroup") != null
            && FindChildRect(gameplayRoot, "GameplayP2LetterGroup") != null;
    }

    bool HasPrefabOwnedRoundResultUi()
    {
        var roundResultRoot = _roundResultElementsGroupRect != null ? _roundResultElementsGroupRect : FindRoundResultElementsRoot();

        return roundResultRoot != null
            && roundResultRoot.GetComponent<CanvasGroup>() != null
            && HasImageChild(roundResultRoot, "RoundResultPanel")
            && HasTextChild(roundResultRoot, "RoundResultP1WordText")
            && HasTextChild(roundResultRoot, "RoundResultP2WordText")
            && HasTextChild(roundResultRoot, "RoundResultDeathLabelText")
            && HasImageChild(roundResultRoot, "RoundResultP1ScoreBar")
            && HasImageChild(roundResultRoot, "RoundResultP2ScoreBar")
            && FindChildRect(roundResultRoot, "RoundResultYellowStripe") == null
            && FindChildRect(roundResultRoot, "RoundResultBlueStripe") == null
            && FindChildRect(roundResultRoot, "RoundResultRedStripe") == null;
    }

    bool HasPrefabOwnedPromptCalibrationOverlay()
    {
        var designRoot = _designOverlayRoot != null ? _designOverlayRoot : FindChildRect(transform, DesignOverlayGroupName);
        var overlayRoot = designRoot != null ? FindChildRect(designRoot, PromptCalibrationOverlayName) : null;
        return overlayRoot != null
            && HasImageChild(overlayRoot, "PromptTitleVisualBounds")
            && HasImageChild(overlayRoot, "PromptBannedVisualBounds");
    }

    bool HasImageChild(RectTransform parent, string childName)
    {
        var rect = FindChildRect(parent, childName);
        return rect != null && rect.GetComponent<Image>() != null;
    }

    bool HasTextChild(RectTransform parent, string childName)
    {
        var rect = FindChildRect(parent, childName);
        return rect != null && rect.GetComponent<TMP_Text>() != null;
    }
#endif

    RectTransform FindPromptSharedRoot(bool renameDeprecated)
    {
        var rect = FindChildRect(transform, PromptSharedGroupName);
        if (rect == null)
            rect = FindChildRect(transform, DeprecatedPromptShowcaseRootName);

        if (renameDeprecated && rect != null && rect.name == DeprecatedPromptShowcaseRootName)
            rect.name = PromptSharedGroupName;

        return rect;
    }

    RectTransform FindGameplayElementsRoot(bool renameDeprecated)
    {
        var rect = FindChildRect(transform, GameplayElementsGroupName);
        if (rect == null)
            rect = FindChildRect(transform, DeprecatedGameplayRootName);

        if (renameDeprecated && rect != null && rect.name == DeprecatedGameplayRootName)
            rect.name = GameplayElementsGroupName;

        return rect;
    }

    RectTransform FindRoundResultElementsRoot()
    {
        return FindChildRect(transform, RoundResultElementsGroupName);
    }

    [ContextMenu("Capture Current As Initial State")]
    void CaptureInitialState()
    {
        _initBarPos = _cmykBar.anchoredPosition;
        _initBarSize = _cmykBar.sizeDelta;
        _initMWidth = _layoutM.preferredWidth;
        _initYWidth = _layoutY.preferredWidth;
        _initCWidth = _layoutC.preferredWidth;
        _initSkew = _graphicM.Skew;
        if (_inputFieldRect != null)
        {
            _initInputFieldAnchoredPos = _inputFieldRect.anchoredPosition;
            _initInputFieldSize = _inputFieldRect.sizeDelta;
        }
        if (_decorativeLines != null)
        {
            foreach (var l in _decorativeLines)
            {
                if (l?.rect == null) continue;
                l.initialAnchoredPos = l.rect.anchoredPosition;
                l.initialSizeDelta = l.rect.sizeDelta;
            }
        }
        _initialCaptured = true;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    void Start()
    {
        RegisterGameplayInputListener();
        if (_playIntroOnStart)
            PlayIntro();
    }

    void OnEnable()
    {
        RegisterGameplayInputListener();
        TryRegisterWaitingLobbyCallback();
    }

    void OnDisable()
    {
        UnregisterGameplayInputListener();
        TryUnregisterWaitingLobbyCallback();
        UnregisterWaitingCommandInputListener();
        UnsubscribeRoundTimerAcceleratedVisual();
        _awaitingResolutionScoresWhileLoading = false;
        if (_deferredResolutionRoundResultCoroutine != null)
        {
            StopCoroutine(_deferredResolutionRoundResultCoroutine);
            _deferredResolutionRoundResultCoroutine = null;
        }
    }

    void Update()
    {
        if (_showPromptCalibrationOverlay)
            RefreshPromptCalibrationOverlay();
        if (!_waitingLobbyCallbackRegistered)
            TryRegisterWaitingLobbyCallback();
    }

    [ContextMenu("Play Intro")]
    public void PlayIntro()
    {
        StopAllCoroutines();
        StartCoroutine(IntroRoutine());
    }

    IEnumerator IntroRoutine()
    {
        ApplyStartInputPlaceholderState();
        ApplyStartHintText();
        _titleTypewriter.Play();
        yield return new WaitUntil(() => !_titleTypewriter.IsPlaying);
        yield return new WaitForSeconds(_introGapSeconds);
        _hintTypewriter.Play();
        yield return new WaitUntil(() => !_hintTypewriter.IsPlaying);
        if (_hintCycler != null)
            _hintCycler.StartCycling();
    }

    [ContextMenu("Transition To Tutorial")]
    public void TransitionToTutorial()
    {
        DOTween.Kill(this);
        StopAllCoroutines();

        if (_hintCycler != null) _hintCycler.StopCycling();

        var seq = DOTween.Sequence().SetId(this);
        FadeStateDifference(seq, _currentState, MainUIState.Tutorial);

        AddCmykBarToTutorialTween(seq);

        // Input field: disable editing, fade text/placeholder/caret, shrink height
        if (_inputField != null)
        {
            _inputField.DeactivateInputField();
            _inputField.readOnly = true;
        }
        if (_inputFieldContentGroup != null)
            seq.Join(_inputFieldContentGroup.DOFade(0f, _fadeOutDuration).SetEase(_ease));
        if (_inputFieldRect != null)
        {
            var target = new Vector2(_inputFieldRect.sizeDelta.x, _inputFieldTutorialHeight);
            seq.Join(_inputFieldRect.DOSizeDelta(target, _duration).SetEase(_ease));
        }

        // Decorative lines: move to tutorial pos, resize to shared width
        if (_decorativeLines != null)
        {
            foreach (var l in _decorativeLines)
            {
                if (l?.rect == null) continue;
                seq.Join(l.rect.DOAnchorPos(l.tutorialAnchoredPos, _duration).SetEase(_ease));
                seq.Join(l.rect.DOSizeDelta(l.tutorialSizeDelta, _duration).SetEase(_ease));
            }
        }

        // Tutorial title typewriter + press space fade, sequenced via coroutine
        if (_tutorialTitleTypewriter != null) _tutorialTitleTypewriter.Hide();
        ConfigureTutorialPressSpaceHint(0f);
        StartCoroutine(TutorialRevealRoutine());

        if (UI.UIManager.Instance != null)
            UI.UIManager.Instance.EnterTutorialScreen(transitionMainUiToTutorial: false);
    }

    [ContextMenu("Transition To Room ID")]
    public void TransitionToRoomId()
    {
        if (_currentState == MainUIState.Start)
        {
            TransitionFromStartToRoomId();
            return;
        }

        TransitionToRoomIdCore();
    }

    void TransitionFromStartToRoomId()
    {
        DOTween.Kill(this);
        StopAllCoroutines();

        if (_hintCycler != null) _hintCycler.StopCycling();

        var titleText = GetTitleText();
        var hintText = GetHintText();
        CaptureStartTitleInitialPosition(titleText);
        CaptureStartHintInitialColor(hintText);
        if (_titleTypewriter != null)
        {
            _titleTypewriter.Stop();
            _titleTypewriter.ShowAll();
        }
        if (_hintTypewriter != null)
            _hintTypewriter.Stop();

        var titleCharacterCount = GetTitleCharacterCount(titleText);
        SetTitleVisibleCharacters(titleText, titleCharacterCount);
        SetHintAlpha(hintText, 1f);

        if (_inputField != null)
        {
            _inputField.DeactivateInputField();
            _inputField.readOnly = true;
        }

        if (_cmykBar == null)
        {
            SetTitleVisibleCharacters(titleText, 0);
            TransitionToRoomIdCore();
            return;
        }

        var startTipX = GetCmykBarLeftEdgeInParentSpace();
        var sweepDeltaX = GetStartJoinSweepDeltaX();
        var targetTipX = startTipX + sweepDeltaX;
        var titleStartPosition = GetStartTitleAnchoredPosition(titleText);
        var startPosition = _cmykBar.anchoredPosition;
        var startSize = _cmykBar.sizeDelta;
        var sweepTargetPosition = startPosition;
        var sweepTargetSize = startSize;
        GetStartJoinSweepBarTargets(sweepDeltaX, ref sweepTargetPosition, ref sweepTargetSize);

        var seq = DOTween.Sequence().SetId(this);
        seq.Append(_cmykBar.DOAnchorPos(sweepTargetPosition, _startJoinSweepOutDuration)
            .SetEase(_startJoinSweepEase)
            .OnUpdate(() => UpdateTitleFromStartJoinSweep(titleText, titleCharacterCount, startTipX, targetTipX, titleStartPosition)));
        seq.Join(_cmykBar.DOSizeDelta(sweepTargetSize, _startJoinSweepOutDuration).SetEase(_startJoinSweepEase));
        seq.Join(TweenHintAlpha(hintText, 0f, _startJoinSweepOutDuration));
        seq.AppendCallback(() => SetTitleVisibleCharacters(titleText, 0));
        seq.Append(_cmykBar.DOAnchorPos(startPosition, _startJoinSweepReturnDuration).SetEase(_startJoinSweepEase));
        seq.Join(_cmykBar.DOSizeDelta(startSize, _startJoinSweepReturnDuration).SetEase(_startJoinSweepEase));
        seq.OnComplete(() => TransitionToRoomIdCore(false, false));
    }

    void TransitionToRoomIdCore(bool killActiveTweens = true, bool animateCmykBar = true)
    {
        if (killActiveTweens)
            DOTween.Kill(this);
        StopAllCoroutines();

        if (_currentState == MainUIState.Loading)
            DismissLoadingOverlayImmediate();

        if (_hintCycler != null) _hintCycler.StopCycling();

        var seq = DOTween.Sequence().SetId(this);
        var fromState = _currentState;
        FadeStateDifference(seq, fromState, MainUIState.RoomId);

        if (animateCmykBar)
        {
            if (fromState == MainUIState.Tutorial)
                AddCmykBarFromTutorialToRoomIdTween(seq);
            else
                AddCmykBarToRoomIdTween(seq);
        }

        // Input field: re-enable editing, swap placeholder, fade content in, resize
        if (_inputField != null)
        {
            _inputField.readOnly = false;
            _inputField.text = string.Empty;
        }
        ApplyRoomIdInputPlaceholderState();
        if (_inputFieldContentGroup != null)
        {
            _inputFieldContentGroup.alpha = 0f;
            seq.Join(_inputFieldContentGroup.DOFade(1f, _duration).SetEase(_ease));
        }
        if (_inputFieldRect != null)
            seq.Join(_inputFieldRect.DOSizeDelta(_inputFieldRoomIdSize, _duration).SetEase(_ease));

        // Decorative lines to room-id targets
        if (_decorativeLines != null)
        {
            foreach (var l in _decorativeLines)
            {
                if (l?.rect == null) continue;
                seq.Join(l.rect.DOAnchorPos(l.roomIdAnchoredPos, _duration).SetEase(_ease));
                seq.Join(l.rect.DOSizeDelta(l.roomIdSizeDelta, _duration).SetEase(_ease));
            }
        }

        // Room-id title + hint typewriter reveal
        if (_roomIdTitleTypewriter != null) _roomIdTitleTypewriter.Hide();
        if (_roomIdHintTypewriter != null) _roomIdHintTypewriter.Hide();
        if (_roomIdTitleGroup != null) _roomIdTitleGroup.alpha = 0f;
        if (_roomIdHintGroup != null) _roomIdHintGroup.alpha = 0f;
        StartCoroutine(RoomIdRevealRoutine(GetRoomIdBarTransitionSeconds()));
    }

    [ContextMenu("Transition To Waiting")]
    public void TransitionToWaiting(string sessionRoomNameForWaitingRoomIdLine = null)
    {
        DOTween.Kill(this);
        StopAllCoroutines();

        if (_hintCycler != null) _hintCycler.StopCycling();

        ApplyWaitingRoomIdTypewriterLine(sessionRoomNameForWaitingRoomIdLine);

        var seq = DOTween.Sequence().SetId(this);
        FadeStateDifference(seq, _currentState, MainUIState.Waiting);

        // Decorative lines: move to waiting (top stacked) positions
        if (_decorativeLines != null)
        {
            for (var i = 0; i < _decorativeLines.Length; i++)
            {
                var l = _decorativeLines[i];
                if (l?.rect == null) continue;
                seq.Join(l.rect.DOAnchorPos(GetWaitingLineTargetPosition(l, i), _duration).SetEase(_ease));
                seq.Join(l.rect.DOSizeDelta(l.waitingSizeDelta, _duration).SetEase(_ease));
            }
        }

        // InputField: enable typing for waiting commands ("ready")
        if (UI.UIManager.Instance != null)
        {
            UI.UIManager.Instance.SetAnswerInputEnabled(true);
            UI.UIManager.Instance.SetAnswerInputReadOnly(false);
            UI.UIManager.Instance.ClearAnswerInputField();
        }
        if (_inputFieldContentGroup != null)
            seq.Join(_inputFieldContentGroup.DOFade(0f, _fadeOutDuration).SetEase(_ease));

        // Black panel: starts at the InputField footprint, grows up to target size
        if (_waitingPanel != null)
        {
            _waitingPanel.anchoredPosition = GetWaitingPanelStartPosition();
            _waitingPanel.sizeDelta = _waitingPanelStartSize;
            seq.Join(_waitingPanel.DOSizeDelta(_waitingPanelTargetSize, _duration).SetEase(_ease));
        }

        // Reset waiting content to invisible starting state
        if (_waitingTitleTypewriter != null) _waitingTitleTypewriter.Hide();
        if (_waitingRoomIdTypewriter != null) _waitingRoomIdTypewriter.Hide();
        if (_waitingHintTypewriter != null) _waitingHintTypewriter.Hide();
        if (_waitingTitleGroup != null) _waitingTitleGroup.alpha = 0f;
        if (_waitingRoomIdGroup != null) _waitingRoomIdGroup.alpha = 0f;
        if (_waitingHintGroup != null) _waitingHintGroup.alpha = 0f;
        _waitingP2LobbyRevealCompleted = false;
        ConfigureWaitingPlayerIconsLayout();
        ApplyWaitingTextStyles();
        if (_waitingP1Group != null) _waitingP1Group.alpha = 0f;
        if (_waitingP2Group != null) _waitingP2Group.alpha = 0f;

        TryRegisterWaitingLobbyCallback();
        RegisterWaitingCommandInputListener();
        StartCoroutine(WaitingRevealRoutine());
    }

    void ApplyWaitingRoomIdTypewriterLine(string sessionRoomName)
    {
        if (string.IsNullOrWhiteSpace(sessionRoomName) || _waitingRoomIdTypewriter == null)
            return;

        var tmp = _waitingRoomIdTypewriter.GetComponent<TMP_Text>();
        if (tmp != null)
            tmp.text = MainUiDisplayText($"room id: {sessionRoomName}");
    }

    [ContextMenu("Transition To Loading")]
    public void TransitionToLoading()
    {
        if (_currentState == MainUIState.Waiting && _loadingScreenRect != null && _loadingScreenGroup != null)
        {
            TransitionFromWaitingToLoading();
            return;
        }

        TransitionToConfiguredState(MainUIState.Loading);
    }

    /// <summary>
    /// Called when a round ends in MainUI flow: tear down answer listeners so the next Gameplay can re-register, clear prompt latch, then Loading hold until <see cref="PromptGenerator"/> pushes the next prompt (same path as match start).
    /// </summary>
    public void BeginNextRoundFromResolution() => ResetMatchFlowToLoadingAwaitingPrompt();

    /// <summary>
    /// Match-session UI reset: same entry as a new round after resolution — clears local gameplay input wiring, kills round-result / prompt defer coroutines,
    /// clears the prompt latch, then full-screen <see cref="MainUIState.Loading"/> until <see cref="NotifyPromptReceivedFromServer"/> (normally after <see cref="UI.UIManager.SetMainUiPrompt"/>).
    /// For lobby / CMYK start screen use <see cref="ResetToStart"/> instead.
    /// </summary>
    public void ResetMatchFlowToLoadingAwaitingPrompt()
    {
        foreach (var client in FindObjectsByType<Client>(FindObjectsSortMode.InstanceID))
        {
            if (!client.IsOwner) continue;
            client.ResetGameplayAnswerListenersForNewRound();
            break;
        }

        PrepareForRoundResultTransitionCleanup();

        _gameplayP1Word = string.Empty;
        _gameplayP2Word = string.Empty;
        _gameplayP1SyncedLetterCount = -1;
        _gameplayP2SyncedLetterCount = -1;
        RefreshGameplayLetterBlocks();

        _promptReceivedFromServer = false;
        _promptShowcaseTransitionStarted = false;

        EnterLoadingHoldForPrompt();
    }

    public void EnterLoadingHoldForPrompt()
    {
        DOTween.Kill(this);
        StopAllCoroutines();
        UnsubscribeRoundTimerAcceleratedVisual();

        if (_deferredPromptShowcaseCoroutine != null)
        {
            StopCoroutine(_deferredPromptShowcaseCoroutine);
            _deferredPromptShowcaseCoroutine = null;
        }

        var seq = DOTween.Sequence().SetId(this);
        if (AppendFadeOutAllUiCanvasGroupsBeforeLoading(seq, _fadeOutDuration))
            seq.OnComplete(EnterLoadingHoldForPromptAfterFadeOut);
        else
            EnterLoadingHoldForPromptAfterFadeOut();
    }

    void EnterLoadingHoldForPromptAfterFadeOut()
    {
        // Important: we need to be in the Loading state so TransitionToPromptShowcase
        // uses the Loading -> PromptShowcase animated path. For hold-loading we switch
        // state immediately (no tween), then wait for prompt push.
        ApplyMainLoadingOverlayVisualStateImmediate();

        _awaitingPromptFromServerWhileLoading = true;

        if (_debugPromptFlow)
            Debug.Log($"[MainUIController] EnterLoadingHoldForPrompt -> state={_currentState} promptReceived={_promptReceivedFromServer}");

        // If the prompt already arrived before we got into Loading, advance immediately.
        if (_promptReceivedFromServer && _currentState == MainUIState.Loading)
            NotifyPromptReceivedFromServer();
    }

    /// <summary>Sets <see cref="MainUIState.Loading"/> visibility + full-screen loading wipe; shared by prompt-hold and resolution→round-result loading.</summary>
    void ApplyMainLoadingOverlayVisualStateImmediate()
    {
        SetStateVisibilityImmediate(MainUIState.Loading);
        _promptShowcaseTransitionStarted = false;
        _loadingHoldStartUnscaledTime = Time.unscaledTime;

        if (_loadingScreenRect != null && _loadingScreenGroup != null)
        {
            PrepareLoadingWipeStart();
            _loadingScreenRect.SetAsLastSibling();
            SetLoadingWipeComplete();
            _loadingScreenGroup.alpha = 1f;
            _loadingScreenGroup.interactable = false;
            _loadingScreenGroup.blocksRaycasts = false;
        }

        HideSharedPromptAndInputForFullScreenLoadingOverlay();
    }

    IEnumerator DeferredAdvanceToPromptShowcaseRoutine(float delaySeconds)
    {
        // Always wait at least one frame so Loading visibility settles.
        yield return null;
        if (delaySeconds > 0f)
            yield return new WaitForSecondsRealtime(delaySeconds);

        _deferredPromptShowcaseCoroutine = null;
        NotifyPromptReceivedFromServer();
    }

    public void NotifyPromptReceivedFromServer()
    {
        _promptReceivedFromServer = true;
        if (_debugPromptFlow)
            Debug.Log($"[MainUIController] NotifyPromptReceivedFromServer state={_currentState} awaiting={_awaitingPromptFromServerWhileLoading} started={_promptShowcaseTransitionStarted} prompt='{_promptText}' banned='{_promptBannedLetters}'");
        if (!_awaitingPromptFromServerWhileLoading) return;
        if (_currentState != MainUIState.Loading) return;
        if (_promptShowcaseTransitionStarted) return;

        // Ensure Loading is visible for at least a short moment, otherwise it feels like
        // PromptShowcase starts mid-transition.
        var elapsed = _loadingHoldStartUnscaledTime < 0f ? 999f : (Time.unscaledTime - _loadingHoldStartUnscaledTime);
        var remaining = Mathf.Max(0f, _minLoadingHoldSecondsBeforePromptShowcase - elapsed);
        if (remaining > 0f || _loadingHoldStartUnscaledTime >= 0f)
        {
            if (_deferredPromptShowcaseCoroutine == null && (remaining > 0f))
            {
                if (_debugPromptFlow)
                    Debug.Log($"[MainUIController] Deferring PromptShowcase by {remaining:0.###}s to ensure Loading hold.");
                _deferredPromptShowcaseCoroutine = StartCoroutine(DeferredAdvanceToPromptShowcaseRoutine(remaining));
                return;
            }
        }

        _awaitingPromptFromServerWhileLoading = false;
        _promptShowcaseTransitionStarted = true;
        TransitionToPromptShowcase();
    }

    void TransitionFromWaitingToLoading()
    {
        DOTween.Kill(this);
        StopAllCoroutines();

        PrepareLoadingWipeStart();
        _loadingScreenRect.SetAsLastSibling();

        _loadingScreenGroup.alpha = 1f;
        _loadingScreenGroup.interactable = false;
        _loadingScreenGroup.blocksRaycasts = false;

        var seq = DOTween.Sequence().SetId(this);
        AppendFadeOutAllUiCanvasGroupsBeforeLoading(seq, _fadeOutDuration);
        seq.AppendCallback(() =>
        {
            _currentState = MainUIState.Loading;
            RefreshPromptCalibrationOverlay();
            SyncRoomIdScreenWithUIManager(MainUIState.Loading);
            UpdateWaitingCommandListenerForState(MainUIState.Loading);
        });
        seq.Append(_loadingScreenRect.DOScaleY(1f, _loadingWipeDuration).SetEase(_loadingWipeEase));
        seq.OnComplete(() =>
        {
            SetLoadingWipeComplete();
            StartCoroutine(AutoPromptAfterLoadingRoutine());
        });
    }

    IEnumerator AutoPromptAfterLoadingRoutine()
    {
        yield return new WaitForSeconds(_loadingAutoPromptDelay);
        if (_currentState == MainUIState.Loading)
            TransitionToPromptShowcase();
    }

    void PrepareLoadingWipeStart()
    {
        ApplyLoadingScreenLayout(0f);

        if (_loadingScreenImage != null)
        {
            _loadingScreenImage.type = Image.Type.Simple;
            _loadingScreenImage.fillAmount = 1f;
        }
    }

    void SetLoadingWipeComplete()
    {
        if (_loadingScreenRect == null) return;

        ApplyLoadingScreenLayout(1f);
        if (_loadingScreenImage != null)
        {
            _loadingScreenImage.type = Image.Type.Simple;
            _loadingScreenImage.fillAmount = 1f;
        }
        if (_loadingScreenGroup != null)
            _loadingScreenGroup.alpha = 1f;
    }

    void DismissLoadingOverlayImmediate()
    {
        if (_loadingScreenGroup != null)
            _loadingScreenGroup.DOKill(false);
        if (_loadingScreenRect != null)
            SetLoadingWipeComplete();
        if (_loadingScreenGroup != null)
            _loadingScreenGroup.alpha = 0f;
    }

    /// <summary>
    /// After Gameplay, shared prompt/input CanvasGroups may not all be driven by <see cref="SetStateVisibilityImmediate"/> for Loading,
    /// or the input strip can be <c>SetAsLastSibling</c> above the wipe. Force them fully transparent until the next flow restores layout.
    /// </summary>
    void HideSharedPromptAndInputForFullScreenLoadingOverlay()
    {
        if (_inputField != null)
        {
            _inputField.DeactivateInputField();
            _inputField.interactable = false;
            _inputField.readOnly = true;
        }

        if (_inputFieldContentGroup != null)
        {
            _inputFieldContentGroup.DOKill(false);
            _inputFieldContentGroup.alpha = 0f;
        }

        var shell = GetInputFieldStateGroup();
        if (shell != null)
        {
            shell.DOKill(false);
            shell.alpha = 0f;
        }

        if (_promptSharedGroup != null)
        {
            _promptSharedGroup.DOKill(false);
            _promptSharedGroup.alpha = 0f;
        }
    }

    void ApplyLoadingScreenLayout(float scaleY)
    {
        if (_loadingScreenRect == null) return;

        var parentSize = GetParentRectSize(_loadingScreenRect);
        _loadingScreenRect.anchorMin = new Vector2(0.5f, 0.5f);
        _loadingScreenRect.anchorMax = new Vector2(0.5f, 0.5f);
        _loadingScreenRect.pivot = new Vector2(0.5f, 0f);
        _loadingScreenRect.anchoredPosition = new Vector2(0f, parentSize.y * -0.5f);
        _loadingScreenRect.sizeDelta = parentSize;
        _loadingScreenRect.localScale = new Vector3(1f, scaleY, 1f);
    }

    Vector2 GetParentRectSize(RectTransform rect)
    {
        if (rect != null && rect.parent is RectTransform parentRect)
        {
            var size = parentRect.rect.size;
            return new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
        }

        return new Vector2(1920f, 1080f);
    }

    [ContextMenu("Transition To Prompt Showcase")]
    public void TransitionToPromptShowcase()
    {
        if (_debugPromptFlow)
            Debug.Log($"[MainUIController] TransitionToPromptShowcase currentState={_currentState} prompt='{_promptText}' banned='{_promptBannedLetters}'");
        if (_currentState == MainUIState.Loading)
        {
            TransitionFromLoadingToPromptShowcase();
            return;
        }

        TransitionToConfiguredState(MainUIState.PromptShowcase);
    }

    void TransitionFromLoadingToPromptShowcase()
    {
        DOTween.Kill(this);
        StopAllCoroutines();
        if (_debugPromptFlow)
            Debug.Log("[MainUIController] TransitionFromLoadingToPromptShowcase BEGIN (killed tweens/coroutines)");

        EnsurePromptSharedView();
        var showBannedInThisShowcase = HasPromptBannedLetters();
        PreparePromptShowcaseStart(showBannedInThisShowcase);
        if (_debugPromptFlow)
            Debug.Log($"[MainUIController] PreparePromptShowcaseStart DONE maskMainX={_promptPromptMask?.anchoredPosition.x} maskBannedX={_promptBannedMask?.anchoredPosition.x} titleAlpha={_promptTitleText?.alpha} bannedAlpha={_promptBannedText?.alpha}");

        if (_promptSharedGroupRect != null)
            _promptSharedGroupRect.SetAsLastSibling();

        var seq = DOTween.Sequence().SetId(this);

        if (_loadingScreenGroup != null)
            _loadingScreenGroup.alpha = 1f;

        var hasEnterTween = false;
        if (_promptPromptMask != null)
        {
            seq.Append(_promptPromptMask.DOAnchorPosX(GetPromptMaskMainTargetX(), _promptMaskEnterDuration).SetEase(_promptMaskRevealEase));
            hasEnterTween = true;
        }
        if (_promptBannedMask != null && showBannedInThisShowcase)
            AddPromptEnterTween(seq, ref hasEnterTween, _promptBannedMask.DOAnchorPosX(GetPromptMaskBannedTargetX(), _promptMaskEnterDuration).SetEase(_promptMaskRevealEase));
        else if (_promptBannedMask != null)
            _promptBannedMask.gameObject.SetActive(false);
        if (_promptTitleText != null)
            AddPromptEnterTween(seq, ref hasEnterTween, _promptTitleText.DOFade(1f, _promptTextFadeDuration).SetEase(_ease));
        if (_promptBannedText != null && showBannedInThisShowcase)
            AddPromptEnterTween(seq, ref hasEnterTween, _promptBannedText.DOFade(1f, _promptTextFadeDuration).SetEase(_ease));

        seq.AppendInterval(_promptHoldBeforeRevealSeconds);
        seq.AppendCallback(SetPromptTextForReveal);
        var hasRevealTween = false;
        if (_promptPromptMask != null)
            AddPromptRevealTween(seq, ref hasRevealTween, _promptPromptMask.DOAnchorPosX(GetPromptMaskMainTargetX() + 5000f, _promptMaskRevealDuration).SetEase(_promptMaskRevealEase));
        if (_promptBannedMask != null && showBannedInThisShowcase)
            AddPromptRevealTween(seq, ref hasRevealTween, _promptBannedMask.DOAnchorPosX(GetPromptMaskBannedTargetX() + 1980f, _promptMaskRevealDuration).SetEase(_promptMaskRevealEase));
        if (_promptTitleText != null)
            AddPromptRevealTween(seq, ref hasRevealTween, _promptTitleText.DOColor(_promptInkColor, _promptMaskRevealDuration).SetEase(_promptMaskRevealEase));
        if (_promptBannedText != null && showBannedInThisShowcase)
            AddPromptRevealTween(seq, ref hasRevealTween, _promptBannedText.DOColor(_promptInkColor, _promptMaskRevealDuration).SetEase(_promptMaskRevealEase));

        seq.OnComplete(() =>
        {
            StartCoroutine(AutoGameplayAfterPromptRoutine());
        });

        _currentState = MainUIState.PromptShowcase;
        _promptShowcaseFinishedNotified = false;
    }

    IEnumerator AutoGameplayAfterPromptRoutine()
    {
        yield return new WaitForSeconds(_promptAutoGameplayDelay);
        if (_currentState == MainUIState.PromptShowcase)
            TransitionToGameplay();
    }

    void AddPromptEnterTween(Sequence seq, ref bool hasEnterTween, Tween tween)
    {
        if (seq == null || tween == null) return;

        if (hasEnterTween)
            seq.Join(tween);
        else
        {
            seq.Append(tween);
            hasEnterTween = true;
        }
    }

    void AddPromptRevealTween(Sequence seq, ref bool hasRevealTween, Tween tween)
    {
        if (seq == null || tween == null) return;

        if (hasRevealTween)
            seq.Join(tween);
        else
        {
            seq.Append(tween);
            hasRevealTween = true;
        }
    }

    [ContextMenu("Build Shared Prompt Elements")]
    void BuildPromptSharedView()
    {
        EnsurePromptSharedView();
        PreparePromptShowcaseStart();
        if (_promptSharedGroup != null)
            _promptSharedGroup.alpha = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        if (_promptSharedGroupRect != null)
            UnityEditor.EditorUtility.SetDirty(_promptSharedGroupRect.gameObject);
#endif
    }

    [ContextMenu("Toggle Prompt Calibration Overlay")]
    void TogglePromptCalibrationOverlay()
    {
        _showPromptCalibrationOverlay = !_showPromptCalibrationOverlay;
        if (!_showPromptCalibrationOverlay)
        {
            HidePromptCalibrationOverlay();
            return;
        }

        RefreshPromptCalibrationOverlay();
    }

    [ContextMenu("Refresh Prompt Calibration Overlay")]
    void RefreshPromptCalibrationOverlay()
    {
        EnsurePromptCalibrationOverlayView(false);
        if (!_showPromptCalibrationOverlay)
        {
            HidePromptCalibrationOverlay();
            return;
        }

        ApplyPromptCalibrationOverlayVisibility();

        UpdateTextVisualBoundsOverlay(_promptTitleText, _promptTitleVisualBoundsOverlay, _promptTitleBoundsOverlayColor);
        UpdateTextVisualBoundsOverlay(_promptBannedText, _promptBannedVisualBoundsOverlay, _promptBannedBoundsOverlayColor);
    }

    [ContextMenu("Next Prompt Calibration Reference Image")]
    void NextPromptCalibrationReferenceImage()
    {
        var count = CountDesignReferenceImages();
        if (count <= 0) return;

        _autoSelectDesignReferenceForCurrentState = false;
        _promptCalibrationReferenceImageIndex = (_promptCalibrationReferenceImageIndex + 1) % count;
        RefreshPromptCalibrationOverlay();
    }

    [ContextMenu("Use Current State Design Reference")]
    void UseCurrentStateDesignReference()
    {
        _autoSelectDesignReferenceForCurrentState = true;
        RefreshPromptCalibrationOverlay();
    }

    [ContextMenu("Transition To Gameplay")]
    public void TransitionToGameplay()
    {
        if (_currentState == MainUIState.PromptShowcase)
        {
            TransitionFromPromptShowcaseToGameplay();
            return;
        }

        TransitionToConfiguredState(MainUIState.Gameplay);
    }

    void TransitionFromPromptShowcaseToGameplay()
    {
        NotifyPromptShowcaseFinishedToServerIfNeeded();
        DOTween.Kill(this);
        StopAllCoroutines();

        EnsurePromptSharedView();
        EnsureGameplayElementsView();
        PrepareGameplayStart();

        if (_gameplayElementsGroupRect != null)
            _gameplayElementsGroupRect.SetAsLastSibling();
        if (_promptSharedGroupRect != null)
            _promptSharedGroupRect.SetAsLastSibling();
        SetGameplayInputFieldSiblingOrder();
        SetGameplayPlayerIconSiblingOrder();
        SetOptionalGameObjectActive(_promptSharedBackground, false);

        var seq = DOTween.Sequence().SetId(this);
        if (_gameplayElementsGroup != null)
            seq.Append(_gameplayElementsGroup.DOFade(1f, _gameplayFadeDuration).SetEase(_ease));

        AddPromptToGameplayTween(seq);
        AddGameplayInputFieldTween(seq);
        AddGameplayPlayerIconEnterTween(seq, _waitingP1Group);
        AddGameplayPlayerIconEnterTween(seq, _waitingP2Group);

        seq.OnComplete(FinishEnteringGameplayState);

        _currentState = MainUIState.Gameplay;
    }

    void NotifyPromptShowcaseFinishedToServerIfNeeded()
    {
        if (_promptShowcaseFinishedNotified) return;
        if (NetworkManager.Singleton == null) return;
        var rm = FindAnyObjectByType<RoundManager>();
        if (rm == null) return;

        _promptShowcaseFinishedNotified = true;
        rm.NotifyPromptShowcaseFinishedServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    /// <summary>
    /// Tells the server this client has finished entering Gameplay UI. The round timer starts only after both clients notify (see <see cref="RoundManager.NotifyGameplayUiEnteredServerRpc"/>).
    /// </summary>
    void NotifyGameplayUiEnteredToServer()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient) return;
        var rm = FindAnyObjectByType<RoundManager>();
        if (rm == null || !rm.IsSpawned) return;

        rm.NotifyGameplayUiEnteredServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    /// <summary>
    /// Registers shared <see cref="TMP_InputField.onValueChanged"/> and <see cref="TMP_InputField.onSubmit"/> on the local owner <see cref="Client"/> (word typing + submit).
    /// </summary>
    void RegisterLocalOwnerGameplayAnswerInput()
    {
        if (UI.UIManager.Instance == null) return;
        foreach (var client in FindObjectsByType<Client>(FindObjectsSortMode.InstanceID))
        {
            if (!client.IsOwner) continue;
            client.OnEnteredGameplayScreen();
            return;
        }
    }

    void AddPromptToGameplayTween(Sequence seq)
    {
        if (seq == null) return;

        if (_promptTitleText != null)
        {
            _promptTitleText.alignment = TextAlignmentOptions.Left;
            SetCenterLeftAnchorsKeepingVisualPosition(_promptTitleText.rectTransform);
            seq.Join(_promptTitleText.rectTransform.DOAnchorPos(GetGameplayPromptPosition(), _gameplayFadeDuration).SetEase(_ease));
            seq.Join(_promptTitleText.rectTransform.DOSizeDelta(GetGameplayPromptSize(), _gameplayFadeDuration).SetEase(_ease));
            seq.Join(DOTween.To(() => _promptTitleText.fontSize, x => _promptTitleText.fontSize = x, GetGameplayPromptFontSize(), _gameplayFadeDuration).SetEase(_ease));
        }

        if (_promptBannedText != null)
        {
            _promptBannedText.alignment = TextAlignmentOptions.Left;
            SetCenterLeftAnchorsKeepingVisualPosition(_promptBannedText.rectTransform);
            seq.Join(_promptBannedText.rectTransform.DOAnchorPos(GetGameplayBannedLabelPosition(), _gameplayFadeDuration).SetEase(_ease));
            seq.Join(_promptBannedText.rectTransform.DOSizeDelta(GetGameplayBannedLabelSize(), _gameplayFadeDuration).SetEase(_ease));
            seq.Join(DOTween.To(() => _promptBannedText.fontSize, x => _promptBannedText.fontSize = x, GetGameplayBannedLabelFontSize(), _gameplayFadeDuration).SetEase(_ease));
        }
    }

    void AddPromptToRoundResultTween(Sequence seq)
    {
        if (seq == null) return;

        if (_promptTitleText != null)
        {
            _promptTitleText.alignment = TextAlignmentOptions.Left;
            _promptTitleText.color = _roundResultTextColor;
            _promptTitleText.text = MainUiDisplayText(GetPromptTextWithBannedLetters(_promptText));
            SetTopLeftAnchors(_promptTitleText.rectTransform);
            _promptTitleText.alignment = TextAlignmentOptions.TopLeft;
            seq.Join(_promptTitleText.rectTransform.DOAnchorPos(GetRoundResultPromptTopLeftPosition(), _roundResultTransitionDuration).SetEase(_ease));
            seq.Join(_promptTitleText.rectTransform.DOSizeDelta(new Vector2(1400f, 220f), _roundResultTransitionDuration).SetEase(_ease));
            seq.Join(DOTween.To(() => _promptTitleText.fontSize, x => _promptTitleText.fontSize = x, 170f, _roundResultTransitionDuration).SetEase(_ease));
        }

        if (_promptBannedText != null)
        {
            _promptBannedText.richText = true;
            _promptBannedText.overrideColorTags = false;
            _promptBannedText.alignment = TextAlignmentOptions.Left;
            _promptBannedText.color = _roundResultTextColor;
            _promptBannedText.text = MainUiDisplayText(GetBannedLetterRevealText());
            SetTopLeftAnchors(_promptBannedText.rectTransform);
            _promptBannedText.alignment = TextAlignmentOptions.TopLeft;
            seq.Join(_promptBannedText.rectTransform.DOAnchorPos(GetRoundResultBannedLabelTopLeftPosition(), _roundResultTransitionDuration).SetEase(_ease));
            seq.Join(_promptBannedText.rectTransform.DOSizeDelta(new Vector2(1000f, 90f), _roundResultTransitionDuration).SetEase(_ease));
            seq.Join(DOTween.To(() => _promptBannedText.fontSize, x => _promptBannedText.fontSize = x, 59f, _roundResultTransitionDuration).SetEase(_ease));
        }
    }

    void AddGameplayPlayerIconEnterTween(Sequence seq, CanvasGroup group)
    {
        if (seq == null || group == null) return;
        if (!(group.transform is RectTransform rect)) return;

        seq.Join(group.DOFade(1f, _gameplayFadeDuration).SetEase(_ease));
        seq.Join(rect.DOAnchorPosY(rect.anchoredPosition.y + _gameplaySlideOffset, _gameplayFadeDuration).SetEase(_ease));
    }

    void AddRoundResultPlayerIconTween(Sequence seq, CanvasGroup group, Vector2 targetPosition, bool isP1Slot)
    {
        if (seq == null || group == null) return;
        if (!(group.transform is RectTransform rect)) return;

        var showLocal = IsLocalYouIndicatorForSlot(isP1Slot);
        var icon = group.GetComponent<PlayerIcon>();
        if (icon != null)
            icon.IsLocal = showLocal;
        ConfigurePlayerIconBoxForGameplay(rect);
        ConfigurePlayerIconIndicatorForGameplay(rect, icon != null ? icon.IsLocal : showLocal);
        seq.Join(group.DOFade(1f, _roundResultTransitionDuration).SetEase(_ease));
        seq.Join(rect.DOAnchorPos(targetPosition, _roundResultTransitionDuration).SetEase(_ease));
        seq.Join(rect.DOSizeDelta(new Vector2(100f, 100f), _roundResultTransitionDuration).SetEase(_ease));
    }

    /// <summary>
    /// Fills round-result copy from the same host/client answers and HP used by the legacy resolution screen (host = P1). Call before <see cref="TransitionToRoundResult"/> when entering from <see cref="RoundManager"/> resolution.
    /// </summary>
    public void ApplyRoundResolutionData(string hostAnswer, string clientAnswer, int hostHp, int clientHp, bool hostAnswerLetterEligible = true, bool clientAnswerLetterEligible = true)
    {
        _roundResultP1Word = hostAnswer ?? "";
        _roundResultP2Word = clientAnswer ?? "";
        _roundResultP1Score = hostHp;
        _roundResultP2Score = clientHp;
        _roundResultHostAnswerLetterEligible = hostAnswerLetterEligible;
        _roundResultClientAnswerLetterEligible = clientAnswerLetterEligible;
    }

    /// <summary>
    /// MainUI flow: after resolution damage is applied on the server, wait until local <see cref="Client.CurrentHp"/> matches the server snapshot, then play the input-field wipe into Round Result.
    /// </summary>
    public void BeginResolutionScoreSyncThenRoundResult(string hostAnswer, string clientAnswer, int hostHpAfter, int clientHpAfter, bool hostAnswerLetterEligible, bool clientAnswerLetterEligible)
    {
        PrepareForRoundResultTransitionCleanup();

        _pendingResolutionHostAnswer = hostAnswer ?? "";
        _pendingResolutionClientAnswer = clientAnswer ?? "";
        _pendingResolutionHostHpTarget = hostHpAfter;
        _pendingResolutionClientHpTarget = clientHpAfter;
        _pendingResolutionHostAnswerLetterEligible = hostAnswerLetterEligible;
        _pendingResolutionClientAnswerLetterEligible = clientAnswerLetterEligible;

        _awaitingPromptFromServerWhileLoading = false;
        if (_deferredPromptShowcaseCoroutine != null)
        {
            StopCoroutine(_deferredPromptShowcaseCoroutine);
            _deferredPromptShowcaseCoroutine = null;
        }

        BeginResolutionScoreSyncWait();
    }

    void BeginResolutionScoreSyncWait()
    {
        _awaitingResolutionScoresWhileLoading = true;
        _loadingHoldStartUnscaledTime = -1f;
        _deferredResolutionRoundResultCoroutine = StartCoroutine(WaitResolutionHpSyncAndEnterRoundResultRoutine());
    }

    IEnumerator WaitResolutionHpSyncAndEnterRoundResultRoutine()
    {
        yield return null;

        var deadline = Time.realtimeSinceStartup + k_resolutionHpSyncTimeoutSeconds;
        while (_awaitingResolutionScoresWhileLoading && Time.realtimeSinceStartup < deadline)
        {
            if (LocalResolutionHpMatchesPendingTargets())
                break;
            yield return null;
        }

        if (!_awaitingResolutionScoresWhileLoading)
            yield break;

        _awaitingResolutionScoresWhileLoading = false;
        _deferredResolutionRoundResultCoroutine = null;

        var matched = LocalResolutionHpMatchesPendingTargets();
        var p1Hp = _pendingResolutionHostHpTarget;
        var p2Hp = _pendingResolutionClientHpTarget;
        if (matched && PlayerManager.Instance != null)
        {
            var host = PlayerManager.Instance.GetHost();
            var client = PlayerManager.Instance.GetClient(1);
            if (host != null && client != null)
            {
                p1Hp = host.CurrentHp.Value;
                p2Hp = client.CurrentHp.Value;
            }
        }

        ApplyRoundResolutionData(_pendingResolutionHostAnswer, _pendingResolutionClientAnswer, p1Hp, p2Hp, _pendingResolutionHostAnswerLetterEligible, _pendingResolutionClientAnswerLetterEligible);

        // Do not call StopAllCoroutines() here — this code runs inside WaitResolutionHpSyncAndEnterRoundResultRoutine.
        DOTween.Kill(this);
        UnsubscribeRoundTimerAcceleratedVisual();
        _deferredPromptShowcaseCoroutine = null;
        _deferredResolutionRoundResultCoroutine = null;

        RunRoundResultEntranceTweenSequence();
    }

    bool LocalResolutionHpMatchesPendingTargets()
    {
        if (PlayerManager.Instance == null) return false;

        var host = PlayerManager.Instance.GetHost();
        var client = PlayerManager.Instance.GetClient(1);
        if (host == null || client == null) return false;

        return host.CurrentHp.Value == _pendingResolutionHostHpTarget &&
               client.CurrentHp.Value == _pendingResolutionClientHpTarget;
    }

    void PrepareForRoundResultTransitionCleanup()
    {
        DOTween.Kill(this);
        StopAllCoroutines();
        _deferredPromptShowcaseCoroutine = null;
        _deferredResolutionRoundResultCoroutine = null;
        UnsubscribeRoundTimerAcceleratedVisual();
        _awaitingResolutionScoresWhileLoading = false;
    }

    void RunRoundResultEntranceTweenSequence()
    {
        EnsurePromptSharedView();
        EnsureGameplayElementsView();
        EnsureRoundResultElementsView();

        SetSharedPromptVisibleForGameplay();
        SetGameplayPlayerIconsVisible();
        PrepareRoundResultStart();

        var seq = DOTween.Sequence().SetId(this);

        if (_roundResultElementsGroupRect != null)
            _roundResultElementsGroupRect.SetAsLastSibling();
        if (_promptSharedGroupRect != null)
            _promptSharedGroupRect.SetAsLastSibling();
        SetRoundResultPlayerIconSiblingOrder();

        PrepareRoundResultTransitionStart();

        var inputGroup = GetInputFieldStateGroup();
        if (inputGroup != null)
        {
            inputGroup.alpha = 1f;
            inputGroup.interactable = false;
            inputGroup.blocksRaycasts = false;
        }

        if (_inputField != null)
            _inputField.DeactivateInputField();
        if (_inputFieldContentGroup != null)
            _inputFieldContentGroup.alpha = 0f;

        var panelMorphSeq = DOTween.Sequence().SetId(this);
        if (_inputFieldRect != null)
        {
            _inputFieldRect.SetAsLastSibling();
            panelMorphSeq.Join(_inputFieldRect.DOAnchorPos(GetRoundResultPanelPosition(), _roundResultPanelMorphDuration).SetEase(_ease));
            panelMorphSeq.Join(_inputFieldRect.DOSizeDelta(GetRoundResultPanelSize(), _roundResultPanelMorphDuration).SetEase(_ease));
        }
        seq.Append(panelMorphSeq);

        seq.AppendCallback(() =>
        {
            SetRoundResultPanelAlpha(1f);
            if (inputGroup != null)
                inputGroup.alpha = 0f;
            if (_promptSharedGroup != null)
                _promptSharedGroup.alpha = 0f;
            if (_gameplayElementsGroup != null)
                _gameplayElementsGroup.alpha = 0f;
            if (_waitingP1Group != null)
                _waitingP1Group.alpha = 0f;
            if (_waitingP2Group != null)
                _waitingP2Group.alpha = 0f;
            if (_inputFieldContentGroup != null)
                _inputFieldContentGroup.alpha = 0f;
            PrepareRoundResultContentForReveal();
            SetRoundResultStripeGroupAlpha(1f);
        });

        AddRoundResultStripeRevealTween(seq);
        AddRoundResultContentRevealTween(seq);

        seq.OnComplete(() =>
        {
            if (_inputField != null)
                _inputField.DeactivateInputField();
            SetStateVisibilityImmediate(MainUIState.RoundResult);
            ConfigureRoundResultStripes();
            SetSharedPromptVisibleForRoundResult();
            SetRoundResultPlayerIconsVisible();
            SetRoundResultElementsVisible();
        });

        _currentState = MainUIState.RoundResult;
    }

    [ContextMenu("Transition To Round Result")]
    public void TransitionToRoundResult()
    {
        if (_currentState == MainUIState.Gameplay)
        {
            TransitionFromGameplayToRoundResult();
            return;
        }

        TransitionToConfiguredState(MainUIState.RoundResult);
    }

    void TransitionFromGameplayToRoundResult()
    {
        PrepareForRoundResultTransitionCleanup();
        RunRoundResultEntranceTweenSequence();
    }

    [ContextMenu("Transition To Game End")]
    public void TransitionToGameEnd()
    {
        if (_currentState == MainUIState.RoundResult)
        {
            TransitionFromRoundResultToGameEnd();
            return;
        }

        TransitionToConfiguredState(MainUIState.GameEnd);
    }

    void TransitionFromRoundResultToGameEnd()
    {
        DOTween.Kill(this);
        StopAllCoroutines();

        EnsureRoundResultElementsView();

        if (_roundResultElementsGroup != null)
        {
            _roundResultElementsGroup.alpha = 1f;
            _roundResultElementsGroup.interactable = false;
            _roundResultElementsGroup.blocksRaycasts = false;
        }
        var stripeGroup = GetDecorativeLineStateGroup();
        if (stripeGroup != null)
        {
            if (stripeGroup.transform is RectTransform stripeRect)
                stripeRect.SetAsLastSibling();
            stripeGroup.alpha = 1f;
            stripeGroup.interactable = false;
            stripeGroup.blocksRaycasts = false;
        }
        ConfigureGameEndWinnerText(0f);
        ConfigureGameEndRestartHint(0f);

        var seq = DOTween.Sequence().SetId(this);
        var fadeSeq = DOTween.Sequence().SetId(this);
        AddRoundResultFadeOutTween(fadeSeq, _promptSharedGroup);
        AddRoundResultFadeOutTween(fadeSeq, _waitingP1Group);
        AddRoundResultFadeOutTween(fadeSeq, _waitingP2Group);
        AddRoundResultFadeOutTween(fadeSeq, _roundResultP1WordText);
        AddRoundResultFadeOutTween(fadeSeq, _roundResultP2WordText);
        AddRoundResultFadeOutTween(fadeSeq, _roundResultP1ScoreText);
        AddRoundResultFadeOutTween(fadeSeq, _roundResultP2ScoreText);
        AddRoundResultFadeOutTween(fadeSeq, _roundResultP1ScoreBar != null ? _roundResultP1ScoreBar.GetComponent<Graphic>() : null);
        AddRoundResultFadeOutTween(fadeSeq, _roundResultP2ScoreBar != null ? _roundResultP2ScoreBar.GetComponent<Graphic>() : null);
        AddRoundResultFadeOutDeathLineTween(fadeSeq);
        seq.Append(fadeSeq);

        AddGameEndStripeBrushTween(seq);
        if (_roundResultPanel != null)
        {
            var panelRect = _roundResultPanel.rectTransform;
            seq.Join(panelRect.DOAnchorPos(GetGameEndBlackBarPosition(), _roundResultStripeRevealDuration).SetEase(_ease));
            seq.Join(panelRect.DOSizeDelta(GetGameEndBlackBarSize(), _roundResultStripeRevealDuration).SetEase(_ease));
        }
        if (_roundResultDeathLabelText != null)
            seq.Join(_roundResultDeathLabelText.DOFade(1f, _roundResultContentFadeDuration).SetEase(_ease));
        if (_pressSpaceGroup != null)
            seq.Join(_pressSpaceGroup.DOFade(1f, _roundResultContentFadeDuration).SetEase(_ease));

        seq.OnComplete(() =>
        {
            ConfigureGameEndWinnerText(1f);
            ConfigureGameEndRestartHint(1f);
            ConfigureGameEndStripes();
            SetStateVisibilityImmediate(MainUIState.GameEnd);
        });

        _currentState = MainUIState.GameEnd;
    }

    public void SetGameEndText(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
            _gameEndTitle = text;

        if (_roundResultDeathLabelText != null)
            _roundResultDeathLabelText.text = _gameEndTitle;
    }

    void TransitionToConfiguredState(MainUIState targetState)
    {
        DOTween.Kill(this);
        StopAllCoroutines();

        if (_hintCycler != null) _hintCycler.StopCycling();

        var previousState = _currentState;
        EnsureGeneratedStateView(previousState);
        PrepareGeneratedStateTarget(targetState);

        var seq = DOTween.Sequence().SetId(this);
        if (targetState == MainUIState.Loading)
            AppendFadeOutAllUiCanvasGroupsBeforeLoading(seq, _fadeOutDuration);
        FadeStateDifference(seq, previousState, targetState);

        var animationSet = GetStateAnimationSet(targetState);
        if (animationSet != null)
        {
            AddRectTweens(seq, animationSet.rectTargets);
            AddCanvasGroupTweens(seq, animationSet.canvasGroupTargets);
            ResetTypewriters(animationSet.typewriterTargets);
            StartCoroutine(RevealTypewritersRoutine(animationSet.typewriterTargets));
        }

        if (targetState == MainUIState.Gameplay)
            seq.OnComplete(FinishEnteringGameplayState);
    }

    IEnumerator WaitingRevealRoutine()
    {
        // Wait for the grow phase to finish before retreating the panel's bottom
        yield return new WaitForSeconds(_duration + _waitingPanelRevealDelay);

        // Phase 2: panel bottom retreats upward, revealing the InputField
        // strip underneath. Top edge stays put: anchoredPos.y += amount, sizeDelta.y -= amount.
        // (Assumes panel pivot.y = 0 / bottom.)
        if (_waitingPanel != null)
        {
            var revealAmount = GetWaitingPanelRevealAmount();
            var basePos = _waitingPanel.anchoredPosition;
            var revealedPos = basePos + new Vector2(0f, revealAmount);
            var revealedSize = new Vector2(
                _waitingPanelTargetSize.x,
                _waitingPanelTargetSize.y - revealAmount);
            _waitingPanel.DOAnchorPos(revealedPos, _waitingPanelRevealDuration).SetEase(_ease).SetId(this);
            _waitingPanel.DOSizeDelta(revealedSize, _waitingPanelRevealDuration).SetEase(_ease).SetId(this);
        }

        yield return new WaitForSeconds(_waitingPanelRevealDuration + _waitingContentGapAfterReveal);

        // Typewriter sequence: title -> room id -> hint, with P1/P2 fading in mid-sequence
        // 1) "waiting.." typewriter
        if (_waitingTitleGroup != null) _waitingTitleGroup.alpha = 1f;
        if (_waitingTitleTypewriter != null)
        {
            _waitingTitleTypewriter.Play();
            yield return new WaitUntil(() => !_waitingTitleTypewriter.IsPlaying);
        }
        yield return new WaitForSeconds(_waitingContentStagger);

        // 2) room id typewriter
        if (_waitingRoomIdGroup != null) _waitingRoomIdGroup.alpha = 1f;
        if (_waitingRoomIdTypewriter != null)
        {
            _waitingRoomIdTypewriter.Play();
            yield return new WaitUntil(() => !_waitingRoomIdTypewriter.IsPlaying);
        }
        yield return new WaitForSeconds(_waitingContentStagger);

        // 3) P1 + optional P2 fade in (host alone: only P1 until second client connects)
        if (_waitingP1Group != null)
            _waitingP1Group.DOFade(1f, _waitingContentFadeDuration).SetEase(_ease).SetId(this);
        if (_waitingP2Group != null && ShouldShowWaitingP2PlayerIcon() && !_waitingP2LobbyRevealCompleted)
        {
            _waitingP2LobbyRevealCompleted = true;
            _waitingP2Group.DOFade(1f, _waitingContentFadeDuration).SetEase(_ease).SetId(this);
        }
        else if (_waitingP2Group != null && !_waitingP2LobbyRevealCompleted)
            _waitingP2Group.alpha = 0f;
        yield return new WaitForSeconds(_waitingContentFadeDuration + _waitingContentStagger);

        // 4) "type ready to ready up" hint typewriter
        if (_waitingHintGroup != null) _waitingHintGroup.alpha = 1f;
        if (_waitingHintTypewriter != null)
        {
            _waitingHintTypewriter.Play();
            yield return new WaitUntil(() => !_waitingHintTypewriter.IsPlaying);
        }

        // Show placeholder ("ready") in the bottom strip but disable typing.
        // "ready" is captured elsewhere (key listener), the InputField is now
        // purely a visual element.
        if (_inputFieldPlaceholderText != null)
        {
            _inputFieldPlaceholderText.text = MainUiDisplayText(_waitingPlaceholder);
            _inputFieldPlaceholderText.color = _inputFieldPlaceholderColor;
        }
        if (_inputFieldContentGroup != null)
            _inputFieldContentGroup.DOFade(1f, _waitingContentFadeDuration).SetEase(_ease).SetId(this);
        UI.UIManager.Instance?.SetAnswerInputEnabled(true);
        UI.UIManager.Instance?.SetAnswerInputReadOnly(false);
        UI.UIManager.Instance?.FocusAnswerInputField();
    }

    IEnumerator RoomIdRevealRoutine(float delayBeforeReveal)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, delayBeforeReveal + _roomIdTitleDelay));
        ApplyRoomIdTextStyles();
        if (_roomIdTitleGroup != null) _roomIdTitleGroup.alpha = 1f;
        if (_roomIdTitleTypewriter != null)
        {
            _roomIdTitleTypewriter.Play();
            yield return new WaitUntil(() => !_roomIdTitleTypewriter.IsPlaying);
        }
        yield return new WaitForSeconds(_roomIdHintGapAfterTitle);
        if (_roomIdHintGroup != null) _roomIdHintGroup.alpha = 1f;
        if (_roomIdHintTypewriter != null)
        {
            _roomIdHintTypewriter.Play();
            yield return new WaitUntil(() => !_roomIdHintTypewriter.IsPlaying);
        }
    }

    void ApplyRoomIdTextStyles()
    {
        SetTypewriterText(_roomIdHintTypewriter, _roomIdHintText);
        SetTypewriterTextColor(_roomIdTitleTypewriter, _promptInkColor);
        SetTypewriterTextColor(_roomIdHintTypewriter, _promptInkColor);
    }

    void ApplyStartHintText()
    {
        SetTypewriterText(_hintTypewriter, _startHintText);
        var hintText = GetHintText();
        if (hintText != null)
        {
            ApplySingleLineOverflow(hintText);
            hintText.alignment = TextAlignmentOptions.Right;
        }
    }

    void ApplyStartInputPlaceholderState()
    {
        if (_inputFieldPlaceholderText == null) return;

        _inputFieldPlaceholderText.text = string.Empty;
        _inputFieldPlaceholderText.color = _inputFieldPlaceholderColor;
    }

    void ApplyRoomIdInputPlaceholderState()
    {
        if (_inputFieldPlaceholderText == null) return;

        _inputFieldPlaceholderText.text = MainUiDisplayText(_roomIdPlaceholder);
        _inputFieldPlaceholderText.color = _inputFieldPlaceholderColor;
    }

    void ApplyWaitingTextStyles()
    {
        SetTypewriterTextColor(_waitingTitleTypewriter, _promptPaperColor);
        SetTypewriterTextColor(_waitingRoomIdTypewriter, _roundResultMutedTextColor);
        SetTypewriterTextColor(_waitingHintTypewriter, _promptPaperColor);
        ConfigureWaitingDotsSpacing();
    }

    void SetTypewriterTextColor(TypewriterEffect typewriter, Color color)
    {
        if (typewriter == null) return;

        var text = typewriter.GetComponent<TMP_Text>();
        if (text != null)
        {
            ApplySingleLineOverflow(text);
            text.color = color;
        }
    }

    void SetTypewriterText(TypewriterEffect typewriter, string value)
    {
        if (typewriter == null) return;

        var text = typewriter.GetComponent<TMP_Text>();
        if (text != null)
        {
            ApplySingleLineOverflow(text);
            text.text = MainUiDisplayText(value ?? string.Empty);
        }
    }

    void ConfigureWaitingDotsSpacing()
    {
        if (_waitingTitleGroup == null || !(_waitingTitleGroup.transform is RectTransform root)) return;

        var waitingText = FindChildRect(root, "waitingUI");
        var dots = FindChildRect(root, "....UI");
        if (dots == null) return;

        if (waitingText != null)
        {
            var rightEdge = waitingText.anchoredPosition.x + waitingText.sizeDelta.x * (1f - waitingText.pivot.x);
            dots.anchoredPosition = new Vector2(rightEdge + 58f, dots.anchoredPosition.y);
        }

        var layout = dots.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
            layout.spacing = 52.66f;
    }

    IEnumerator TutorialRevealRoutine()
    {
        yield return new WaitForSeconds(_tutorialTitleDelay);
        if (_tutorialTitleGroup != null) _tutorialTitleGroup.alpha = 1f;
        if (_tutorialTitleTypewriter != null)
        {
            _tutorialTitleTypewriter.Play();
            yield return new WaitUntil(() => !_tutorialTitleTypewriter.IsPlaying);
        }
        yield return new WaitForSeconds(_pressSpaceGapAfterTitle);
        if (_pressSpaceGroup != null)
            _pressSpaceGroup.DOFade(1f, _pressSpaceFadeDuration).SetEase(_ease).SetId(this);
    }

    [ContextMenu("Reset To Start")]
    public void ResetToStart()
    {
        DOTween.Kill(this);
        StopAllCoroutines();

        _cmykBar.pivot = new Vector2(0.5f, 0.5f);
        _cmykBar.anchoredPosition = _initBarPos;
        _cmykBar.sizeDelta = _initBarSize;
        _layoutM.preferredWidth = _initMWidth;
        _layoutY.preferredWidth = _initYWidth;
        _layoutC.preferredWidth = _initCWidth;
        _graphicM.Skew = _initSkew;
        _graphicY.Skew = _initSkew;
        _graphicC.Skew = _initSkew;
        _graphicK.Skew = _initSkew;

        if (_inputField != null) { _inputField.enabled = true; _inputField.readOnly = false; }
        if (_inputFieldRect != null)
        {
            _inputFieldRect.anchoredPosition = _initInputFieldAnchoredPos;
            _inputFieldRect.sizeDelta = _initInputFieldSize;
        }
        if (_inputFieldContentGroup != null) _inputFieldContentGroup.alpha = 1f;

        ApplyStartDecorativeLineLayoutImmediate();

        SetStateVisibilityImmediate(MainUIState.Start);

        RestoreStartTitleInitialPosition();
        RestoreStartHintInitialColor();
        ApplyStartHintText();
        ApplyStartInputPlaceholderState();
        if (_titleTypewriter != null) _titleTypewriter.ShowAll();
        if (_hintTypewriter != null) _hintTypewriter.ShowAll();

        if (_tutorialTitleGroup != null) _tutorialTitleGroup.alpha = 0f;
        if (_tutorialTitleTypewriter != null) _tutorialTitleTypewriter.Hide();
        if (_pressSpaceGroup != null) _pressSpaceGroup.alpha = 0f;

        if (_roomIdTitleGroup != null) _roomIdTitleGroup.alpha = 0f;
        if (_roomIdHintGroup != null) _roomIdHintGroup.alpha = 0f;
        if (_roomIdTitleTypewriter != null) _roomIdTitleTypewriter.Hide();
        if (_roomIdHintTypewriter != null) _roomIdHintTypewriter.Hide();

        if (_waitingPanel != null)
        {
            _waitingPanel.anchoredPosition = _waitingPanelStartAnchoredPos;
            _waitingPanel.sizeDelta = _waitingPanelStartSize;
        }
        if (_waitingTitleGroup != null) _waitingTitleGroup.alpha = 0f;
        if (_waitingTitleTypewriter != null) _waitingTitleTypewriter.Hide();
        if (_waitingRoomIdGroup != null) _waitingRoomIdGroup.alpha = 0f;
        if (_waitingRoomIdTypewriter != null) _waitingRoomIdTypewriter.Hide();
        if (_waitingHintGroup != null) _waitingHintGroup.alpha = 0f;
        if (_waitingHintTypewriter != null) _waitingHintTypewriter.Hide();
        _waitingP2LobbyRevealCompleted = false;
        ConfigureWaitingPlayerIconsLayout();
        if (_waitingP1Group != null) _waitingP1Group.alpha = 0f;
        if (_waitingP2Group != null) _waitingP2Group.alpha = 0f;
        DismissLoadingOverlayImmediate();

        ApplyStartInputPlaceholderState();

        EnsurePromptSharedView();
        PreparePromptShowcaseStart();
        if (_promptSharedGroup != null)
            _promptSharedGroup.alpha = 0f;

        EnsureGameplayElementsView();
        PrepareGameplayStart();
        if (_gameplayElementsGroup != null)
            _gameplayElementsGroup.alpha = 0f;
    }

    void EnsurePromptSharedView()
    {
        if (_promptSharedGroupRect == null)
            _promptSharedGroupRect = FindPromptSharedRoot(true);
        if (_promptSharedGroupRect == null)
            _promptSharedGroupRect = CreateRect(PromptSharedGroupName, transform);
        if (_promptSharedGroupRect == null) return;

        StretchToParent(_promptSharedGroupRect);

        if (_promptSharedGroup == null)
            _promptSharedGroup = _promptSharedGroupRect.GetComponent<CanvasGroup>();
        if (_promptSharedGroup == null)
        {
            if (!CanCreatePrefabOwnedUi($"{PromptSharedGroupName} CanvasGroup")) return;
            _promptSharedGroup = _promptSharedGroupRect.gameObject.AddComponent<CanvasGroup>();
        }

        if (_promptSharedBackground == null)
        {
            var promptBackgroundRect = FindChildRect(_promptSharedGroupRect, "PromptBackground");
            if (promptBackgroundRect != null)
                _promptSharedBackground = promptBackgroundRect.GetComponent<Image>();
        }
        if (_promptSharedBackground != null)
        {
            StretchToParent(_promptSharedBackground.rectTransform);
            _promptSharedBackground.gameObject.SetActive(false);
        }

        var fontSource = GetComponentInChildren<TMP_Text>(true);
        _promptTitleText = GetOrCreatePromptText(
            _promptTitleText,
            _promptSharedGroupRect,
            "PromptTitleText",
            _promptText,
            GetPromptTitlePosition(),
            new Vector2(1400f, 260f),
            184f,
            fontSource,
            TextAlignmentOptions.Left);
        _promptBannedText = GetOrCreatePromptText(
            _promptBannedText,
            _promptSharedGroupRect,
            "PromptBannedText",
            "banned letter \"i\"",
            GetPromptBannedTextPosition(),
            new Vector2(1100f, 100f),
            58f,
            fontSource,
            TextAlignmentOptions.Left);
        if (_promptPromptMask == null)
            _promptPromptMask = GetOrCreateImage("PromptMainBlackMask", _promptSharedGroupRect, _promptInkColor).rect;
        if (_promptBannedMask == null)
            _promptBannedMask = GetOrCreateImage("PromptBannedBlackMask", _promptSharedGroupRect, _promptInkColor).rect;
    }

    void EnsurePromptCalibrationOverlayView(bool createMissing)
    {
        if (_designOverlayRoot == null)
            _designOverlayRoot = FindChildRect(transform, DesignOverlayGroupName);
        if (_designOverlayRoot == null && createMissing)
            _designOverlayRoot = CreateRect(DesignOverlayGroupName, transform);
        if (_designOverlayRoot == null) return;

        ConfigureDesignOverlayRoot(_designOverlayRoot);

        if (_promptCalibrationOverlayRoot == null)
            _promptCalibrationOverlayRoot = FindChildRect(_designOverlayRoot, PromptCalibrationOverlayName);
        if (_promptCalibrationOverlayRoot == null && createMissing)
            _promptCalibrationOverlayRoot = CreateRect(PromptCalibrationOverlayName, _designOverlayRoot);
        if (_promptCalibrationOverlayRoot == null) return;

        ConfigureDesignReferenceSpaceRect(_promptCalibrationOverlayRoot);
        _promptCalibrationOverlayRoot.SetAsLastSibling();

        _promptTitleVisualBoundsOverlay = EnsureCalibrationBoundsImage(
            _promptTitleVisualBoundsOverlay,
            "PromptTitleVisualBounds",
            _promptTitleBoundsOverlayColor,
            createMissing);
        _promptBannedVisualBoundsOverlay = EnsureCalibrationBoundsImage(
            _promptBannedVisualBoundsOverlay,
            "PromptBannedVisualBounds",
            _promptBannedBoundsOverlayColor,
            createMissing);
    }

    RectTransform EnsureCalibrationBoundsImage(RectTransform current, string childName, Color color, bool createMissing)
    {
        if (_promptCalibrationOverlayRoot == null) return current;

        if (current == null)
            current = FindChildRect(_promptCalibrationOverlayRoot, childName);
        if (current == null && createMissing)
            current = CreateRect(childName, _promptCalibrationOverlayRoot);
        if (current == null) return null;

        var image = current.GetComponent<Image>();
        if (image == null && createMissing)
        {
            if (!CanCreatePrefabOwnedUi($"{childName} Image")) return current;
            image = current.gameObject.AddComponent<Image>();
        }
        if (image != null)
        {
            image.color = color;
            image.raycastTarget = false;
        }

        current.gameObject.SetActive(_showPromptCalibrationOverlay);
        return current;
    }

    void ApplyPromptCalibrationOverlayVisibility()
    {
        if (_designOverlayRoot == null) return;

        if (_showPromptCalibrationOverlay && !_designOverlayRoot.gameObject.activeSelf)
            _designOverlayRoot.gameObject.SetActive(true);
        SetExistingDesignReferenceImagesVisible(_showPromptCalibrationOverlay && _showExistingDesignReferenceImages);
        if (_promptCalibrationOverlayRoot != null)
            _promptCalibrationOverlayRoot.gameObject.SetActive(_showPromptCalibrationOverlay);
        if (_promptTitleVisualBoundsOverlay != null)
            _promptTitleVisualBoundsOverlay.gameObject.SetActive(_showPromptCalibrationOverlay);
        if (_promptBannedVisualBoundsOverlay != null)
            _promptBannedVisualBoundsOverlay.gameObject.SetActive(_showPromptCalibrationOverlay);
    }

    void HidePromptCalibrationOverlay()
    {
        HidePromptCalibrationBounds();
        SetExistingDesignReferenceImagesVisible(false);
    }

    void HidePromptCalibrationBounds()
    {
        if (_promptCalibrationOverlayRoot != null)
            _promptCalibrationOverlayRoot.gameObject.SetActive(false);
        if (_promptTitleVisualBoundsOverlay != null)
            _promptTitleVisualBoundsOverlay.gameObject.SetActive(false);
        if (_promptBannedVisualBoundsOverlay != null)
            _promptBannedVisualBoundsOverlay.gameObject.SetActive(false);
    }

    void SetExistingDesignReferenceImagesVisible(bool visible)
    {
        if (_designOverlayRoot == null) return;

        var selectedIndex = visible ? GetActiveDesignReferenceImageIndex() : -1;
        var referenceIndex = 0;
        for (var i = 0; i < _designOverlayRoot.childCount; i++)
        {
            var child = _designOverlayRoot.GetChild(i);
            if (child == null || child.name == PromptCalibrationOverlayName) continue;
            if (child.name.StartsWith("tempDesign", System.StringComparison.Ordinal))
            {
                child.gameObject.SetActive(referenceIndex == selectedIndex);
                referenceIndex++;
            }
        }
    }

    int GetActiveDesignReferenceImageIndex()
    {
        var count = CountDesignReferenceImages();
        if (count <= 0) return -1;

        var index = _autoSelectDesignReferenceForCurrentState
            ? GetDesignReferenceImageIndexForState(_currentState)
            : _promptCalibrationReferenceImageIndex;

        if (index < 0 || index >= count) return -1;
        return index;
    }

    int GetDesignReferenceImageIndexForState(MainUIState state)
    {
        if (_stateDesignReferences != null)
        {
            foreach (var reference in _stateDesignReferences)
            {
                if (reference != null && reference.state == state)
                    return reference.referenceImageIndex;
            }
        }

        switch (state)
        {
            case MainUIState.Start:
                return 0;
            case MainUIState.Tutorial:
                return 1;
            case MainUIState.RoomId:
                return 2;
            case MainUIState.Waiting:
                return 3;
            case MainUIState.Gameplay:
                return 4;
            default:
                return -1;
        }
    }

    int CountDesignReferenceImages()
    {
        if (_designOverlayRoot == null)
            _designOverlayRoot = FindChildRect(transform, DesignOverlayGroupName);
        if (_designOverlayRoot == null) return 0;

        var count = 0;
        for (var i = 0; i < _designOverlayRoot.childCount; i++)
        {
            var child = _designOverlayRoot.GetChild(i);
            if (child != null && child.name.StartsWith("tempDesign", System.StringComparison.Ordinal))
                count++;
        }

        return count;
    }

    void UpdateTextVisualBoundsOverlay(TMP_Text text, RectTransform overlayRect, Color color)
    {
        if (text == null || overlayRect == null || _promptCalibrationOverlayRoot == null) return;

        if (!TryGetTextVisualBoundsInOverlaySpace(text, _promptCalibrationOverlayRoot, out var center, out var size))
        {
            overlayRect.gameObject.SetActive(false);
            return;
        }

        overlayRect.gameObject.SetActive(_showPromptCalibrationOverlay);
        ConfigureRect(overlayRect, center, size, new Vector2(0.5f, 0.5f));
        var image = overlayRect.GetComponent<Image>();
        if (image != null)
        {
            image.color = color;
            image.raycastTarget = false;
        }
    }

    bool TryGetTextVisualBoundsInOverlaySpace(TMP_Text text, RectTransform overlayRoot, out Vector2 center, out Vector2 size)
    {
        center = Vector2.zero;
        size = Vector2.zero;
        if (text == null || overlayRoot == null) return false;

        text.ForceMeshUpdate();
        var bounds = text.textBounds;
        if (bounds.size.x <= 0.01f || bounds.size.y <= 0.01f) return false;

        var textTransform = text.rectTransform;
        var localMin = bounds.min;
        var localMax = bounds.max;
        var corners = new[]
        {
            new Vector3(localMin.x, localMin.y, 0f),
            new Vector3(localMin.x, localMax.y, 0f),
            new Vector3(localMax.x, localMax.y, 0f),
            new Vector3(localMax.x, localMin.y, 0f)
        };

        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (var i = 0; i < corners.Length; i++)
        {
            var world = textTransform.TransformPoint(corners[i]);
            var local = overlayRoot.InverseTransformPoint(world);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        center = (min + max) * 0.5f;
        size = max - min;
        return size.x > 0.01f && size.y > 0.01f;
    }

    void ConfigureDesignOverlayRoot(RectTransform rect)
    {
        if (rect == null) return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    void ConfigureDesignReferenceSpaceRect(RectTransform rect)
    {
        if (rect == null) return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1920f, 1080f);
        rect.localScale = Vector3.one;
    }

    void ConfigureStretchRect(RectTransform rect)
    {
        if (rect == null) return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    void PreparePromptShowcaseStart(bool? showBannedLettersInShowcase = null)
    {
        _showBannedLettersInActivePromptShowcase = showBannedLettersInShowcase ?? HasPromptBannedLetters();

        EnsurePromptSharedView();

        if (_promptSharedGroup != null)
        {
            _promptSharedGroup.alpha = 1f;
            _promptSharedGroup.interactable = false;
            _promptSharedGroup.blocksRaycasts = false;
        }

        SetOptionalGameObjectActive(_promptSharedBackground, false);

        if (_promptTitleText != null)
        {
            _promptTitleText.richText = false;
            _promptTitleText.overrideColorTags = true;
            _promptTitleText.color = _promptMaskTitleColor;
            _promptTitleText.text = MainUiDisplayText(GetPromptShowcaseMaskPhaseTitleFromCurrentRound());
            _promptTitleText.alpha = 0f;
            _promptTitleText.fontSize = 184f;
            _promptTitleText.alignment = TextAlignmentOptions.Left;
            ConfigureRect(_promptTitleText.rectTransform, GetPromptTitlePosition(), new Vector2(1400f, 260f), new Vector2(0.5f, 0.5f));
            _promptTitleText.transform.SetAsLastSibling();
        }
        if (_promptBannedText != null)
        {
            _promptBannedText.richText = false;
            _promptBannedText.overrideColorTags = true;
            _promptBannedText.color = _promptMaskBannedTextColor;
            _promptBannedText.text = MainUiDisplayText(_promptMaskBannedTextValue);
            _promptBannedText.alpha = 0f;
            _promptBannedText.fontSize = 58f;
            _promptBannedText.alignment = TextAlignmentOptions.Left;
            ConfigureRect(_promptBannedText.rectTransform, GetPromptBannedTextPosition(), new Vector2(1100f, 100f), new Vector2(0.5f, 0.5f));
            _promptBannedText.transform.SetAsLastSibling();
        }

        if (_promptPromptMask != null)
        {
            _promptPromptMask.gameObject.SetActive(true);
            _promptPromptMask.anchorMin = new Vector2(0.5f, 0.5f);
            _promptPromptMask.anchorMax = new Vector2(0.5f, 0.5f);
            _promptPromptMask.pivot = new Vector2(0.5f, 0.5f);
            _promptPromptMask.anchoredPosition = new Vector2(GetPromptMaskMainStartX(), 65f);
            _promptPromptMask.sizeDelta = new Vector2(5000f, 397f);
            _promptPromptMask.localScale = Vector3.one;
            _promptPromptMask.transform.SetSiblingIndex(Mathf.Max(0, _promptSharedGroupRect.childCount - 3));
        }

        if (_promptBannedMask != null)
        {
            _promptBannedMask.gameObject.SetActive(_showBannedLettersInActivePromptShowcase);
            _promptBannedMask.anchorMin = new Vector2(0.5f, 0.5f);
            _promptBannedMask.anchorMax = new Vector2(0.5f, 0.5f);
            _promptBannedMask.pivot = new Vector2(0.5f, 0.5f);
            _promptBannedMask.anchoredPosition = new Vector2(GetPromptMaskBannedStartX(), -185f);
            _promptBannedMask.sizeDelta = new Vector2(1980f, 133f);
            _promptBannedMask.localScale = Vector3.one;
            _promptBannedMask.transform.SetSiblingIndex(Mathf.Max(0, _promptSharedGroupRect.childCount - 3));
        }
    }

    /// <summary>Mask-phase title before <see cref="SetPromptTextForReveal"/>: this round's <see cref="PromptGenerator.PromptType"/> (same spacing rule as <see cref="PromptGenerator.Prompt.ToString"/> type half), or serialized <see cref="_promptMaskText"/> if unavailable.</summary>
    string GetPromptShowcaseMaskPhaseTitleFromCurrentRound()
    {
        var pg = FindAnyObjectByType<PromptGenerator>();
        if (pg == null)
            return _promptMaskText;

        var type = pg.CurrentPrompt.Value.type;
        if (type == PromptGenerator.PromptType.None)
            return _promptMaskText;

        return Regex.Replace(type.ToString(), "([a-z])([A-Z])", "$1 $2");
    }

    float GetPromptMaskMainTargetX() => 1480f;
    float GetPromptMaskMainStartX() => GetPromptMaskMainTargetX() - 5000f;
    float GetPromptMaskBannedTargetX() => -30f;
    float GetPromptMaskBannedStartX() => GetPromptMaskBannedTargetX() - 1980f;
    Vector2 GetPromptTitlePosition() => new Vector2(24f, 65f);
    Vector2 GetPromptBannedTextPosition() => new Vector2(297f, -185f);

    void SetPromptTextForReveal()
    {
        if (_debugPromptFlow)
            Debug.Log($"[MainUIController] SetPromptTextForReveal prompt='{_promptText}' banned='{_promptBannedLetters}'");
        var showBanned = _showBannedLettersInActivePromptShowcase;
        if (_promptTitleText != null)
        {
            _promptTitleText.richText = true;
            _promptTitleText.overrideColorTags = false;
            var titleSource = showBanned ? GetPromptTextWithBannedLetters(_promptText) : _promptText;
            _promptTitleText.text = MainUiDisplayText(titleSource);
            _promptTitleText.color = _promptMaskTitleColor;
            _promptTitleText.alignment = TextAlignmentOptions.Left;
            _promptTitleText.alpha = 1f;
        }
        if (_promptBannedText != null)
        {
            _promptBannedText.richText = true;
            _promptBannedText.overrideColorTags = false;
            _promptBannedText.text = MainUiDisplayText(GetBannedLetterRevealText());
            _promptBannedText.color = _promptMaskBannedTextColor;
            _promptBannedText.alignment = TextAlignmentOptions.Left;
            _promptBannedText.alpha = showBanned ? 1f : 0f;
        }
    }

    public void SetPromptForShowcase(string promptText, string bannedLetters)
    {
        if (!string.IsNullOrWhiteSpace(promptText))
            _promptText = promptText;
        _promptBannedLetters = string.IsNullOrWhiteSpace(bannedLetters) ? _promptBannedLetters : bannedLetters;
    }

    bool HasPromptBannedLetters()
    {
        return !string.IsNullOrWhiteSpace(_promptBannedLetters);
    }

    string GetBannedLetterRevealText()
    {
        var colorHex = ColorUtility.ToHtmlStringRGB(_promptBannedLetterColor);
        var coloredLetters = $"<size=150%><color=#{colorHex}>{_promptBannedLetters}</color></size>";
        return _promptBannedLetters.Length == 1
            ? $"banned letter \"{coloredLetters}\""
            : $"banned letters \"{coloredLetters}\"";
    }

    string GetPromptTextWithBannedLetters(string source)
    {
        if (string.IsNullOrEmpty(source) || !HasPromptBannedLetters())
            return source;

        var colorHex = ColorUtility.ToHtmlStringRGB(_promptBannedLetterColor);
        var result = new StringBuilder(source.Length);
        foreach (var c in source)
        {
            if (IsBannedPromptLetter(c))
                result.Append("<color=#").Append(colorHex).Append(">").Append(c).Append("</color>");
            else
                result.Append(c);
        }

        return result.ToString();
    }

    bool IsBannedPromptLetter(char c)
    {
        if (!HasPromptBannedLetters())
            return false;

        foreach (var banned in _promptBannedLetters)
        {
            if (char.ToUpperInvariant(c) == char.ToUpperInvariant(banned))
                return true;
        }

        return false;
    }

    (RectTransform rect, Image image) GetOrCreateImage(string childName, RectTransform parent, Color color)
    {
        var rect = FindChildRect(parent, childName);
        if (rect == null)
            rect = CreateRect(childName, parent);
        if (rect == null) return (null, null);

        var image = rect.GetComponent<Image>();
        if (image == null)
        {
            if (!CanCreatePrefabOwnedUi($"{childName} Image")) return (rect, null);
            image = rect.gameObject.AddComponent<Image>();
        }

        image.color = color;
        image.raycastTarget = false;
        return (rect, image);
    }

    TMP_Text GetOrCreatePromptText(TMP_Text current, RectTransform parent, string childName, string text, Vector2 anchoredPos, Vector2 size, float fontSize, TMP_Text fontSource, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        if (current == null)
        {
            var rect = FindChildRect(parent, childName);
            if (rect == null)
                rect = CreateRect(childName, parent);
            if (rect == null) return null;

            current = rect.GetComponent<TMP_Text>();
            if (current == null)
            {
                if (!CanCreatePrefabOwnedUi($"{childName} TextMeshProUGUI")) return null;
                current = rect.gameObject.AddComponent<TextMeshProUGUI>();
            }
        }

        var textRect = current.rectTransform;
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = anchoredPos;
        textRect.sizeDelta = size;
        textRect.localScale = Vector3.one;

        current.text = MainUiDisplayText(text);
        current.color = _promptInkColor;
        current.fontSize = fontSize;
        current.enableAutoSizing = false;
        current.richText = true;
        current.alignment = alignment;
        ApplySingleLineOverflow(current);
        current.raycastTarget = false;
        if (fontSource != null && fontSource.font != null)
        {
            current.font = fontSource.font;
            current.fontSharedMaterial = fontSource.fontSharedMaterial;
        }

        return current;
    }

    void EnsureGameplayElementsView()
    {
        if (_gameplayElementsGroupRect == null)
            _gameplayElementsGroupRect = FindGameplayElementsRoot(true);
        if (_gameplayElementsGroupRect == null)
            _gameplayElementsGroupRect = CreateRect(GameplayElementsGroupName, transform);
        if (_gameplayElementsGroupRect == null) return;

        StretchToParent(_gameplayElementsGroupRect);

        if (_gameplayElementsGroup == null)
            _gameplayElementsGroup = _gameplayElementsGroupRect.GetComponent<CanvasGroup>();
        if (_gameplayElementsGroup == null)
        {
            if (!CanCreatePrefabOwnedUi($"{GameplayElementsGroupName} CanvasGroup")) return;
            _gameplayElementsGroup = _gameplayElementsGroupRect.gameObject.AddComponent<CanvasGroup>();
        }

        if (_gameplayBackground == null)
        {
            var gameplayBackgroundRect = FindChildRect(_gameplayElementsGroupRect, "GameplayBackground");
            if (gameplayBackgroundRect != null)
                _gameplayBackground = gameplayBackgroundRect.GetComponent<Image>();
        }
        if (_gameplayBackground != null)
        {
            StretchToParent(_gameplayBackground.rectTransform);
            _gameplayBackground.gameObject.SetActive(false);
        }

        var fontSource = GetComponentInChildren<TMP_Text>(true);

        if (_gameplayTimerBar == null)
            _gameplayTimerBar = GetOrCreateImage("GameplayTimerBar", _gameplayElementsGroupRect, new Color(0.52156866f, 0.52156866f, 0.52156866f, 1f)).rect;

        if (_gameplayP1LetterGroup == null)
            _gameplayP1LetterGroup = CreateRect("GameplayP1LetterGroup", _gameplayElementsGroupRect);
        if (_gameplayP2LetterGroup == null)
            _gameplayP2LetterGroup = CreateRect("GameplayP2LetterGroup", _gameplayElementsGroupRect);

        // Do not call PrepareGameplayInputFieldStart here: this method runs from UpdateGameplayRoundTimer /
        // letter-block updates every frame; resetting input alpha to 0 here kept the shared field invisible.
        HideDeprecatedGameplayPlayerLabelCopies();
    }

    void PrepareGameplayStart()
    {
        EnsureGameplayElementsView();

        if (_gameplayElementsGroup != null)
        {
            _gameplayElementsGroup.alpha = 0f;
            _gameplayElementsGroup.interactable = false;
            _gameplayElementsGroup.blocksRaycasts = false;
        }

        SetOptionalGameObjectActive(_gameplayBackground, false);

        ConfigureRect(_gameplayTimerBar, new Vector2(-900f, -322.5f), new Vector2(GameplayTimerBarFullWidth, 35f), new Vector2(0f, 0.5f));
        ConfigureRect(_gameplayP1LetterGroup, GetGameplayP1LetterGroupPosition(), new Vector2(520f, 50f), new Vector2(0f, 0.5f));
        ConfigureRect(_gameplayP2LetterGroup, GetGameplayP2LetterGroupPosition(), new Vector2(520f, 50f), new Vector2(0f, 0.5f));

        _gameplayP1SyncedLetterCount = -1;
        _gameplayP2SyncedLetterCount = -1;
        if (_gameplayP1LetterGroup != null) _gameplayP1LetterGroup.localScale = Vector3.one;
        if (_gameplayP2LetterGroup != null) _gameplayP2LetterGroup.localScale = Vector3.one;

        RefreshGameplayLetterBlocks();
        PrepareGameplayInputFieldStart();
        PrepareGameplayPlayerIconsStart();
        HideDeprecatedGameplayPlayerLabelCopies();
    }

    void EnsureRoundResultElementsView()
    {
        if (_roundResultElementsGroupRect == null)
            _roundResultElementsGroupRect = FindRoundResultElementsRoot();
        if (_roundResultElementsGroupRect == null)
            _roundResultElementsGroupRect = CreateRect(RoundResultElementsGroupName, transform);
        if (_roundResultElementsGroupRect == null) return;

        StretchToParent(_roundResultElementsGroupRect);
        RemoveDeprecatedRoundResultStripeCopies();

        if (_roundResultElementsGroup == null)
            _roundResultElementsGroup = _roundResultElementsGroupRect.GetComponent<CanvasGroup>();
        if (_roundResultElementsGroup == null)
        {
            if (!CanCreatePrefabOwnedUi($"{RoundResultElementsGroupName} CanvasGroup")) return;
            _roundResultElementsGroup = _roundResultElementsGroupRect.gameObject.AddComponent<CanvasGroup>();
        }

        var fontSource = GetComponentInChildren<TMP_Text>(true);
        _roundResultPanel = GetOrCreateImage("RoundResultPanel", _roundResultElementsGroupRect, _promptInkColor).image;
        _roundResultP1WordText = GetOrCreatePromptText(_roundResultP1WordText, _roundResultElementsGroupRect, "RoundResultP1WordText", _roundResultP1Word, GetRoundResultP1WordPosition(), new Vector2(520f, 70f), 49f, fontSource, TextAlignmentOptions.Left);
        _roundResultP2WordText = GetOrCreatePromptText(_roundResultP2WordText, _roundResultElementsGroupRect, "RoundResultP2WordText", _roundResultP2Word, GetRoundResultP2WordPosition(), new Vector2(520f, 70f), 49f, fontSource, TextAlignmentOptions.Left);
        _roundResultDeathLabelText = GetOrCreatePromptText(_roundResultDeathLabelText, _roundResultElementsGroupRect, "RoundResultDeathLabelText", "death.", Vector2.zero, new Vector2(220f, 60f), 36f, fontSource, TextAlignmentOptions.Left);
        _roundResultP1ScoreText = GetOrCreatePromptText(_roundResultP1ScoreText, _roundResultElementsGroupRect, "RoundResultP1ScoreText", _roundResultP1Score.ToString(), Vector2.zero, new Vector2(120f, 70f), 49f, fontSource, TextAlignmentOptions.Center);
        _roundResultP2ScoreText = GetOrCreatePromptText(_roundResultP2ScoreText, _roundResultElementsGroupRect, "RoundResultP2ScoreText", _roundResultP2Score.ToString(), Vector2.zero, new Vector2(120f, 70f), 49f, fontSource, TextAlignmentOptions.Center);

        if (_roundResultP1ScoreBar == null)
            _roundResultP1ScoreBar = GetOrCreateImage("RoundResultP1ScoreBar", _roundResultElementsGroupRect, _roundResultTextColor).rect;
        if (_roundResultP2ScoreBar == null)
            _roundResultP2ScoreBar = GetOrCreateImage("RoundResultP2ScoreBar", _roundResultElementsGroupRect, _roundResultTextColor).rect;
        if (_roundResultDeathLineGroup == null)
            _roundResultDeathLineGroup = FindChildRect(_roundResultElementsGroupRect, "RoundResultDeathLineGroup");
        if (_roundResultDeathLineGroup == null)
            _roundResultDeathLineGroup = CreateRect("RoundResultDeathLineGroup", _roundResultElementsGroupRect);

        EnsureRoundResultDeathLineSegments();
    }

    void PrepareRoundResultStart()
    {
        EnsureRoundResultElementsView();

        if (_roundResultElementsGroup != null)
        {
            _roundResultElementsGroup.alpha = 0f;
            _roundResultElementsGroup.interactable = false;
            _roundResultElementsGroup.blocksRaycasts = false;
        }

        ConfigureRoundResultElements();
    }

    void SetRoundResultElementsVisible()
    {
        EnsureRoundResultElementsView();
        ConfigureRoundResultElements();
        ConfigureRoundResultStripes();

        if (_roundResultElementsGroup != null)
        {
            _roundResultElementsGroup.alpha = 1f;
            _roundResultElementsGroup.interactable = false;
            _roundResultElementsGroup.blocksRaycasts = false;
        }
    }

    void PrepareRoundResultTransitionStart()
    {
        if (_roundResultElementsGroup != null)
        {
            _roundResultElementsGroup.alpha = 1f;
            _roundResultElementsGroup.interactable = false;
            _roundResultElementsGroup.blocksRaycasts = false;
        }

        ConfigureRoundResultElements();
        SetRoundResultPanelAlpha(0f);
        SetRoundResultContentAlpha(0f);
        PrepareRoundResultStripesForReveal();

        if (_inputField != null)
        {
            _inputField.readOnly = true;
            _inputField.interactable = false;
        }
        if (_inputFieldContentGroup != null)
        {
            _inputFieldContentGroup.interactable = false;
            _inputFieldContentGroup.blocksRaycasts = false;
        }
        if (_inputFieldRect != null && _inputField.targetGraphic != null)
            _inputField.targetGraphic.color = _promptInkColor;
    }

    void PrepareRoundResultContentForReveal()
    {
        SetSharedPromptVisibleForRoundResult();
        SetRoundResultPlayerIconsVisible();
        ConfigureRoundResultElements();
        SetRoundResultPanelAlpha(1f);
        SetRoundResultContentAlpha(0f);
        PrepareRoundResultTypewriterText(_promptTitleText);
        PrepareRoundResultTypewriterText(_promptBannedText);
    }

    void SetRoundResultPanelAlpha(float alpha)
    {
        if (_roundResultPanel == null) return;

        SetGraphicAlpha(_roundResultPanel, alpha);
    }

    void SetRoundResultContentAlpha(float alpha)
    {
        SetTextAlpha(_promptTitleText, alpha);
        SetTextAlpha(_promptBannedText, alpha);
        SetCanvasGroupAlpha(_waitingP1Group, alpha);
        SetCanvasGroupAlpha(_waitingP2Group, alpha);
        SetTextAlpha(_roundResultP1WordText, alpha);
        SetTextAlpha(_roundResultP2WordText, alpha);
        SetTextAlpha(_roundResultDeathLabelText, alpha);
        SetTextAlpha(_roundResultP1ScoreText, alpha);
        SetTextAlpha(_roundResultP2ScoreText, alpha);
        SetGraphicAlpha(_roundResultP1ScoreBar != null ? _roundResultP1ScoreBar.GetComponent<Graphic>() : null, alpha);
        SetGraphicAlpha(_roundResultP2ScoreBar != null ? _roundResultP2ScoreBar.GetComponent<Graphic>() : null, alpha);
        SetRoundResultDeathLineAlpha(alpha);
    }

    void PrepareRoundResultStripesForReveal()
    {
        if (_decorativeLines == null || _decorativeLines.Length < 3) return;

        PrepareRoundResultStripeForReveal(_decorativeLines[0]?.rect, GetRoundResultStripeStartPosition(), GetRoundResultStripeColor(0));
        PrepareRoundResultStripeForReveal(_decorativeLines[1]?.rect, GetRoundResultStripePosition(0), GetRoundResultStripeColor(1));
        PrepareRoundResultStripeForReveal(_decorativeLines[2]?.rect, GetRoundResultStripePosition(1), GetRoundResultStripeColor(2));

        var stripeGroup = GetDecorativeLineStateGroup();
        if (stripeGroup != null)
        {
            stripeGroup.alpha = 0f;
            stripeGroup.interactable = false;
            stripeGroup.blocksRaycasts = false;
        }
    }

    void SetRoundResultStripeGroupAlpha(float alpha)
    {
        var stripeGroup = GetDecorativeLineStateGroup();
        if (stripeGroup != null)
            stripeGroup.alpha = alpha;
    }

    void PrepareRoundResultStripeForReveal(RectTransform rect, Vector2 leftCenterPosition, Color color)
    {
        if (rect == null) return;

        ConfigureRect(rect, leftCenterPosition, new Vector2(1800f, 20f), new Vector2(0.5f, 0.5f));
        rect.gameObject.SetActive(true);
        var graphic = rect.GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.color = color;
            graphic.raycastTarget = false;
        }
    }

    void AddRoundResultStripeRevealTween(Sequence seq)
    {
        if (seq == null || _decorativeLines == null || _decorativeLines.Length < 3) return;

        var stripeSeq = DOTween.Sequence().SetId(this);
        AddRoundResultStripeMoveTween(stripeSeq, _decorativeLines[0]?.rect, GetRoundResultStripePosition(0));
        AddRoundResultStripeMoveTween(stripeSeq, _decorativeLines[1]?.rect, GetRoundResultStripePosition(1));
        AddRoundResultStripeMoveTween(stripeSeq, _decorativeLines[2]?.rect, GetRoundResultStripePosition(2));
        seq.Append(stripeSeq);
    }

    void AddRoundResultStripeMoveTween(Sequence stripeSeq, RectTransform rect, Vector2 targetPosition)
    {
        if (stripeSeq == null || rect == null) return;

        stripeSeq.Append(rect.DOAnchorPos(targetPosition, _roundResultStripeRevealDuration).SetEase(_ease));
    }

    void AddRoundResultContentRevealTween(Sequence seq)
    {
        if (seq == null) return;

        var contentSeq = DOTween.Sequence().SetId(this);
        AddRoundResultTypewriterTween(contentSeq, _promptTitleText);
        AddRoundResultTypewriterTween(contentSeq, _promptBannedText);
        AddRoundResultFadeTween(contentSeq, _waitingP1Group);
        AddRoundResultFadeTween(contentSeq, _roundResultP1WordText);
        AddRoundResultFadeTween(contentSeq, _waitingP2Group);
        AddRoundResultFadeTween(contentSeq, _roundResultP2WordText);
        AddRoundResultFadeTween(contentSeq, _roundResultDeathLabelText);
        AddRoundResultDeathLineFadeTween(contentSeq);
        AddRoundResultFadeTween(contentSeq, _roundResultP1ScoreBar != null ? _roundResultP1ScoreBar.GetComponent<Graphic>() : null);
        AddRoundResultFadeTween(contentSeq, _roundResultP1ScoreText);
        AddRoundResultFadeTween(contentSeq, _roundResultP2ScoreBar != null ? _roundResultP2ScoreBar.GetComponent<Graphic>() : null);
        AddRoundResultFadeTween(contentSeq, _roundResultP2ScoreText);
        seq.Append(contentSeq);
    }

    void AddRoundResultTypewriterTween(Sequence seq, TMP_Text text)
    {
        if (seq == null || text == null) return;

        PrepareRoundResultTypewriterText(text);
        var totalCharacters = GetVisibleTextCharacterCount(text);
        if (totalCharacters <= 0)
        {
            SetTextVisibleCharacters(text, 0);
            return;
        }

        var duration = totalCharacters / Mathf.Max(0.01f, _roundResultPromptCharactersPerSecond);
        var visibleCharacters = 0;
        seq.Append(DOTween.To(() => visibleCharacters, value =>
            {
                visibleCharacters = value;
                SetTextVisibleCharacters(text, value);
            }, totalCharacters, duration)
            .SetEase(Ease.Linear)
            .SetId(this));
        if (_roundResultContentStagger > 0f)
            seq.AppendInterval(_roundResultContentStagger);
    }

    void PrepareRoundResultTypewriterText(TMP_Text text)
    {
        if (text == null) return;

        SetTextAlpha(text, 1f);
        SetTextVisibleCharacters(text, 0);
    }

    void AddRoundResultFadeTween(Sequence seq, TMP_Text text)
    {
        if (seq == null || text == null) return;

        seq.Append(text.DOFade(1f, _roundResultContentFadeDuration).SetEase(_ease));
        if (_roundResultContentStagger > 0f)
            seq.AppendInterval(_roundResultContentStagger);
    }

    void AddRoundResultFadeTween(Sequence seq, Graphic graphic)
    {
        if (seq == null || graphic == null) return;

        seq.Append(graphic.DOFade(1f, _roundResultContentFadeDuration).SetEase(_ease));
        if (_roundResultContentStagger > 0f)
            seq.AppendInterval(_roundResultContentStagger);
    }

    void AddRoundResultFadeTween(Sequence seq, CanvasGroup group)
    {
        if (seq == null || group == null) return;

        seq.Append(group.DOFade(1f, _roundResultContentFadeDuration).SetEase(_ease));
        if (_roundResultContentStagger > 0f)
            seq.AppendInterval(_roundResultContentStagger);
    }

    void AddRoundResultFadeOutTween(Sequence seq, TMP_Text text)
    {
        if (seq == null || text == null) return;

        seq.Join(text.DOFade(0f, _roundResultFadeGameplayDuration).SetEase(_ease));
    }

    void AddRoundResultFadeOutTween(Sequence seq, Graphic graphic)
    {
        if (seq == null || graphic == null) return;

        seq.Join(graphic.DOFade(0f, _roundResultFadeGameplayDuration).SetEase(_ease));
    }

    void AddRoundResultFadeOutTween(Sequence seq, CanvasGroup group)
    {
        if (seq == null || group == null) return;

        seq.Join(group.DOFade(0f, _roundResultFadeGameplayDuration).SetEase(_ease));
    }

    void AddRoundResultDeathLineFadeTween(Sequence seq)
    {
        if (seq == null || _roundResultDeathLineGroup == null) return;

        var lineSeq = DOTween.Sequence().SetId(this);
        for (var i = 0; i < _roundResultDeathLineGroup.childCount; i++)
        {
            var graphic = _roundResultDeathLineGroup.GetChild(i).GetComponent<Graphic>();
            if (graphic != null)
                lineSeq.Join(graphic.DOFade(0.49f, _roundResultContentFadeDuration).SetEase(_ease));
        }
        seq.Append(lineSeq);
        if (_roundResultContentStagger > 0f)
            seq.AppendInterval(_roundResultContentStagger);
    }

    void AddRoundResultFadeOutDeathLineTween(Sequence seq)
    {
        if (seq == null || _roundResultDeathLineGroup == null) return;

        for (var i = 0; i < _roundResultDeathLineGroup.childCount; i++)
        {
            var graphic = _roundResultDeathLineGroup.GetChild(i).GetComponent<Graphic>();
            if (graphic != null)
                seq.Join(graphic.DOFade(0f, _roundResultFadeGameplayDuration).SetEase(_ease));
        }
    }

    void ConfigureRoundResultElements()
    {
        ConfigureImageRect(_roundResultPanel, GetRoundResultPanelPosition(), GetRoundResultPanelSize(), _promptInkColor);
        RemoveDeprecatedRoundResultStripeCopies();

        ConfigureRoundResultTopLeftText(_roundResultP1WordText, GetRoundResultWordText(_roundResultP1Word, _roundResultP2Word, _gameplayP1LetterColor, _roundResultHostAnswerLetterEligible, _roundResultClientAnswerLetterEligible), GetRoundResultP1WordTopLeftPosition(), new Vector2(520f, 70f), 49f, _roundResultTextColor);
        ConfigureRoundResultTopLeftText(_roundResultP2WordText, GetRoundResultWordText(_roundResultP2Word, _roundResultP1Word, _gameplayP2LetterColor, _roundResultClientAnswerLetterEligible, _roundResultHostAnswerLetterEligible), GetRoundResultP2WordTopLeftPosition(), new Vector2(520f, 70f), 49f, _roundResultTextColor);
        ConfigureRoundResultTopCenterText(_roundResultDeathLabelText, "death.", GetRoundResultDeathLabelTopCenterPosition(), new Vector2(140f, 60f), 36f, _roundResultMutedTextColor);
        ConfigureRoundResultCenterLeftText(_roundResultP1ScoreText, _roundResultP1Score.ToString(), GetRoundResultP1ScoreTextLeftCenterPosition(), new Vector2(120f, 70f), 49f, _roundResultTextColor);
        ConfigureRoundResultCenterLeftText(_roundResultP2ScoreText, _roundResultP2Score.ToString(), GetRoundResultP2ScoreTextLeftCenterPosition(), new Vector2(120f, 70f), 49f, _roundResultTextColor);

        ConfigureRect(_roundResultP1ScoreBar, GetRoundResultP1ScoreBarPosition(), GetRoundResultScoreBarSizeForHp(_roundResultP1Score), new Vector2(0f, 0.5f));
        ConfigureRect(_roundResultP2ScoreBar, GetRoundResultP2ScoreBarPosition(), GetRoundResultScoreBarSizeForHp(_roundResultP2Score), new Vector2(0f, 0.5f));
        ConfigureRect(_roundResultDeathLineGroup, GetRoundResultDeathLinePosition(), new Vector2(5f, 338f), new Vector2(0.5f, 0.5f));
        LayoutRoundResultDeathLineSegments();
        SetRoundResultSiblingOrder();
    }

    void ConfigureGameEndWinnerText(float alpha)
    {
        if (_roundResultDeathLabelText == null) return;

        _roundResultDeathLabelText.text = _gameEndTitle;
        _roundResultDeathLabelText.color = _promptInkColor;
        _roundResultDeathLabelText.fontSize = 170f;
        _roundResultDeathLabelText.alignment = TextAlignmentOptions.Center;
        ConfigureRect(_roundResultDeathLabelText.rectTransform, GetGameEndTitlePosition(), new Vector2(1600f, 220f), new Vector2(0.5f, 0.5f));
        _roundResultDeathLabelText.alpha = alpha;
        _roundResultDeathLabelText.transform.SetAsLastSibling();
    }

    void ConfigureGameEndRestartHint(float alpha)
    {
        var text = GetPressSpaceText();
        if (text == null) return;

        text.text = "press space to restart";
        text.color = _promptInkColor;
        text.fontSize = 52f;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        ConfigureRect(text.rectTransform, GetGameEndRestartHintPosition(), new Vector2(900f, 70f), new Vector2(0.5f, 0.5f));
        if (_pressSpaceGroup != null)
        {
            _pressSpaceGroup.alpha = alpha;
            _pressSpaceGroup.interactable = false;
            _pressSpaceGroup.blocksRaycasts = false;
            _pressSpaceGroup.transform.SetAsLastSibling();
        }
    }

    void ConfigureTutorialPressSpaceHint(float alpha)
    {
        var text = GetPressSpaceText();
        if (text == null) return;

        text.text = "press space to continue";
        text.fontSize = 52.5f;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        ConfigureRect(text.rectTransform, new Vector2(23f, -292.6615f), new Vector2(644.2111f, 62.6421f), new Vector2(0.5f, 0.5f));
        if (_pressSpaceGroup != null)
        {
            _pressSpaceGroup.alpha = alpha;
            _pressSpaceGroup.interactable = false;
            _pressSpaceGroup.blocksRaycasts = false;
        }
    }

    TMP_Text GetPressSpaceText() => _pressSpaceGroup != null ? _pressSpaceGroup.GetComponent<TMP_Text>() : null;

    void ConfigureRoundResultStripes()
    {
        if (_decorativeLines == null || _decorativeLines.Length < 3) return;

        ConfigureRoundResultStripe(_decorativeLines[0]?.rect, GetRoundResultStripePosition(0), GetRoundResultStripeColor(0));
        ConfigureRoundResultStripe(_decorativeLines[1]?.rect, GetRoundResultStripePosition(1), GetRoundResultStripeColor(1));
        ConfigureRoundResultStripe(_decorativeLines[2]?.rect, GetRoundResultStripePosition(2), GetRoundResultStripeColor(2));

        var stripeGroup = GetDecorativeLineStateGroup();
        if (stripeGroup != null)
        {
            stripeGroup.interactable = false;
            stripeGroup.blocksRaycasts = false;
        }
    }

    void ConfigureGameEndStripes()
    {
        if (_decorativeLines == null || _decorativeLines.Length < 3) return;

        ConfigureRoundResultStripe(_decorativeLines[0]?.rect, GetGameEndStripePosition(0), GetRoundResultStripeColor(0));
        ConfigureRoundResultStripe(_decorativeLines[1]?.rect, GetGameEndStripePosition(1), GetRoundResultStripeColor(1));
        ConfigureRoundResultStripe(_decorativeLines[2]?.rect, GetGameEndStripePosition(2), GetRoundResultStripeColor(2));

        var stripeGroup = GetDecorativeLineStateGroup();
        if (stripeGroup != null)
        {
            if (stripeGroup.transform is RectTransform stripeRect)
                stripeRect.SetAsLastSibling();
            stripeGroup.alpha = 1f;
            stripeGroup.interactable = false;
            stripeGroup.blocksRaycasts = false;
        }
    }

    void AddGameEndStripeBrushTween(Sequence seq)
    {
        if (seq == null || _decorativeLines == null || _decorativeLines.Length < 3) return;

        var stripeSeq = DOTween.Sequence().SetId(this);
        AddGameEndStripeMoveTween(stripeSeq, _decorativeLines[0]?.rect, GetGameEndStripePosition(0));
        AddGameEndStripeMoveTween(stripeSeq, _decorativeLines[1]?.rect, GetGameEndStripePosition(1));
        AddGameEndStripeMoveTween(stripeSeq, _decorativeLines[2]?.rect, GetGameEndStripePosition(2));
        seq.Append(stripeSeq);
    }

    void AddGameEndStripeMoveTween(Sequence stripeSeq, RectTransform rect, Vector2 targetPosition)
    {
        if (stripeSeq == null || rect == null) return;

        stripeSeq.Join(rect.DOAnchorPos(targetPosition, _roundResultStripeRevealDuration).SetEase(_ease));
    }

    void ConfigureRoundResultStripe(RectTransform rect, Vector2 position, Color color)
    {
        if (rect == null) return;

        ConfigureRect(rect, position, GetStandardStripeSize(), new Vector2(0.5f, 0.5f));
        rect.gameObject.SetActive(true);
        var graphic = rect.GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.color = color;
            graphic.raycastTarget = false;
        }
    }

    Color GetRoundResultStripeColor(int index)
    {
        if (index == 0) return _roundResultTopStripeColor;
        if (index == 1) return _roundResultMiddleStripeColor;
        return _roundResultBottomStripeColor;
    }

    void RemoveDeprecatedRoundResultStripeCopies()
    {
        RemoveOrHideDeprecatedRoundResultStripeCopy("RoundResultYellowStripe");
        RemoveOrHideDeprecatedRoundResultStripeCopy("RoundResultBlueStripe");
        RemoveOrHideDeprecatedRoundResultStripeCopy("RoundResultRedStripe");
    }

    void RemoveOrHideDeprecatedRoundResultStripeCopy(string childName)
    {
        var rect = FindChildRect(_roundResultElementsGroupRect, childName);
        if (rect == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying && CanEditPrefabAssetStructure())
        {
            UnityEditor.Undo.DestroyObjectImmediate(rect.gameObject);
            return;
        }
#endif
        rect.gameObject.SetActive(false);
    }

    void SetRoundResultSiblingOrder()
    {
        if (_roundResultPanel != null)
            _roundResultPanel.transform.SetAsFirstSibling();
        if (_roundResultDeathLineGroup != null)
            _roundResultDeathLineGroup.SetAsLastSibling();
        if (_roundResultDeathLabelText != null)
            _roundResultDeathLabelText.transform.SetAsLastSibling();
        if (_roundResultP1WordText != null)
            _roundResultP1WordText.transform.SetAsLastSibling();
        if (_roundResultP2WordText != null)
            _roundResultP2WordText.transform.SetAsLastSibling();
        if (_roundResultP1ScoreText != null)
            _roundResultP1ScoreText.transform.SetAsLastSibling();
        if (_roundResultP2ScoreText != null)
            _roundResultP2ScoreText.transform.SetAsLastSibling();
    }

    void ConfigureImageRect(Image image, Vector2 position, Vector2 size, Color color)
    {
        if (image == null) return;

        ConfigureRect(image.rectTransform, position, size, new Vector2(0.5f, 0.5f));
        image.color = color;
        image.raycastTarget = false;
    }

    void ConfigureRoundResultText(TMP_Text text, string value, Vector2 position, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        if (text == null) return;

        text.richText = true;
        text.overrideColorTags = false;
        text.text = MainUiDisplayText(value);
        text.color = color;
        text.fontSize = fontSize;
        text.alignment = alignment;
        ApplySingleLineOverflow(text);
        text.alpha = 1f;
        text.raycastTarget = false;
        ConfigureRect(text.rectTransform, position, text.rectTransform.sizeDelta, new Vector2(0.5f, 0.5f));
    }

    void ConfigureRoundResultTopLeftText(TMP_Text text, string value, Vector2 topLeftPosition, Vector2 size, float fontSize, Color color)
    {
        if (text == null) return;

        text.richText = true;
        text.overrideColorTags = false;
        text.text = MainUiDisplayText(value);
        text.color = color;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.TopLeft;
        ApplySingleLineOverflow(text);
        text.alpha = 1f;
        text.raycastTarget = false;
        ConfigureTopLeftRect(text.rectTransform, topLeftPosition, size);
    }

    void ConfigureRoundResultTopCenterText(TMP_Text text, string value, Vector2 topCenterPosition, Vector2 size, float fontSize, Color color)
    {
        if (text == null) return;

        text.richText = true;
        text.overrideColorTags = false;
        text.text = MainUiDisplayText(value);
        text.color = color;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Top;
        ApplySingleLineOverflow(text);
        text.alpha = 1f;
        text.raycastTarget = false;
        ConfigureTopCenterRect(text.rectTransform, topCenterPosition, size);
    }

    void ConfigureRoundResultCenterLeftText(TMP_Text text, string value, Vector2 leftCenterPosition, Vector2 size, float fontSize, Color color)
    {
        if (text == null) return;

        text.richText = true;
        text.overrideColorTags = false;
        text.text = MainUiDisplayText(value);
        text.color = color;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Left;
        ApplySingleLineOverflow(text);
        text.alpha = 1f;
        text.raycastTarget = false;
        ConfigureCenterLeftRect(text.rectTransform, leftCenterPosition, size);
    }

    void EnsureRoundResultDeathLineSegments()
    {
        if (_roundResultDeathLineGroup == null) return;

        const int segmentCount = 17;
        for (var i = 0; i < segmentCount; i++)
            GetOrCreateImage($"DeathDash{i + 1}", _roundResultDeathLineGroup, new Color(_roundResultTextColor.r, _roundResultTextColor.g, _roundResultTextColor.b, 0.49f));
    }

    void LayoutRoundResultDeathLineSegments()
    {
        if (_roundResultDeathLineGroup == null) return;

        for (var i = 0; i < _roundResultDeathLineGroup.childCount; i++)
        {
            if (!(_roundResultDeathLineGroup.GetChild(i) is RectTransform dash)) continue;
            dash.gameObject.SetActive(i < 17);
            ConfigureRect(dash, new Vector2(0f, 159f - i * 20f), new Vector2(5f, 10f), new Vector2(0.5f, 0.5f));
            var image = dash.GetComponent<Image>();
            if (image != null)
                image.color = new Color(_roundResultTextColor.r, _roundResultTextColor.g, _roundResultTextColor.b, 0.49f);
        }
    }

    string GetRoundResultWordText(string word, string opposingWord, Color advantageColor, bool ownAnswerLetterEligible, bool opposingAnswerLetterEligible)
    {
        if (string.IsNullOrEmpty(word)) return string.Empty;

        // Invalid / uncounted submission: show the typed string as plain default-colored text (no letter advantage / banned highlights).
        if (!ownAnswerLetterEligible)
            return word;

        var colorHex = ColorUtility.ToHtmlStringRGB(advantageColor);
        var ownLetterCount = CountLetters(word);
        var opposingLetterCount = opposingAnswerLetterEligible ? CountLetters(opposingWord) : 0;
        var letterIndex = 0;
        var result = new StringBuilder(word.Length);

        foreach (var c in word)
        {
            if (!char.IsLetter(c))
            {
                result.Append(c);
                continue;
            }

            var shouldColor = ownLetterCount > opposingLetterCount && letterIndex >= opposingLetterCount;
            if (shouldColor)
                result.Append("<color=#").Append(colorHex).Append(">").Append(c).Append("</color>");
            else if (IsBannedPromptLetter(c))
                result.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(_promptBannedLetterColor)).Append(">").Append(c).Append("</color>");
            else
                result.Append(c);

            letterIndex++;
        }

        return result.ToString();
    }

    void PrepareGameplayText(TMP_Text text, string value, Color color, Vector2 targetPosition, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        if (text == null) return;

        text.richText = true;
        text.overrideColorTags = false;
        text.alignment = alignment;
        ApplySingleLineOverflow(text);
        text.color = color;
        text.text = MainUiDisplayText(value);
        text.alpha = 0f;
        text.rectTransform.anchoredPosition = targetPosition - new Vector2(0f, _gameplaySlideOffset);
    }

    Vector2 GetGameplayPromptPosition() => new Vector2(-910f, 338f);
    Vector2 GetGameplayPromptSize() => new Vector2(1720f, 210f);
    float GetGameplayPromptFontSize() => 165f;
    Vector2 GetGameplayBannedLabelPosition() => new Vector2(-896f, 201f);
    Vector2 GetGameplayBannedLabelSize() => new Vector2(1000f, 76f);
    float GetGameplayBannedLabelFontSize() => 52f;
    Vector2 GetGameplayInputFieldPosition() => new Vector2(0f, -420f);
    Vector2 GetGameplayInputFieldSize() => new Vector2(1800f, 120f);
    Vector2 GetWaitingPanelStartPosition() => _waitingPanelStartAnchoredPos + new Vector2(0f, _waitingPanelYOffset);
    void ApplyStartDecorativeLineLayoutImmediate()
    {
        if (_decorativeLines == null || _inputFieldRect == null)
            return;

        var inputWidth = Mathf.Max(0f, _initInputFieldSize.x);
        if (inputWidth <= 0f)
            inputWidth = Mathf.Max(0f, _inputFieldRect.sizeDelta.x);
        if (inputWidth <= 0f)
            return;

        var inputLeftEdge = _initInputFieldAnchoredPos.x - inputWidth * _inputFieldRect.pivot.x;
        foreach (var line in _decorativeLines)
        {
            if (line?.rect == null) continue;

            var lineHeight = Mathf.Max(1f, line.initialSizeDelta.y);
            var lineSize = new Vector2(inputWidth, lineHeight);
            line.rect.sizeDelta = lineSize;
            line.rect.anchoredPosition = new Vector2(
                inputLeftEdge + lineSize.x * line.rect.pivot.x,
                line.initialAnchoredPos.y);
        }
    }

    float GetWaitingPanelRevealAmount()
    {
        var inputHeight = _inputFieldRect != null
            ? Mathf.Max(0f, _inputFieldRect.sizeDelta.y)
            : Mathf.Max(0f, _waitingPanelStartSize.y);
        return Mathf.Max(0f, inputHeight + StandardUiGap - _waitingPanelYOffset);
    }

    void ApplyWaitingDecorativeLineLayoutImmediate()
    {
        if (_decorativeLines == null) return;

        for (var i = 0; i < _decorativeLines.Length; i++)
        {
            var line = _decorativeLines[i];
            if (line?.rect == null) continue;

            line.rect.anchoredPosition = GetWaitingLineTargetPosition(line, i);
            line.rect.sizeDelta = line.waitingSizeDelta;
        }
    }

    Vector2 GetWaitingLineTargetPosition(LineTarget line, int index)
    {
        if (line == null) return Vector2.zero;

        var lineHeight = Mathf.Max(1f, line.waitingSizeDelta.y);
        var gap = StandardUiGap;
        var bottomLineIndex = _decorativeLines != null ? Mathf.Max(0, _decorativeLines.Length - 1) : 0;
        var stepsAboveBottom = Mathf.Max(0, bottomLineIndex - index);
        var panelPivotY = _waitingPanel != null ? _waitingPanel.pivot.y : 0f;
        var panelTopY = GetWaitingPanelStartPosition().y + _waitingPanelTargetSize.y * (1f - panelPivotY);
        var y = panelTopY + gap + lineHeight * 0.5f + stepsAboveBottom * (lineHeight + gap);
        return new Vector2(line.waitingAnchoredPos.x, y);
    }

    float StandardUiGap => Mathf.Max(0f, _standardUiGap);
    Vector2 GetGameplayP1LetterGroupPosition() => new Vector2(-760f, GetGameplayP1IconPosition().y);
    Vector2 GetGameplayP2LetterGroupPosition() => new Vector2(-760f, GetGameplayP2IconPosition().y);
    Vector2 GetRoundResultPromptTopLeftPosition() => new Vector2(105f, -185f);
    Vector2 GetRoundResultBannedLabelTopLeftPosition() => new Vector2(125f, -365f);
    Vector2 GetRoundResultP1IconPosition() => new Vector2(-780f, -100f);
    Vector2 GetRoundResultP2IconPosition() => new Vector2(-780f, -295f);
    Vector2 GetRoundResultP1WordPosition() => new Vector2(-414f, -110f);
    Vector2 GetRoundResultP2WordPosition() => new Vector2(-414f, -310f);
    Vector2 GetRoundResultP1WordTopLeftPosition() => new Vector2(270f, -610f);
    Vector2 GetRoundResultP2WordTopLeftPosition() => new Vector2(270f, -805f);
    Vector2 GetRoundResultDeathLabelTopCenterPosition() => new Vector2(-325f, -490f);
    Vector2 GetRoundResultDeathLinePosition() => new Vector2(-325f, -169f);
    Vector2 GetRoundResultP1ScoreBarPosition() => new Vector2(-325f, -100f);
    Vector2 GetRoundResultP2ScoreBarPosition() => new Vector2(-325f, -295f);

    Vector2 GetRoundResultScoreBarSizeForHp(int currentHp)
    {
        var maxHp = GameManager.Instance != null ? GameManager.Instance.MaxPlayerHp : 20;
        maxHp = Mathf.Max(1, maxHp);
        var t = Mathf.Clamp01(currentHp / (float)maxHp);
        var w = _roundResultScoreBarFullWidth * t;
        return new Vector2(Mathf.Max(0f, w), _roundResultScoreBarHeight);
    }
    Vector2 GetRoundResultP1ScoreTextLeftCenterPosition()
    {
        var barPos = GetRoundResultP1ScoreBarPosition();
        var barW = GetRoundResultScoreBarSizeForHp(_roundResultP1Score).x;
        return new Vector2(barPos.x + barW + _roundResultScoreTextOffsetFromBarEnd.x, barPos.y + _roundResultScoreTextOffsetFromBarEnd.y);
    }

    Vector2 GetRoundResultP2ScoreTextLeftCenterPosition()
    {
        var barPos = GetRoundResultP2ScoreBarPosition();
        var barW = GetRoundResultScoreBarSizeForHp(_roundResultP2Score).x;
        return new Vector2(barPos.x + barW + _roundResultScoreTextOffsetFromBarEnd.x, barPos.y + _roundResultScoreTextOffsetFromBarEnd.y);
    }
    Vector2 GetRoundResultPanelPosition() => new Vector2(0f, -52f);
    Vector2 GetRoundResultPanelSize() => new Vector2(1800f, 856f);
    Vector2 GetGameEndTitlePosition() => new Vector2(0f, 80f);
    Vector2 GetGameEndRestartHintPosition() => new Vector2(0f, -210f);
    Vector2 GetGameEndBlackBarPosition() => GetGameEndBlackBarCenterInPanelSpace();
    Vector2 GetGameEndBlackBarSize() => new Vector2(GetGameEndStripeWidthInPanelSpace(), GetStandardStripeSize().y);
    float GetGameEndStripeBottomCenterY() => GetGameEndBlackBarCenterYInStripeSpace() + GetStandardStripeSize().y + StandardUiGap;
    Vector2 GetGameEndBlackBarCenterInPanelSpace()
    {
        var stripe = GetGameEndReferenceStripe();
        var panelParent = GetRoundResultPanelParent();
        if (stripe == null || stripe.parent == null || panelParent == null)
            return GetGameEndBlackBarPositionFallback();

        var blackBarCenter = GetGameEndBlackBarCenterInStripeSpace();
        var worldCenter = stripe.parent.TransformPoint(blackBarCenter);
        return panelParent.InverseTransformPoint(worldCenter);
    }

    Vector2 GetGameEndBlackBarCenterInStripeSpace() => new Vector2(0f, GetGameEndBlackBarCenterYInStripeSpace());

    float GetGameEndBlackBarCenterYInStripeSpace()
    {
        var stripeHeight = GetStandardStripeSize().y;
        var stripeParent = GetGameEndReferenceStripe()?.parent as RectTransform;
        var bottomY = -GetDesignCanvasSize().y * 0.5f;
        return bottomY + GetGameEndPageMargin(stripeParent) + stripeHeight * 0.5f;
    }

    float GetGameEndPageMargin(RectTransform stripeParent)
    {
        var parentWidth = GetDesignCanvasSize().x;
        return Mathf.Max(0f, (parentWidth - GetStandardStripeSize().x) * 0.5f);
    }

    Vector2 GetDesignCanvasSize()
    {
        var width = _lockedResolution.x > 0 ? _lockedResolution.x : 1920;
        var height = _lockedResolution.y > 0 ? _lockedResolution.y : 1080;
        return new Vector2(width, height);
    }

    float GetGameEndStripeWidthInPanelSpace()
    {
        var stripe = GetGameEndReferenceStripe();
        var panelParent = GetRoundResultPanelParent();
        if (stripe == null || stripe.parent == null || panelParent == null)
            return GetStandardStripeSize().x;

        var stripeSize = GetStandardStripeSize();
        var stripeCenter = GetGameEndStripePosition(0);
        var worldLeft = stripe.parent.TransformPoint(stripeCenter + new Vector2(-stripeSize.x * 0.5f, 0f));
        var worldRight = stripe.parent.TransformPoint(stripeCenter + new Vector2(stripeSize.x * 0.5f, 0f));
        var localLeft = panelParent.InverseTransformPoint(worldLeft);
        var localRight = panelParent.InverseTransformPoint(worldRight);
        return Mathf.Abs(localRight.x - localLeft.x);
    }

    RectTransform GetGameEndReferenceStripe()
    {
        if (_decorativeLines == null) return null;
        foreach (var line in _decorativeLines)
        {
            if (line?.rect != null)
                return line.rect;
        }
        return null;
    }

    Transform GetRoundResultPanelParent()
    {
        if (_roundResultPanel != null && _roundResultPanel.rectTransform != null)
            return _roundResultPanel.rectTransform.parent;
        return _roundResultElementsGroupRect != null ? _roundResultElementsGroupRect : transform;
    }

    Vector2 GetGameEndStripePosition(int index)
    {
        var clampedIndex = Mathf.Clamp(index, 0, 2);
        var stripeHeight = GetStandardStripeSize().y;
        var bottomStripeCenterY = GetGameEndStripeBottomCenterY();
        var centerStep = stripeHeight + StandardUiGap;
        return new Vector2(0f, bottomStripeCenterY + clampedIndex * centerStep);
    }

    Vector2 GetGameEndBlackBarPositionFallback()
    {
        return GetGameEndBlackBarCenterInStripeSpace();
    }

    Vector2 GetRoundResultStripePosition(int index)
    {
        var clampedIndex = Mathf.Clamp(index, 0, 2);
        var panelTopY = GetRoundResultPanelPosition().y + GetRoundResultPanelSize().y * 0.5f;
        var stripeHeight = GetStandardStripeSize().y;
        var bottomStripeCenterY = panelTopY + StandardUiGap + stripeHeight * 0.5f;
        var centerStep = stripeHeight + StandardUiGap;
        return new Vector2(0f, bottomStripeCenterY + clampedIndex * centerStep);
    }

    Vector2 GetRoundResultStripeStartPosition()
    {
        var panelTopY = GetRoundResultPanelPosition().y + GetRoundResultPanelSize().y * 0.5f;
        return new Vector2(0f, panelTopY + GetStandardStripeSize().y * 0.5f);
    }

    Vector2 GetStandardStripeSize() => new Vector2(1800f, 20f);

    void SetSharedPromptVisibleForGameplay(bool preserveGameplayInputSubmitLock = false)
    {
        SetOptionalGameObjectActive(_promptSharedBackground, false);
        if (_promptPromptMask != null) _promptPromptMask.gameObject.SetActive(false);
        if (_promptBannedMask != null) _promptBannedMask.gameObject.SetActive(false);

        if (_promptTitleText != null)
        {
            _promptTitleText.alignment = TextAlignmentOptions.Left;
            _promptTitleText.color = _promptInkColor;
            _promptTitleText.text = MainUiDisplayText(GetPromptTextWithBannedLetters(_promptText));
            _promptTitleText.fontSize = GetGameplayPromptFontSize();
            SetCenterLeftAnchors(_promptTitleText.rectTransform);
            _promptTitleText.rectTransform.sizeDelta = GetGameplayPromptSize();
            _promptTitleText.alpha = 1f;
            _promptTitleText.rectTransform.anchoredPosition = GetGameplayPromptPosition();
        }
        if (_promptBannedText != null)
        {
            _promptBannedText.richText = true;
            _promptBannedText.overrideColorTags = false;
            _promptBannedText.alignment = TextAlignmentOptions.Left;
            _promptBannedText.color = _promptInkColor;
            _promptBannedText.text = MainUiDisplayText(GetBannedLetterRevealText());
            _promptBannedText.fontSize = GetGameplayBannedLabelFontSize();
            SetCenterLeftAnchors(_promptBannedText.rectTransform);
            _promptBannedText.rectTransform.sizeDelta = GetGameplayBannedLabelSize();
            _promptBannedText.alpha = HasPromptBannedLetters() ? 1f : 0f;
            _promptBannedText.rectTransform.anchoredPosition = GetGameplayBannedLabelPosition();
        }

        RefreshPromptCalibrationOverlay();
        SetGameplayInputFieldVisible(preserveGameplayInputSubmitLock);
        HideDeprecatedGameplayPlayerLabelCopies();
    }

    void SetSharedPromptVisibleForRoundResult()
    {
        SetOptionalGameObjectActive(_promptSharedBackground, false);
        if (_promptPromptMask != null) _promptPromptMask.gameObject.SetActive(false);
        if (_promptBannedMask != null) _promptBannedMask.gameObject.SetActive(false);

        if (_promptSharedGroup != null)
        {
            _promptSharedGroup.alpha = 1f;
            _promptSharedGroup.interactable = false;
            _promptSharedGroup.blocksRaycasts = false;
        }

        if (_promptTitleText != null)
        {
            _promptTitleText.richText = true;
            _promptTitleText.overrideColorTags = false;
            _promptTitleText.alignment = TextAlignmentOptions.TopLeft;
            _promptTitleText.color = _roundResultTextColor;
            _promptTitleText.text = MainUiDisplayText(GetPromptTextWithBannedLetters(_promptText));
            _promptTitleText.fontSize = 170f;
            _promptTitleText.alpha = 1f;
            SetTextFullyVisible(_promptTitleText);
            ConfigureTopLeftRect(_promptTitleText.rectTransform, GetRoundResultPromptTopLeftPosition(), new Vector2(1400f, 220f));
        }

        if (_promptBannedText != null)
        {
            _promptBannedText.richText = true;
            _promptBannedText.overrideColorTags = false;
            _promptBannedText.alignment = TextAlignmentOptions.TopLeft;
            _promptBannedText.color = _roundResultTextColor;
            _promptBannedText.text = MainUiDisplayText(GetBannedLetterRevealText());
            _promptBannedText.fontSize = 59f;
            _promptBannedText.alpha = 1f;
            SetTextFullyVisible(_promptBannedText);
            ConfigureTopLeftRect(_promptBannedText.rectTransform, GetRoundResultBannedLabelTopLeftPosition(), new Vector2(1000f, 90f));
        }
    }

    void PrepareGameplayInputFieldStart()
    {
        if (_inputFieldRect != null)
        {
            ConfigureRect(_inputFieldRect, GetGameplayInputFieldPosition() - new Vector2(0f, _gameplaySlideOffset), GetGameplayInputFieldSize(), new Vector2(0.5f, 0.5f));
            SetGameplayInputFieldSiblingOrder();
        }

        ConfigureGameplayInputFieldContent(0f);
    }

    void SetGameplayInputFieldVisible(bool preserveSubmitLock = false)
    {
        if (_inputFieldRect != null)
        {
            ConfigureRect(_inputFieldRect, GetGameplayInputFieldPosition(), GetGameplayInputFieldSize(), new Vector2(0.5f, 0.5f));
            SetGameplayInputFieldSiblingOrder();
        }

        ConfigureGameplayInputFieldContent(1f, preserveSubmitLock);
    }

    void SetGameplayInputFieldSiblingOrder()
    {
        if (_inputFieldRect != null)
            _inputFieldRect.SetAsLastSibling();
    }

    void AddGameplayInputFieldTween(Sequence seq)
    {
        if (seq == null || _inputFieldRect == null) return;

        var inputGroup = GetInputFieldStateGroup();
        if (inputGroup != null)
        {
            inputGroup.alpha = 0f;
            inputGroup.interactable = false;
            inputGroup.blocksRaycasts = false;
            seq.Join(inputGroup.DOFade(1f, _gameplayFadeDuration).SetEase(_ease));
        }
        seq.Join(_inputFieldRect.DOAnchorPos(GetGameplayInputFieldPosition(), _gameplayFadeDuration).SetEase(_ease));
        seq.Join(_inputFieldRect.DOSizeDelta(GetGameplayInputFieldSize(), _gameplayFadeDuration).SetEase(_ease));
        if (_inputFieldContentGroup != null)
            seq.Join(_inputFieldContentGroup.DOFade(1f, _gameplayFadeDuration).SetEase(_ease));
    }

    void ConfigureGameplayInputFieldContent(float alpha, bool preserveSubmitLock = false)
    {
        if (_inputFieldContentGroup != null)
            _inputFieldContentGroup.DOKill(false);
        var shellPre = GetInputFieldStateGroup();
        if (shellPre != null)
            shellPre.DOKill(false);

        if (_inputField != null)
        {
            _inputField.enabled = true;
            _inputField.transition = Selectable.Transition.None;
            if (preserveSubmitLock)
            {
                _inputField.interactable = false;
                _inputField.readOnly = true;
                _inputField.DeactivateInputField();
            }
            else
            {
                _inputField.interactable = true;
                _inputField.readOnly = false;
                _inputField.SetTextWithoutNotify(string.Empty);
            }

            if (_inputField.targetGraphic != null)
                _inputField.targetGraphic.color = _promptInkColor;
        }
        if (_inputFieldPlaceholderText != null && !preserveSubmitLock)
        {
            _inputFieldPlaceholderText.text = MainUiDisplayText(_gameplayInputPlaceholder);
            _inputFieldPlaceholderText.color = _inputFieldPlaceholderColor;
        }
        if (_inputFieldContentGroup != null)
        {
            _inputFieldContentGroup.alpha = alpha;
            _inputFieldContentGroup.interactable = false;
            _inputFieldContentGroup.blocksRaycasts = false;
        }

        var inputGroup = GetInputFieldStateGroup();
        if (inputGroup != null)
        {
            inputGroup.alpha = alpha;
            inputGroup.interactable = alpha > 0f;
            inputGroup.blocksRaycasts = alpha > 0f;
        }

        if (_debugGameplaySharedInputVisibility && alpha >= 0.99f)
            DebugLogSharedInputField($"ConfigureGameplayInputFieldContent(alpha={alpha})", null, includeAncestors: true);

        _gameplayP1Word = _inputField != null ? _inputField.text : "";
        RefreshGameplayLetterBlocks();
    }

    void FocusGameplayInputField()
    {
        if (_inputField == null) return;

        StartCoroutine(FocusGameplayInputFieldNextFrame());
    }

    IEnumerator FocusGameplayInputFieldNextFrame()
    {
        yield return null;

        if (_inputField == null) yield break;

        if (TryGetLocalOwnerClient(out var owner) && owner.AnswerCheckedValid.Value)
            yield break;

        _inputField.enabled = true;
        _inputField.interactable = true;
        _inputField.readOnly = false;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_inputField.gameObject);

        _inputField.Select();
        _inputField.ActivateInputField();
    }

    static bool TryGetLocalOwnerClient(out Client client)
    {
        foreach (var c in FindObjectsByType<Client>(FindObjectsSortMode.InstanceID))
        {
            if (c.IsOwner)
            {
                client = c;
                return true;
            }
        }

        client = null;
        return false;
    }

    void PrepareGameplayPlayerIconsStart()
    {
        ConfigureGameplayPlayerIcon(_waitingP1Group, GetGameplayP1IconPosition() - new Vector2(0f, _gameplaySlideOffset), true);
        ConfigureGameplayPlayerIcon(_waitingP2Group, GetGameplayP2IconPosition() - new Vector2(0f, _gameplaySlideOffset), false);
        if (_waitingP1Group != null) _waitingP1Group.alpha = 0f;
        if (_waitingP2Group != null) _waitingP2Group.alpha = 0f;
    }

    void SetGameplayPlayerIconsVisible()
    {
        ConfigureGameplayPlayerIcon(_waitingP1Group, GetGameplayP1IconPosition(), true);
        ConfigureGameplayPlayerIcon(_waitingP2Group, GetGameplayP2IconPosition(), false);
    }

    void SetRoundResultPlayerIconsVisible()
    {
        ConfigureRoundResultPlayerIcon(_waitingP1Group, GetRoundResultP1IconPosition(), true);
        ConfigureRoundResultPlayerIcon(_waitingP2Group, GetRoundResultP2IconPosition(), false);
    }

    void ConfigureGameplayPlayerIcon(CanvasGroup group, Vector2 anchoredPosition, bool isP1Slot)
    {
        if (group == null || !(group.transform is RectTransform rect)) return;

        group.alpha = 1f;
        group.interactable = false;
        group.blocksRaycasts = false;
        ConfigureRect(rect, anchoredPosition, new Vector2(120f, 120f), new Vector2(0.5f, 0.5f));
        var showLocal = IsLocalYouIndicatorForSlot(isP1Slot);
        var icon = group.GetComponent<PlayerIcon>();
        if (icon != null)
            icon.IsLocal = showLocal;
        ConfigurePlayerIconBoxForGameplay(rect);
        ConfigurePlayerIconIndicatorForGameplay(rect, icon != null ? icon.IsLocal : showLocal);
        rect.SetAsLastSibling();
    }

    void ConfigureRoundResultPlayerIcon(CanvasGroup group, Vector2 anchoredPosition, bool isP1Slot)
    {
        if (group == null || !(group.transform is RectTransform rect)) return;

        group.alpha = 1f;
        group.interactable = false;
        group.blocksRaycasts = false;
        ConfigureRect(rect, anchoredPosition, new Vector2(100f, 100f), new Vector2(0.5f, 0.5f));
        var showLocal = IsLocalYouIndicatorForSlot(isP1Slot);
        var icon = group.GetComponent<PlayerIcon>();
        if (icon != null)
            icon.IsLocal = showLocal;
        ConfigurePlayerIconBoxForGameplay(rect);
        ConfigurePlayerIconIndicatorForRoundResult(rect, icon != null ? icon.IsLocal : showLocal);
        rect.SetAsLastSibling();
    }

    /// <summary>P1 row = host (clientId 0); P2 row = second client. Used with <see cref="PlayerIcon.IsLocal"/>.</summary>
    static bool IsLocalYouIndicatorForSlot(bool isP1Slot)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
            return false;
        var localId = NetworkManager.Singleton.LocalClientId;
        return isP1Slot ? localId == 0 : localId == 1;
    }

    void SetGameplayPlayerIconSiblingOrder()
    {
        if (_waitingP1Group != null)
            _waitingP1Group.transform.SetAsLastSibling();
        if (_waitingP2Group != null)
            _waitingP2Group.transform.SetAsLastSibling();
    }

    void SetRoundResultPlayerIconSiblingOrder()
    {
        SetGameplayPlayerIconSiblingOrder();
    }

    void ConfigurePlayerIconIndicatorForGameplay(RectTransform playerIconRoot, bool showLocalIndicator)
    {
        if (playerIconRoot == null) return;

        var indicator = FindChildRect(playerIconRoot, "YouIndicator");
        var triangle = FindChildRect(indicator != null ? indicator : playerIconRoot, "YouTriangle");
        var youText = FindChildRect(indicator != null ? indicator : playerIconRoot, "YouText");

        if (indicator != null)
        {
            indicator.gameObject.SetActive(showLocalIndicator);
            ConfigureRect(indicator, new Vector2(-82f, 0f), GetEquilateralTriangleRectSize(TriangleGraphic.Direction.Right, 24f), new Vector2(0.5f, 0.5f));
        }
        if (triangle != null)
        {
            triangle.gameObject.SetActive(showLocalIndicator);
            ConfigureRect(triangle, Vector2.zero, GetEquilateralTriangleRectSize(TriangleGraphic.Direction.Right, 24f), new Vector2(0.5f, 0.5f));
            var triangleGraphic = triangle.GetComponent<TriangleGraphic>();
            if (triangleGraphic != null)
            {
                triangleGraphic.PointingDirection = TriangleGraphic.Direction.Right;
                triangleGraphic.color = _promptInkColor;
                triangleGraphic.raycastTarget = false;
            }
        }
        if (youText != null)
            youText.gameObject.SetActive(false);
    }

    void ConfigurePlayerIconIndicatorForRoundResult(RectTransform playerIconRoot, bool showLocalIndicator)
    {
        if (playerIconRoot == null) return;

        var indicator = FindChildRect(playerIconRoot, "YouIndicator");
        var triangle = FindChildRect(indicator != null ? indicator : playerIconRoot, "YouTriangle");
        var youText = FindChildRect(indicator != null ? indicator : playerIconRoot, "YouText");

        if (indicator != null)
        {
            indicator.gameObject.SetActive(showLocalIndicator);
            ConfigureRect(indicator, new Vector2(-70f, 0f), GetEquilateralTriangleRectSize(TriangleGraphic.Direction.Right, 24f), new Vector2(0.5f, 0.5f));
        }
        if (triangle != null)
        {
            triangle.gameObject.SetActive(showLocalIndicator);
            ConfigureRect(triangle, Vector2.zero, GetEquilateralTriangleRectSize(TriangleGraphic.Direction.Right, 24f), new Vector2(0.5f, 0.5f));
            var triangleGraphic = triangle.GetComponent<TriangleGraphic>();
            if (triangleGraphic != null)
            {
                triangleGraphic.PointingDirection = TriangleGraphic.Direction.Right;
                triangleGraphic.color = _roundResultTextColor;
                triangleGraphic.raycastTarget = false;
            }
        }
        if (youText != null)
            youText.gameObject.SetActive(false);
    }

    void ConfigurePlayerIconBoxForGameplay(RectTransform playerIconRoot)
    {
        if (playerIconRoot == null) return;

        var box = playerIconRoot.GetComponentInChildren<BoxFrameGraphic>(true);
        if (box != null)
        {
            box.Thickness = 10f;
            box.InsetFromEdge = true;
            box.FillColor = _promptInkColor;

            if (box.transform is RectTransform boxRect)
                StretchToParent(boxRect);
        }

        var idText = box != null ? box.GetComponentInChildren<TMP_Text>(true) : null;
        if (idText != null)
        {
            idText.enableAutoSizing = false;
            idText.fontSize = Mathf.Max(51f, playerIconRoot.sizeDelta.y * 0.51f);
            idText.characterSpacing = 0f;
            idText.alignment = TextAlignmentOptions.Center;
            idText.margin = Vector4.zero;
            idText.text = playerIconRoot.name.Contains("2") ? "P2" : "P1";
            if (idText.rectTransform != null)
                StretchToParent(idText.rectTransform);
        }
    }

    static bool ShouldShowWaitingP2PlayerIcon()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsClient)
            return true;
        return nm.ConnectedClients.Count >= 2;
    }

    void TryRegisterWaitingLobbyCallback()
    {
        if (_waitingLobbyCallbackRegistered) return;
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedWaitingLobbyRevealP2;
        _waitingLobbyCallbackRegistered = true;
    }

    void TryUnregisterWaitingLobbyCallback()
    {
        if (!_waitingLobbyCallbackRegistered) return;
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedWaitingLobbyRevealP2;
        _waitingLobbyCallbackRegistered = false;
    }

    void OnClientConnectedWaitingLobbyRevealP2(ulong clientId)
    {
        if (_currentState != MainUIState.Waiting) return;
        if (NetworkManager.Singleton == null) return;
        // Refresh PlayerIcon.IsLocal on every peer when someone connects (second player joins).
        ConfigureWaitingPlayerIconsLayout();
        // Must run on every client: non-host may have entered Waiting while alone (P2 icon skipped in
        // WaitingRevealRoutine); only host used to call Reveal here, so P2's machine never faded P2 in.
        RevealWaitingP2PlayerIconFromLobbyIfNeeded();
    }

    void RevealWaitingP2PlayerIconFromLobbyIfNeeded()
    {
        if (_waitingP2LobbyRevealCompleted) return;
        if (_currentState != MainUIState.Waiting) return;
        if (_waitingP2Group == null) return;
        if (!ShouldShowWaitingP2PlayerIcon()) return;

        _waitingP2LobbyRevealCompleted = true;
        _waitingP2Group.DOKill();
        _waitingP2Group.alpha = 0f;
        _waitingP2Group.DOFade(1f, _waitingContentFadeDuration).SetEase(_ease).SetId(this);
    }

    void RegisterWaitingCommandInputListener()
    {
        if (_waitingCommandListenerRegistered) return;
        if (UI.UIManager.Instance == null) return;
        if (UI.UIManager.Instance.AnswerInputField == null) return;
        UI.UIManager.Instance.AddSubmitListenerToAnswerInputField(OnWaitingCommandSubmit);
        _waitingCommandListenerRegistered = true;
    }

    void UnregisterWaitingCommandInputListener()
    {
        if (!_waitingCommandListenerRegistered) return;
        if (UI.UIManager.Instance != null)
            UI.UIManager.Instance.RemoveSubmitListenerFromAnswerInputField(OnWaitingCommandSubmit);
        _waitingCommandListenerRegistered = false;
    }

    void UpdateWaitingCommandListenerForState(MainUIState state)
    {
        if (state == MainUIState.Waiting) RegisterWaitingCommandInputListener();
        else UnregisterWaitingCommandInputListener();
    }

    void OnWaitingCommandSubmit(string content)
    {
        if (_debugSharedCommandInput)
            Debug.Log($"[MainUIController] OnWaitingCommandSubmit state={_currentState} raw='{content ?? "<null>"}'");

        if (_currentState != MainUIState.Waiting) return;
        if (string.IsNullOrWhiteSpace(content))
        {
            if (_debugSharedCommandInput) Debug.Log("[MainUIController] ignore: whitespace");
            return;
        }

        var key = content.Trim().ToLowerInvariant();
        if (key != "ready")
        {
            if (_debugSharedCommandInput) Debug.Log($"[MainUIController] ignore: key='{key}'");
            return;
        }

        if (_debugSharedCommandInput) Debug.Log("[MainUIController] READY matched -> clear/focus + send ready rpc");
        UI.UIManager.Instance?.ClearAnswerInputField();
        UI.UIManager.Instance?.FocusAnswerInputField();

        var gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogWarning("MainUIController: GameManager.Instance missing; cannot mark ready.");
            return;
        }

        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogWarning("MainUIController: NetworkManager.Singleton missing; cannot mark ready.");
            return;
        }

        gm.SetClientReadyServerRpc(nm.LocalClientId, true);
    }

    void ConfigureWaitingPlayerIconsLayout()
    {
        ConfigureWaitingPlayerIcon(_waitingP1Group, new Vector2(-687.54f, -122.601395f), isP1Slot: true);
        ConfigureWaitingPlayerIcon(_waitingP2Group, new Vector2(-399.5f, -122.601395f), isP1Slot: false);
    }

    void ConfigureWaitingPlayerIcon(CanvasGroup group, Vector2 anchoredPosition, bool isP1Slot)
    {
        if (group == null || !(group.transform is RectTransform rect)) return;

        ConfigureRect(rect, anchoredPosition, new Vector2(220.1179f, 214.7207f), new Vector2(0.5f, 0.5f));
        var showLocal = IsLocalYouIndicatorForSlot(isP1Slot);
        var icon = group.GetComponent<PlayerIcon>();
        if (icon != null)
            icon.IsLocal = showLocal;
        ConfigurePlayerIconBoxForWaiting(rect);
        ConfigurePlayerIconIndicatorForWaiting(rect, icon != null ? icon.IsLocal : showLocal);
    }

    void ConfigurePlayerIconBoxForWaiting(RectTransform playerIconRoot)
    {
        if (playerIconRoot == null) return;

        var box = playerIconRoot.GetComponentInChildren<BoxFrameGraphic>(true);
        if (box != null)
        {
            box.Thickness = 4f;
            box.InsetFromEdge = true;
            box.FillColor = Color.clear;
        }
    }

    void ConfigurePlayerIconIndicatorForWaiting(RectTransform playerIconRoot, bool showLocalIndicator)
    {
        if (playerIconRoot == null) return;

        var indicator = FindChildRect(playerIconRoot, "YouIndicator");
        var triangle = FindChildRect(indicator != null ? indicator : playerIconRoot, "YouTriangle");
        var youText = FindChildRect(indicator != null ? indicator : playerIconRoot, "YouText");

        if (indicator != null)
        {
            indicator.gameObject.SetActive(showLocalIndicator);
            ConfigureRect(indicator, ApplyPlayerIndicatorDownOffset(new Vector2(0f, -132f)), new Vector2(93.0246f, 90f), new Vector2(0.5f, 0.5f));
        }
        if (triangle != null)
        {
            triangle.gameObject.SetActive(showLocalIndicator);
            ConfigureRect(triangle, new Vector2(0f, 4f), GetEquilateralTriangleRectSize(TriangleGraphic.Direction.Up, 27.0481f), new Vector2(0.5f, 0.5f));
            var triangleGraphic = triangle.GetComponent<TriangleGraphic>();
            if (triangleGraphic != null)
            {
                triangleGraphic.PointingDirection = TriangleGraphic.Direction.Up;
                triangleGraphic.color = _promptPaperColor;
                triangleGraphic.raycastTarget = true;
            }
        }
        if (youText != null)
        {
            youText.gameObject.SetActive(showLocalIndicator);
            ConfigureRect(youText, new Vector2(1.9196f, -28f), new Vector2(71f, 48.8f), new Vector2(0.5f, 0.5f));
        }
    }

    static Vector2 GetEquilateralTriangleRectSize(TriangleGraphic.Direction direction, float sideLength)
    {
        var altitude = sideLength * EquilateralTriangleAltitude;
        return direction == TriangleGraphic.Direction.Left || direction == TriangleGraphic.Direction.Right
            ? new Vector2(altitude, sideLength)
            : new Vector2(sideLength, altitude);
    }

    Vector2 ApplyPlayerIndicatorDownOffset(Vector2 position) => position + new Vector2(0f, -Mathf.Max(0f, _playerIndicatorDownOffset));

    Vector2 GetGameplayP1IconPosition() => new Vector2(-840f, -40f);
    Vector2 GetGameplayP2IconPosition() => new Vector2(-840f, -197.5f);

    void HideDeprecatedGameplayPlayerLabelCopies()
    {
        SetOptionalGameObjectActive(_gameplayP1Box, false);
        SetOptionalGameObjectActive(_gameplayP2Box, false);
        SetOptionalGameObjectActive(_gameplayP1Text, false);
        SetOptionalGameObjectActive(_gameplayP2Text, false);
    }

    void SetOptionalGameObjectActive(Component component, bool active)
    {
        if (component != null)
            component.gameObject.SetActive(active);
    }

    void StartGameplayTimerPreview()
    {
        if (_gameplayTimerBar == null) return;

        _gameplayTimerBar.DOKill();
        _gameplayTimerBar.DOSizeDelta(new Vector2(_gameplayTimerPreviewWidth, _gameplayTimerBar.sizeDelta.y), _gameplayTimerDrainPreviewDuration)
            .SetEase(Ease.Linear)
            .SetId(this);
    }

    public void SetGameplayPlayerWords(string p1Word, string p2Word)
    {
        _gameplayP1Word = p1Word ?? "";
        _gameplayP2Word = p2Word ?? "";
        RefreshGameplayLetterBlocks();
    }

    /// <summary>
    /// Shared TMP clears without onValueChanged; reset the word used for banned-letter styling so letter rows match an empty field.
    /// </summary>
    public void ResetGameplaySharedInputLetterPreviewForRefresh()
    {
        _gameplayP1Word = "";
    }

    /// <summary>
    /// Mirrors <see cref="UI.GameScreenUI.UpdateHintText"/> — driven by <see cref="Client.HintText"/> via <see cref="UI.UIManager.UpdateGameScreenHintText"/>.
    /// </summary>
    public void SetGameplayHintText(string hint)
    {
        if (_gameplayHintText == null) return;
        _gameplayHintText.text = MainUiDisplayText(hint ?? string.Empty);
    }

    public void SetGameplayPlayerWord(int playerIndex, string word)
    {
        if (playerIndex == 2)
            _gameplayP2Word = word ?? "";
        else
            _gameplayP1Word = word ?? "";

        RefreshGameplayLetterBlocks();
    }

    void RegisterGameplayInputListener()
    {
        if (_inputField == null || _gameplayInputListenerRegistered) return;

        _inputField.onValueChanged.AddListener(OnGameplayInputValueChanged);
        _gameplayInputListenerRegistered = true;
    }

    void UnregisterGameplayInputListener()
    {
        if (_inputField == null || !_gameplayInputListenerRegistered) return;

        _inputField.onValueChanged.RemoveListener(OnGameplayInputValueChanged);
        _gameplayInputListenerRegistered = false;
    }

    void OnGameplayInputValueChanged(string value)
    {
        _gameplayP1Word = value ?? "";
        if (_currentState == MainUIState.Gameplay)
        {
            _gameplayP1SyncedLetterCount = -1;
            RefreshGameplayLetterBlocks();
        }
    }

    void PlayTypingMusicOnEnteredGameplay()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayTypingMusic();
    }

    /// <summary>
    /// Shared finalization when <see cref="MainUIState.Gameplay"/> is shown. Required for <see cref="TransitionToConfiguredState"/> path:
    /// <see cref="PrepareGeneratedStateTarget"/> runs <see cref="PrepareGameplayInputFieldStart"/> which sets input alpha to 0 until this runs.
    /// </summary>
    void FinishEnteringGameplayState()
    {
        if (_debugGameplaySharedInputVisibility)
            DebugLogSharedInputField("FinishEnteringGameplayState.start", null, includeAncestors: true);

        if (_promptSharedGroup != null)
            _promptSharedGroup.alpha = 1f;

        var preserveSubmitLock = TryGetLocalOwnerClient(out var ownerClient) && ownerClient.AnswerCheckedValid.Value;
        SetSharedPromptVisibleForGameplay(preserveSubmitLock);
        SetGameplayPlayerIconsVisible();
        // Snap serialized state groups after layout; then re-apply input strip so it is not left at alpha 0 by
        // IsManuallyRevealed / missing references in _stateGroups vs. FadeStateDifference skipping unchanged groups.
        SetStateVisibilityImmediate(MainUIState.Gameplay);
        if (!preserveSubmitLock)
        {
            UI.UIManager.Instance?.UpdateAnswerInputFieldInteractability(true);
            FocusGameplayInputField();
        }
        else
            UI.UIManager.Instance?.UpdateAnswerInputFieldInteractability(false);

        NotifyGameplayUiEnteredToServer();
        RegisterLocalOwnerGameplayAnswerInput();
        PlayTypingMusicOnEnteredGameplay();
        SubscribeRoundTimerAcceleratedVisualIfNeeded();

        if (_debugGameplaySharedInputVisibility)
            StartCoroutine(DebugLogSharedInputFieldNextFrameCoroutine("FinishEnteringGameplayState+1frame"));
    }

    IEnumerator DebugLogSharedInputFieldNextFrameCoroutine(string phase)
    {
        yield return null;
        DebugLogSharedInputField(phase, MainUIState.Gameplay, includeAncestors: true);
    }

    /// <summary>Same semantics as <see cref="UI.GameScreenUI.UpdateTimer"/> — normalized remaining time in [0,1].</summary>
    public void UpdateGameplayRoundTimer(float normalizedRemaining, bool roundTimeAccelerated)
    {
        if (_currentState != MainUIState.Gameplay) return;

        EnsureGameplayElementsView();
        if (_gameplayTimerBar == null) return;

        _gameplayTimerBar.DOKill(false);
        var w = Mathf.Max(1f, GameplayTimerBarFullWidth * Mathf.Clamp01(normalizedRemaining));
        _gameplayTimerBar.sizeDelta = new Vector2(w, _gameplayTimerBar.sizeDelta.y);

        ApplyGameplayTimerBarAcceleratedColor(roundTimeAccelerated);
    }

    void SubscribeRoundTimerAcceleratedVisualIfNeeded()
    {
        var rm = FindAnyObjectByType<RoundManager>();
        if (rm == null) return;
        if (_roundManagerAccelVisualSubscription == rm) return;

        UnsubscribeRoundTimerAcceleratedVisual();
        _roundManagerAccelVisualSubscription = rm;
        _roundManagerAccelVisualSubscription.AnyPlayerSubmittedThisRound.OnValueChanged += OnAnyPlayerSubmittedThisRoundForTimerBar;

        if (_currentState == MainUIState.Gameplay)
        {
            EnsureGameplayElementsView();
            ApplyGameplayTimerBarAcceleratedColor(_roundManagerAccelVisualSubscription.AnyPlayerSubmittedThisRound.Value);
        }
    }

    void UnsubscribeRoundTimerAcceleratedVisual()
    {
        if (_roundManagerAccelVisualSubscription == null) return;

        _roundManagerAccelVisualSubscription.AnyPlayerSubmittedThisRound.OnValueChanged -= OnAnyPlayerSubmittedThisRoundForTimerBar;
        _roundManagerAccelVisualSubscription = null;
    }

    void OnAnyPlayerSubmittedThisRoundForTimerBar(bool previousValue, bool newValue)
    {
        ApplyGameplayTimerBarAcceleratedColor(newValue);
    }

    void ApplyGameplayTimerBarAcceleratedColor(bool accelerated)
    {
        if (_gameplayTimerBar == null) return;

        var img = _gameplayTimerBar.GetComponent<Image>();
        if (img != null)
            img.color = accelerated ? _gameplayTimerBarAcceleratedColor : _gameplayTimerBarNormalColor;
    }

    /// <summary>Matches <see cref="UI.GameScreenUI.UpdateP1LettersCountUI"/> — letter block count + owner row scale.</summary>
    public void UpdateGameplayP1LetterBlocks(int lettersCount, bool isOwner)
    {
        if (_currentState != MainUIState.Gameplay) return;

        EnsureGameplayElementsView();
        if (_gameplayP1LetterGroup == null) return;
        _gameplayP1SyncedLetterCount = Mathf.Max(0, lettersCount);
        _gameplayP1LetterGroup.localScale = new Vector3(1f, isOwner ? GameplayLetterRowOwnerScaleY : 1f, 1f);
        RefreshGameplayLetterBlocks();
    }

    /// <summary>Matches <see cref="UI.GameScreenUI.UpdateP2LettersCountUI"/>.</summary>
    public void UpdateGameplayP2LetterBlocks(int lettersCount, bool isOwner)
    {
        if (_currentState != MainUIState.Gameplay) return;

        EnsureGameplayElementsView();
        if (_gameplayP2LetterGroup == null) return;
        _gameplayP2SyncedLetterCount = Mathf.Max(0, lettersCount);
        _gameplayP2LetterGroup.localScale = new Vector3(1f, isOwner ? GameplayLetterRowOwnerScaleY : 1f, 1f);
        RefreshGameplayLetterBlocks();
    }

    void RefreshGameplayLetterBlocks()
    {
        var p1Count = _gameplayP1SyncedLetterCount >= 0 ? _gameplayP1SyncedLetterCount : CountLetters(_gameplayP1Word);
        var p2Count = _gameplayP2SyncedLetterCount >= 0 ? _gameplayP2SyncedLetterCount : CountLetters(_gameplayP2Word);

        UpdateGameplayLetterBlockGroup(_gameplayP1LetterGroup, p1Count, p2Count, _gameplayP1LetterColor, ContainsBannedPromptLetter(_gameplayP1Word));
        UpdateGameplayLetterBlockGroup(_gameplayP2LetterGroup, p2Count, p1Count, _gameplayP2LetterColor, ContainsBannedPromptLetter(_gameplayP2Word));
    }

    void UpdateGameplayLetterBlockGroup(RectTransform parent, int count, int opposingCount, Color playerColor, bool hasBannedLetter)
    {
        if (parent == null) return;

        for (var i = 0; i < parent.childCount; i++)
        {
            if (!(parent.GetChild(i) is RectTransform child)) continue;

            var isVisible = i < count;
            child.gameObject.SetActive(isVisible);

            var image = child.GetComponent<Image>();
            if (image != null)
                image.DOKill();

            if (!isVisible) continue;

            ConfigureGameplayLetterBlockRect(child, i);
            if (image != null)
                ApplyGameplayLetterBlockColor(image, i, count, opposingCount, playerColor, hasBannedLetter);
        }

        for (var i = parent.childCount; i < count; i++)
        {
            var block = GetOrCreateImage($"LetterBlock{i + 1}", parent, _gameplayLetterNeutralColor).rect;
            ConfigureGameplayLetterBlockRect(block, i);
            var image = block != null ? block.GetComponent<Image>() : null;
            if (image != null)
                ApplyGameplayLetterBlockColor(image, i, count, opposingCount, playerColor, hasBannedLetter);
        }
    }

    void ConfigureGameplayLetterBlockRect(RectTransform block, int index)
    {
        if (block == null) return;

        block.anchorMin = new Vector2(0f, 0.5f);
        block.anchorMax = new Vector2(0f, 0.5f);
        block.pivot = new Vector2(0f, 0.5f);
        block.anchoredPosition = new Vector2(index * _gameplayLetterBlockSpacing, 0f);
        block.sizeDelta = _gameplayLetterBlockSize;
        block.localScale = Vector3.one;
    }

    void ApplyGameplayLetterBlockColor(Image image, int index, int count, int opposingCount, Color playerColor, bool hasBannedLetter)
    {
        if (image == null) return;

        image.DOKill();

        if (hasBannedLetter)
        {
            image.color = _gameplayLetterNeutralColor;
            image.DOColor(_gameplayBannedFlashColor, _gameplayBannedFlashDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetId(this);
            return;
        }

        image.color = count > opposingCount && index >= opposingCount ? playerColor : _gameplayLetterNeutralColor;
    }

    int CountLetters(string word)
    {
        if (string.IsNullOrEmpty(word)) return 0;

        var count = 0;
        foreach (var c in word)
        {
            if (char.IsLetter(c))
                count++;
        }

        return count;
    }

    bool ContainsBannedPromptLetter(string word)
    {
        if (string.IsNullOrEmpty(word) || string.IsNullOrEmpty(_promptBannedLetters))
            return false;

        foreach (var c in word)
        {
            if (IsBannedPromptLetter(c))
                return true;
        }

        return false;
    }

    string GetBannedLetterQuotedText()
    {
        return string.IsNullOrEmpty(_promptBannedLetters) ? string.Empty : $"\"{_promptBannedLetters}\"";
    }

    void ConfigureRect(RectTransform rect, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 pivot)
    {
        if (rect == null) return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
    }

    void SetPivotKeepingVisualPosition(RectTransform rect, Vector2 pivot)
    {
        if (rect == null || rect.pivot == pivot) return;

        var delta = pivot - rect.pivot;
        rect.anchoredPosition += new Vector2(delta.x * rect.sizeDelta.x, delta.y * rect.sizeDelta.y);
        rect.pivot = pivot;
    }

    void SetTextAlpha(TMP_Text text, float alpha)
    {
        if (text == null) return;

        text.alpha = alpha;
    }

    void SetCanvasGroupAlpha(CanvasGroup group, float alpha)
    {
        if (group == null) return;

        group.alpha = alpha;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    void SetGraphicAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null) return;

        var color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    void SetRoundResultDeathLineAlpha(float alpha)
    {
        if (_roundResultDeathLineGroup == null) return;

        for (var i = 0; i < _roundResultDeathLineGroup.childCount; i++)
        {
            var graphic = _roundResultDeathLineGroup.GetChild(i).GetComponent<Graphic>();
            if (graphic != null)
                SetGraphicAlpha(graphic, alpha);
        }
    }

    void ConfigureTopLeftRect(RectTransform rect, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        if (rect == null) return;

        SetTopLeftAnchors(rect);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
    }

    void ConfigureTopCenterRect(RectTransform rect, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        if (rect == null) return;

        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
    }

    void ConfigureCenterLeftRect(RectTransform rect, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        if (rect == null) return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
    }

    void SetTopLeftAnchors(RectTransform rect)
    {
        if (rect == null) return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
    }

    void SetCenterLeftAnchors(RectTransform rect)
    {
        if (rect == null) return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
    }

    void SetCenterLeftAnchorsKeepingVisualPosition(RectTransform rect)
    {
        if (rect == null) return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        SetPivotKeepingVisualPosition(rect, new Vector2(0f, 0.5f));
    }

    RectTransform FindChildRect(Transform parent, string childName)
    {
        if (parent == null) return null;

        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == childName && child is RectTransform rect)
                return rect;
        }

        return null;
    }

    RectTransform CreateRect(string childName, Transform parent)
    {
        if (!CanCreatePrefabOwnedUi(childName)) return null;

        var go = new GameObject(childName, typeof(RectTransform));
        go.layer = gameObject.layer;
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    bool CanCreatePrefabOwnedUi(string itemName)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && CanEditPrefabAssetStructure()) return true;
#endif
        // In play mode, allow limited runtime creation of missing prefab-owned UI so the
        // new unified-input flow can function even if MainUI.prefab is missing draft groups.
        // This creates the objects UNDER the MainUI instance (not as scene roots), so it
        // remains self-contained.
        if (Application.isPlaying)
        {
            var allow =
                itemName == GameplayElementsGroupName
                || itemName.StartsWith("Gameplay", System.StringComparison.Ordinal)
                || itemName == RoundResultElementsGroupName
                || itemName.StartsWith("RoundResult", System.StringComparison.Ordinal);

            if (allow)
            {
                Debug.LogWarning($"MainUIController: '{itemName}' missing in MainUI.prefab; creating at runtime as a temporary fallback.");
                return true;
            }
        }

        Debug.LogError($"MainUIController expects '{itemName}' to exist in MainUI.prefab. Runtime UI creation is disabled.");
        return false;
    }

#if UNITY_EDITOR
    bool CanEditPrefabAssetStructure()
    {
        if (s_buildingLoadedPrefabAsset)
            return true;

        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject))
            return true;

        var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        return prefabStage != null
            && prefabStage.prefabContentsRoot != null
            && transform.IsChildOf(prefabStage.prefabContentsRoot.transform);
    }
#endif

    void DisableSceneOwnedGeneratedUiOrphans()
    {
        if (!Application.isPlaying) return;

        var rootObjects = gameObject.scene.GetRootGameObjects();
        foreach (var rootObject in rootObjects)
        {
            if (rootObject == null || rootObject.transform == transform.root) continue;
            if (!IsGeneratedPromptOrGameplayObjectName(rootObject.name)) continue;

            rootObject.SetActive(false);
            Debug.LogWarning($"Disabled scene-owned generated UI orphan '{rootObject.name}'. The shared prompt/gameplay UI must live under MainUI.prefab.");
        }
    }

    bool IsGeneratedPromptOrGameplayObjectName(string objectName)
    {
        switch (objectName)
        {
            case "PromptBackground":
            case "PromptTitleText":
            case "PromptBannedText":
            case "PromptMaskTitleText":
            case "PromptMaskBannedText":
            case "PromptMainBlackMask":
            case "PromptBannedBlackMask":
            case "GameplayBackground":
            case "GameplayPromptText":
            case "GameplayBannedLabelText":
            case "GameplayBannedLetterText":
            case "GameplayTimerBar":
            case "GameplayP1Box":
            case "GameplayP2Box":
            case "GameplayP1LetterGroup":
            case "GameplayP2LetterGroup":
            case "GameplayP1Text":
            case "GameplayP2Text":
            case "RoundResultPanel":
            case "RoundResultYellowStripe":
            case "RoundResultBlueStripe":
            case "RoundResultRedStripe":
            case "RoundResultP1WordText":
            case "RoundResultP2WordText":
            case "RoundResultDeathLabelText":
            case "RoundResultP1ScoreText":
            case "RoundResultP2ScoreText":
            case "RoundResultP1ScoreBar":
            case "RoundResultP2ScoreBar":
            case "RoundResultDeathLineGroup":
                return true;
            default:
                return false;
        }
    }

    void StretchToParent(RectTransform rect)
    {
        if (rect == null) return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    [ContextMenu("Capture Current As Configured State Target")]
    void CaptureCurrentAsConfiguredStateTarget()
    {
        var animationSet = GetStateAnimationSet(_currentState);
        if (animationSet == null)
        {
            Debug.LogWarning($"No configured animation set found for {_currentState}");
            return;
        }

        if (animationSet.rectTargets != null)
        {
            foreach (var target in animationSet.rectTargets)
            {
                if (target?.rect == null) continue;
                target.anchoredPosition = target.rect.anchoredPosition;
                target.sizeDelta = target.rect.sizeDelta;
            }
        }

        Debug.Log($"Captured configured animation targets for {_currentState}");
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Capture Current As Tutorial Target")]
    void CaptureCurrentAsTutorialTarget()
    {
        _barTutorialAnchoredPos = _cmykBar.anchoredPosition;
        _barTutorialSize = _cmykBar.sizeDelta;
        if (_inputFieldRect != null) _inputFieldTutorialHeight = _inputFieldRect.sizeDelta.y;
        if (_decorativeLines != null)
        {
            foreach (var l in _decorativeLines)
            {
                if (l?.rect == null) continue;
                l.tutorialAnchoredPos = l.rect.anchoredPosition;
                l.tutorialSizeDelta = l.rect.sizeDelta;
            }
        }
        Debug.Log("Captured tutorial targets (bar, input height, decorative lines)");
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Capture Current As Room ID Target")]
    void CaptureCurrentAsRoomIdTarget()
    {
        _barRoomIdAnchoredPos = _cmykBar.anchoredPosition;
        _barRoomIdSize = _cmykBar.sizeDelta;
        _roomIdSkew = _graphicM.Skew;
        _roomIdMWidth = _layoutM.preferredWidth;
        _roomIdYWidth = _layoutY.preferredWidth;
        _roomIdCWidth = _layoutC.preferredWidth;
        if (_inputFieldRect != null) _inputFieldRoomIdSize = _inputFieldRect.sizeDelta;
        if (_decorativeLines != null)
        {
            foreach (var l in _decorativeLines)
            {
                if (l?.rect == null) continue;
                l.roomIdAnchoredPos = l.rect.anchoredPosition;
                l.roomIdSizeDelta = l.rect.sizeDelta;
            }
        }
        Debug.Log("Captured room id targets (bar, stripe widths, input size, decorative lines)");
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Capture Current As Waiting Target")]
    void CaptureCurrentAsWaitingTarget()
    {
        if (_waitingPanel != null)
            _waitingPanelTargetSize = _waitingPanel.sizeDelta;
        if (_decorativeLines != null)
        {
            foreach (var l in _decorativeLines)
            {
                if (l?.rect == null) continue;
                l.waitingAnchoredPos = l.rect.anchoredPosition;
                l.waitingSizeDelta = l.rect.sizeDelta;
            }
        }
        Debug.Log("Captured waiting targets (panel size, decorative lines)");
    }

    [ContextMenu("Capture Current As Waiting Panel Start")]
    void CaptureCurrentAsWaitingPanelStart()
    {
        if (_waitingPanel != null)
        {
            _waitingPanelStartAnchoredPos = _waitingPanel.anchoredPosition;
            _waitingPanelStartSize = _waitingPanel.sizeDelta;
            Debug.Log($"Captured waiting panel start: pos {_waitingPanelStartAnchoredPos}, size {_waitingPanelStartSize}");
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    Tween TweenSkew(ParallelogramGraphic g, float target)
    {
        return DOTween.To(() => g.Skew, x => g.Skew = x, target, _duration).SetEase(_ease);
    }

    Tween TweenPreferredWidth(LayoutElement le, float target)
    {
        return DOTween.To(() => le.preferredWidth, x => le.preferredWidth = x, target, _duration).SetEase(_ease);
    }

    void AddCmykBarToTutorialTween(Sequence seq)
    {
        if (seq == null) return;

        var phaseOne = GetCmykShapePhaseSeconds();
        var phaseTwo = GetCmykHeightPhaseSeconds();
        var currentSize = _cmykBar != null ? _cmykBar.sizeDelta : _barTutorialSize;
        var rectangleSize = new Vector2(_barTutorialSize.x, Mathf.Max(1f, currentSize.y));
        var tutorialBottomPosition = _barTutorialAnchoredPos - new Vector2(0f, _barTutorialSize.y * 0.5f);

        var cmykSeq = DOTween.Sequence().SetId(this);
        cmykSeq.Join(TweenSkew(_graphicM, 0f, phaseOne));
        cmykSeq.Join(TweenSkew(_graphicY, 0f, phaseOne));
        cmykSeq.Join(TweenSkew(_graphicC, 0f, phaseOne));
        cmykSeq.Join(TweenSkew(_graphicK, 0f, phaseOne));
        cmykSeq.Join(TweenPreferredWidth(_layoutM, _stripeNarrowWidth, phaseOne));
        cmykSeq.Join(TweenPreferredWidth(_layoutY, _stripeNarrowWidth, phaseOne));
        cmykSeq.Join(TweenPreferredWidth(_layoutC, _stripeNarrowWidth, phaseOne));
        if (_cmykBar != null)
        {
            SetPivotKeepingVisualPosition(_cmykBar, new Vector2(0.5f, 0f));
            cmykSeq.Join(_cmykBar.DOAnchorPos(tutorialBottomPosition, phaseOne).SetEase(_ease));
            cmykSeq.Join(_cmykBar.DOSizeDelta(rectangleSize, phaseOne).SetEase(_ease));
            cmykSeq.Append(_cmykBar.DOSizeDelta(_barTutorialSize, phaseTwo).SetEase(_ease));
        }

        seq.Join(cmykSeq);
    }

    void AddCmykBarToRoomIdTween(Sequence seq)
    {
        if (seq == null) return;

        var phaseOne = GetCmykShapePhaseSeconds();
        var phaseTwo = GetCmykHeightPhaseSeconds();
        var currentSize = _cmykBar != null ? _cmykBar.sizeDelta : _barRoomIdSize;
        var rectangleSize = new Vector2(_barRoomIdSize.x, Mathf.Max(1f, currentSize.y));

        var cmykSeq = DOTween.Sequence().SetId(this);
        cmykSeq.Join(TweenSkew(_graphicM, 0f, phaseOne));
        cmykSeq.Join(TweenSkew(_graphicY, 0f, phaseOne));
        cmykSeq.Join(TweenSkew(_graphicC, 0f, phaseOne));
        cmykSeq.Join(TweenSkew(_graphicK, 0f, phaseOne));
        cmykSeq.Join(TweenPreferredWidth(_layoutM, _roomIdMWidth, phaseOne));
        cmykSeq.Join(TweenPreferredWidth(_layoutY, _roomIdYWidth, phaseOne));
        cmykSeq.Join(TweenPreferredWidth(_layoutC, _roomIdCWidth, phaseOne));
        if (_cmykBar != null)
        {
            SetPivotKeepingVisualPosition(_cmykBar, new Vector2(0.5f, 0.5f));
            cmykSeq.Join(_cmykBar.DOAnchorPos(_barRoomIdAnchoredPos, phaseOne).SetEase(_ease));
            cmykSeq.Join(_cmykBar.DOSizeDelta(rectangleSize, phaseOne).SetEase(_ease));
            cmykSeq.Append(_cmykBar.DOSizeDelta(_barRoomIdSize, phaseTwo).SetEase(_ease));
            cmykSeq.Join(TweenSkew(_graphicM, _roomIdSkew, phaseTwo));
            cmykSeq.Join(TweenSkew(_graphicY, _roomIdSkew, phaseTwo));
            cmykSeq.Join(TweenSkew(_graphicC, _roomIdSkew, phaseTwo));
            cmykSeq.Join(TweenSkew(_graphicK, _roomIdSkew, phaseTwo));
        }

        seq.Join(cmykSeq);
    }

    void AddCmykBarFromTutorialToRoomIdTween(Sequence seq)
    {
        if (seq == null) return;

        var phaseOne = GetCmykHeightPhaseSeconds();
        var phaseTwo = GetCmykShapePhaseSeconds();
        var collapsedSize = new Vector2(_barTutorialSize.x, Mathf.Max(1f, _barRoomIdSize.y));
        var tutorialBottomPosition = _barTutorialAnchoredPos - new Vector2(0f, _barTutorialSize.y * 0.5f);

        var cmykSeq = DOTween.Sequence().SetId(this);
        if (_cmykBar != null)
        {
            SetPivotKeepingVisualPosition(_cmykBar, new Vector2(0.5f, 0f));
            cmykSeq.Append(_cmykBar.DOAnchorPos(tutorialBottomPosition, phaseOne).SetEase(_ease));
            cmykSeq.Join(_cmykBar.DOSizeDelta(collapsedSize, phaseOne).SetEase(_ease));
            cmykSeq.AppendCallback(() => SetPivotKeepingVisualPosition(_cmykBar, new Vector2(0.5f, 0.5f)));
            cmykSeq.Append(_cmykBar.DOAnchorPos(_barRoomIdAnchoredPos, phaseTwo).SetEase(_ease));
            cmykSeq.Join(_cmykBar.DOSizeDelta(_barRoomIdSize, phaseTwo).SetEase(_ease));
        }
        cmykSeq.Join(TweenSkew(_graphicM, _roomIdSkew, phaseTwo));
        cmykSeq.Join(TweenSkew(_graphicY, _roomIdSkew, phaseTwo));
        cmykSeq.Join(TweenSkew(_graphicC, _roomIdSkew, phaseTwo));
        cmykSeq.Join(TweenSkew(_graphicK, _roomIdSkew, phaseTwo));
        cmykSeq.Join(TweenPreferredWidth(_layoutM, _roomIdMWidth, phaseTwo));
        cmykSeq.Join(TweenPreferredWidth(_layoutY, _roomIdYWidth, phaseTwo));
        cmykSeq.Join(TweenPreferredWidth(_layoutC, _roomIdCWidth, phaseTwo));

        seq.Join(cmykSeq);
    }

    float GetStartJoinSweepDeltaX()
    {
        if (_cmykBar == null)
            return 0f;

        var parent = _cmykBar.parent as RectTransform;
        var targetLeft = parent != null ? parent.rect.xMin - Mathf.Max(0f, _startJoinSweepExtraLeft) : -_lockedResolution.x * 0.5f;
        var currentLeft = GetCmykBarLeftEdgeInParentSpace();
        return targetLeft - currentLeft;
    }

    void GetStartJoinSweepBarTargets(float leftEdgeDeltaX, ref Vector2 targetPosition, ref Vector2 targetSize)
    {
        if (_cmykBar == null)
            return;

        var widthDelta = Mathf.Max(0f, -leftEdgeDeltaX);
        targetSize.x += widthDelta;
        targetPosition.x += leftEdgeDeltaX * (1f - _cmykBar.pivot.x);
    }

    float GetCmykBarLeftEdgeInParentSpace()
    {
        if (_cmykBar == null)
            return 0f;

        var parent = _cmykBar.parent as RectTransform;
        if (parent == null)
            return _cmykBar.anchoredPosition.x + _cmykBar.rect.xMin;

        var corners = new Vector3[4];
        _cmykBar.GetWorldCorners(corners);
        var left = float.PositiveInfinity;
        for (var i = 0; i < corners.Length; i++)
            left = Mathf.Min(left, parent.InverseTransformPoint(corners[i]).x);

        return left;
    }

    void UpdateTitleFromStartJoinSweep(TMP_Text titleText, int characterCount, float startTipX, float targetTipX, Vector2 titleStartPosition)
    {
        if (titleText == null)
            return;

        var currentTipX = GetCmykBarLeftEdgeInParentSpace();
        var denominator = Mathf.Abs(targetTipX - startTipX);
        var progress = denominator <= Mathf.Epsilon ? 1f : Mathf.Clamp01(Mathf.Abs(currentTipX - startTipX) / denominator);
        var titleRect = titleText.rectTransform;
        if (titleRect != null)
            titleRect.anchoredPosition = titleStartPosition + new Vector2(currentTipX - startTipX, 0f);

        if (characterCount > 0)
            SetTitleVisibleCharacters(titleText, Mathf.CeilToInt(characterCount * (1f - progress)));
    }

    TMP_Text GetTitleText()
    {
        return _titleTypewriter != null ? _titleTypewriter.GetComponent<TMP_Text>() : null;
    }

    TMP_Text GetHintText()
    {
        return _hintTypewriter != null ? _hintTypewriter.GetComponent<TMP_Text>() : null;
    }

    int GetTitleCharacterCount(TMP_Text titleText)
    {
        if (titleText == null)
            return 0;

        titleText.ForceMeshUpdate();
        return titleText.textInfo.characterCount;
    }

    void SetTitleVisibleCharacters(TMP_Text titleText, int visibleCharacters)
    {
        if (titleText == null)
            return;

        titleText.maxVisibleCharacters = Mathf.Max(0, visibleCharacters);
    }

    int GetVisibleTextCharacterCount(TMP_Text text)
    {
        if (text == null)
            return 0;

        var previousVisibleCharacters = text.maxVisibleCharacters;
        text.maxVisibleCharacters = int.MaxValue;
        text.ForceMeshUpdate();
        var characterCount = text.textInfo.characterCount;
        text.maxVisibleCharacters = previousVisibleCharacters;
        return characterCount;
    }

    void SetTextVisibleCharacters(TMP_Text text, int visibleCharacters)
    {
        if (text == null)
            return;

        text.maxVisibleCharacters = Mathf.Max(0, visibleCharacters);
    }

    void SetTextFullyVisible(TMP_Text text)
    {
        if (text == null)
            return;

        text.maxVisibleCharacters = int.MaxValue;
    }

    Tween TweenHintAlpha(TMP_Text hintText, float targetAlpha, float duration)
    {
        if (hintText == null)
            return null;

        return DOTween.To(() => hintText.color.a, alpha => SetHintAlpha(hintText, alpha), targetAlpha, duration)
            .SetEase(_startJoinSweepEase)
            .SetId(this);
    }

    void SetHintAlpha(TMP_Text hintText, float alpha)
    {
        if (hintText == null)
            return;

        var color = hintText.color;
        color.a = Mathf.Clamp01(alpha);
        hintText.color = color;
    }

    void CaptureStartTitleInitialPosition(TMP_Text titleText)
    {
        if (_hasStartTitleInitialAnchoredPosition || titleText == null || titleText.rectTransform == null)
            return;

        _startTitleInitialAnchoredPosition = titleText.rectTransform.anchoredPosition;
        _hasStartTitleInitialAnchoredPosition = true;
    }

    Vector2 GetStartTitleAnchoredPosition(TMP_Text titleText)
    {
        if (titleText == null || titleText.rectTransform == null)
            return Vector2.zero;

        return titleText.rectTransform.anchoredPosition;
    }

    void RestoreStartTitleInitialPosition()
    {
        var titleText = GetTitleText();
        if (!_hasStartTitleInitialAnchoredPosition || titleText == null || titleText.rectTransform == null)
            return;

        titleText.rectTransform.anchoredPosition = _startTitleInitialAnchoredPosition;
    }

    void CaptureStartHintInitialColor(TMP_Text hintText)
    {
        if (_hasStartHintInitialColor || hintText == null)
            return;

        _startHintInitialColor = hintText.color;
        _hasStartHintInitialColor = true;
    }

    void RestoreStartHintInitialColor()
    {
        var hintText = GetHintText();
        if (!_hasStartHintInitialColor || hintText == null)
            return;

        hintText.color = _startHintInitialColor;
    }

    float GetRoomIdBarTransitionSeconds() => GetCmykShapePhaseSeconds() + GetCmykHeightPhaseSeconds();
    float GetCmykShapePhaseSeconds() => Mathf.Max(0.01f, _duration * Mathf.Clamp01(_cmykShapePhaseRatio));
    float GetCmykHeightPhaseSeconds() => Mathf.Max(0.01f, _duration * (1f - Mathf.Clamp01(_cmykShapePhaseRatio)));

    Tween TweenSkew(ParallelogramGraphic g, float target, float duration)
    {
        if (g == null) return null;
        return DOTween.To(() => g.Skew, x => g.Skew = x, target, duration).SetEase(_ease);
    }

    Tween TweenPreferredWidth(LayoutElement le, float target, float duration)
    {
        if (le == null) return null;
        return DOTween.To(() => le.preferredWidth, x => le.preferredWidth = x, target, duration).SetEase(_ease);
    }

    void AddRectTweens(Sequence seq, RectTransformTweenTarget[] targets)
    {
        if (seq == null || targets == null) return;

        foreach (var target in targets)
        {
            if (target?.rect == null) continue;

            if (target.tweenAnchoredPosition)
                seq.Join(target.rect.DOAnchorPos(target.anchoredPosition, _duration).SetEase(_ease));
            if (target.tweenSizeDelta)
                seq.Join(target.rect.DOSizeDelta(target.sizeDelta, _duration).SetEase(_ease));
        }
    }

    void AddCanvasGroupTweens(Sequence seq, CanvasGroupTweenTarget[] targets)
    {
        if (seq == null || targets == null) return;

        foreach (var target in targets)
        {
            if (target?.group == null) continue;

            target.group.interactable = target.interactable;
            target.group.blocksRaycasts = target.blocksRaycasts;
            seq.Join(target.group.DOFade(target.targetAlpha, _fadeOutDuration).SetEase(_ease));
        }
    }

    void ResetTypewriters(TypewriterRevealTarget[] targets)
    {
        if (targets == null) return;

        foreach (var target in targets)
        {
            if (target == null) continue;
            if (target.group != null) target.group.alpha = 0f;
            if (target.typewriter != null) target.typewriter.Hide();
        }
    }

    IEnumerator RevealTypewritersRoutine(TypewriterRevealTarget[] targets)
    {
        if (targets == null) yield break;

        yield return new WaitForSeconds(Mathf.Max(0f, _duration + _postWaitingRevealDelayAfterMotion));

        foreach (var target in targets)
        {
            if (target == null) continue;

            if (target.delay > 0f)
                yield return new WaitForSeconds(target.delay);

            if (target.group != null)
                target.group.DOFade(1f, _waitingContentFadeDuration).SetEase(_ease).SetId(this);

            if (target.typewriter != null)
            {
                target.typewriter.Play();
                yield return new WaitUntil(() => !target.typewriter.IsPlaying);
            }

            if (_postWaitingRevealStagger > 0f)
                yield return new WaitForSeconds(_postWaitingRevealStagger);
        }
    }

    StateAnimationSet GetStateAnimationSet(MainUIState state)
    {
        if (_stateAnimations == null) return null;

        foreach (var animationSet in _stateAnimations)
        {
            if (animationSet != null && animationSet.state == state)
                return animationSet;
        }

        return null;
    }

    CanvasGroup[] GetVisibleGroups(MainUIState state)
    {
        var result = new List<CanvasGroup>();

        if (_stateGroups != null)
        {
            foreach (var groupSet in _stateGroups)
            {
                if (groupSet == null || groupSet.state != state || groupSet.visibleGroups == null) continue;

                foreach (var cg in groupSet.visibleGroups)
                    AddUniqueGroup(result, cg);
            }
        }

        if (state == MainUIState.GameEnd)
        {
            AddUniqueGroup(result, _roundResultElementsGroup);
            AddUniqueGroup(result, GetDecorativeLineStateGroup());
            AddUniqueGroup(result, _pressSpaceGroup);
        }

        return result.ToArray();
    }

    /// <summary>Fades every tracked UI <see cref="CanvasGroup"/> (except the loading wipe) to 0 so Loading never stacks on top at alpha 1.</summary>
    /// <returns><c>true</c> if a fade tween was scheduled.</returns>
    bool AppendFadeOutAllUiCanvasGroupsBeforeLoading(Sequence parent, float duration)
    {
        var unique = new HashSet<CanvasGroup>();
        foreach (var cg in GetAllStateGroups())
        {
            if (cg != null && !ReferenceEquals(cg, _loadingScreenGroup))
                unique.Add(cg);
        }

        foreach (var cg in new[]
                 {
                      _promptSharedGroup, _gameplayElementsGroup, _roundResultElementsGroup,
                      _tutorialTitleGroup, _pressSpaceGroup, _roomIdTitleGroup, _roomIdHintGroup,
                     _waitingTitleGroup, _waitingRoomIdGroup, _waitingHintGroup, _waitingP1Group, _waitingP2Group,
                     _inputFieldContentGroup
                 })
        {
            if (cg != null && !ReferenceEquals(cg, _loadingScreenGroup))
                unique.Add(cg);
        }

        var shell = GetInputFieldStateGroup();
        if (shell != null && !ReferenceEquals(shell, _loadingScreenGroup))
            unique.Add(shell);

        if (unique.Count == 0)
            return false;

        var inner = DOTween.Sequence().SetId(this);
        var first = true;
        foreach (var cg in unique)
        {
            cg.DOKill(false);
            var tw = cg.DOFade(0f, duration).SetEase(_ease);
            if (first)
            {
                inner.Append(tw);
                first = false;
            }
            else inner.Join(tw);
        }

        parent.Append(inner);
        return true;
    }

    void FadeStateDifference(Sequence seq, MainUIState from, MainUIState to)
    {
        var fromGroups = GetVisibleGroups(from);
        var toGroups = GetVisibleGroups(to);

        var phase = DOTween.Sequence().SetId(this);
        var anyTween = false;

        void AddTween(Tween tw)
        {
            if (tw == null) return;
            if (!anyTween)
            {
                phase.Append(tw);
                anyTween = true;
            }
            else phase.Join(tw);
        }

        foreach (var cg in fromGroups)
        {
            if (cg == null || ContainsGroup(toGroups, cg)) continue;
            AddTween(cg.DOFade(0f, _fadeOutDuration).SetEase(_ease));
        }

        foreach (var cg in toGroups)
        {
            if (cg == null || ContainsGroup(fromGroups, cg)) continue;

            cg.alpha = 0f;
            if (IsManuallyRevealed(to, cg)) continue;

            AddTween(cg.DOFade(1f, _fadeOutDuration).SetEase(_ease));
        }

        if (anyTween)
            seq.Append(phase);

        _currentState = to;
        RefreshPromptCalibrationOverlay();
        SyncRoomIdScreenWithUIManager(to);
        UpdateWaitingCommandListenerForState(to);
    }

    void SetStateVisibilityImmediate(MainUIState state)
    {
        if (_debugGameplaySharedInputVisibility && state == MainUIState.Gameplay)
            DebugLogSharedInputField("SetStateVisibilityImmediate.beforeLoop", MainUIState.Gameplay, includeAncestors: true);

        foreach (var cg in GetAllStateGroups())
        {
            if (cg == null) continue;
            cg.alpha = ContainsGroup(GetVisibleGroups(state), cg) && !IsManuallyRevealed(state, cg) ? 1f : 0f;
        }

        if (state == MainUIState.Gameplay)
            ForceGameplaySharedInputFieldCanvasGroupsOpaque();

        if (_debugGameplaySharedInputVisibility && state == MainUIState.Gameplay)
            DebugLogSharedInputField("SetStateVisibilityImmediate.afterForce", MainUIState.Gameplay, includeAncestors: true);

        _currentState = state;
        if (state == MainUIState.Waiting)
            ApplyWaitingDecorativeLineLayoutImmediate();
        RefreshPromptCalibrationOverlay();
        SyncRoomIdScreenWithUIManager(state);
        UpdateWaitingCommandListenerForState(state);
    }

    /// <summary>
    /// TMP "Text Area" <see cref="_inputFieldContentGroup"/> is not part of <see cref="GetAllStateGroups"/>; the input shell
    /// <see cref="GetInputFieldStateGroup"/> can also disagree with serialized state rows. Stop active fades and force full opacity for Gameplay.
    /// </summary>
    void ForceGameplaySharedInputFieldCanvasGroupsOpaque()
    {
        if (_debugGameplaySharedInputVisibility)
            DebugLogSharedInputField("ForceGameplaySharedInputFieldCanvasGroupsOpaque.before", null, includeAncestors: true);

        if (_inputFieldContentGroup != null)
        {
            _inputFieldContentGroup.DOKill(false);
            _inputFieldContentGroup.alpha = 1f;
        }

        var shell = GetInputFieldStateGroup();
        if (shell != null)
        {
            shell.DOKill(false);
            shell.alpha = 1f;
            shell.interactable = true;
            shell.blocksRaycasts = true;
        }

        if (_debugGameplaySharedInputVisibility)
            DebugLogSharedInputField("ForceGameplaySharedInputFieldCanvasGroupsOpaque.after", null, includeAncestors: true);
    }

    /// <summary>Deep visibility debug for <see cref="_debugGameplaySharedInputVisibility"/>.</summary>
    void DebugLogSharedInputField(string phase, MainUIState? visibilitySnapState = null, bool includeAncestors = false)
    {
        if (!_debugGameplaySharedInputVisibility) return;

        var frame = Time.frameCount;
        var shell = GetInputFieldStateGroup();
        var content = _inputFieldContentGroup;
        var sameRef = shell != null && content != null && ReferenceEquals(shell, content);

        Debug.Log(
            $"[MainUIInputDbg][f{frame}] {phase}\n" +
            $"  _currentState(before tick)={_currentState} activeSelf={gameObject.activeInHierarchy}\n" +
            $"  _inputField={( _inputField != null ? _inputField.name : "NULL" )} enabled={(_inputField != null && _inputField.enabled)} interactable={(_inputField != null && _inputField.interactable)}\n" +
            $"  _inputFieldRect={( _inputFieldRect != null ? _inputFieldRect.name : "NULL" )}\n" +
            $"  shellCanvasGroup={( shell != null ? $"{shell.name} alpha={shell.alpha}" : "NULL (GetInputFieldStateGroup)" )}\n" +
            $"  _inputFieldContentGroup={( content != null ? $"{content.name} alpha={content.alpha}" : "NULL" )}\n" +
            $"  shell==contentRef: {sameRef}\n" +
            $"  _gameplayElementsGroup={( _gameplayElementsGroup != null ? $"alpha={_gameplayElementsGroup.alpha}" : "NULL" )}\n" +
            $"  _promptSharedGroup={( _promptSharedGroup != null ? $"alpha={_promptSharedGroup.alpha}" : "NULL" )}",
            this);

        if (includeAncestors && _inputFieldRect != null)
            DebugLogCanvasGroupAncestors(_inputFieldRect, $"{phase}.ancestorsFromInputFieldRect");

        if (visibilitySnapState.HasValue)
        {
            var st = visibilitySnapState.Value;
            var vis = GetVisibleGroups(st);
            var shellInVis = shell != null && ContainsGroup(vis, shell);
            var contentInVis = content != null && ContainsGroup(vis, content);
            var shellMan = shell != null && IsManuallyRevealed(st, shell);
            var contentMan = content != null && IsManuallyRevealed(st, content);
            Debug.Log(
                $"[MainUIInputDbg][f{frame}] {phase} visibilitySnap={st}\n" +
                $"  GetVisibleGroups count={( vis != null ? vis.Length : 0 )} shellInVisible={shellInVis} shellManuallyRevealed={shellMan}\n" +
                $"  contentInVisible={contentInVis} contentManuallyRevealed={contentMan}\n" +
                $"  shellWouldSnapAlpha={( shellInVis && !shellMan ? 1f : 0f )} contentWouldSnapAlpha={( contentInVis && !contentMan ? 1f : 0f )}",
                this);
        }
    }

    void DebugLogCanvasGroupAncestors(Transform leaf, string label)
    {
        if (leaf == null) return;

        var t = leaf;
        for (var depth = 0; t != null && depth < 12; depth++, t = t.parent)
        {
            var cg = t.GetComponent<CanvasGroup>();
            if (cg == null) continue;
            Debug.Log(
                $"[MainUIInputDbg] {label} depth={depth} go='{t.name}' alpha={cg.alpha} interactable={cg.interactable} blocksRaycasts={cg.blocksRaycasts} ignoreParentGroups={cg.ignoreParentGroups}",
                this);
        }
    }

    void SyncRoomIdScreenWithUIManager(MainUIState state)
    {
        if (UI.UIManager.Instance != null)
            UI.UIManager.Instance.SetRoomIdScreenForMainUiState(state);
    }

    void EnsureGeneratedStateView(MainUIState state)
    {
        switch (state)
        {
            case MainUIState.PromptShowcase:
                EnsurePromptSharedView();
                break;
            case MainUIState.Gameplay:
                EnsureGameplayElementsView();
                break;
            case MainUIState.RoundResult:
                EnsurePromptSharedView();
                EnsureRoundResultElementsView();
                break;
            case MainUIState.GameEnd:
                EnsureRoundResultElementsView();
                ConfigureGameEndRestartHint(1f);
                break;
        }
    }

    void PrepareGeneratedStateTarget(MainUIState state)
    {
        switch (state)
        {
            case MainUIState.PromptShowcase:
                PreparePromptShowcaseStart();
                break;
            case MainUIState.Gameplay:
                PrepareGameplayStart();
                SetSharedPromptVisibleForGameplay();
                SetGameplayPlayerIconsVisible();
                break;
            case MainUIState.RoundResult:
                PrepareRoundResultStart();
                ConfigureRoundResultStripes();
                SetSharedPromptVisibleForRoundResult();
                SetRoundResultPlayerIconsVisible();
                break;
            case MainUIState.GameEnd:
                EnsureRoundResultElementsView();
                ConfigureGameEndWinnerText(1f);
                ConfigureGameEndRestartHint(1f);
                if (_roundResultPanel != null)
                    ConfigureImageRect(_roundResultPanel, GetGameEndBlackBarPosition(), GetGameEndBlackBarSize(), _promptInkColor);
                ConfigureGameEndStripes();
                // RoundResult shares _promptSharedGroup with PromptShowcase; fade-only transitions can
                // briefly read as the showcase layer. Hide it immediately before FadeStateDifference.
                SuppressSharedPromptLayerForGameEndTransition();
                break;
        }
    }

    /// <summary>
    /// Hides the shared prompt root (same CanvasGroup as PromptShowcase) so RoundResult → GameEnd
    /// never flashes the prompt-showcase stack during the state fade.
    /// </summary>
    void SuppressSharedPromptLayerForGameEndTransition()
    {
        if (_promptSharedGroup != null)
        {
            _promptSharedGroup.DOKill(false);
            _promptSharedGroup.alpha = 0f;
            _promptSharedGroup.interactable = false;
            _promptSharedGroup.blocksRaycasts = false;
        }

        if (_promptPromptMask != null) _promptPromptMask.gameObject.SetActive(false);
        if (_promptBannedMask != null) _promptBannedMask.gameObject.SetActive(false);
    }

    List<CanvasGroup> GetAllStateGroups()
    {
        var result = new List<CanvasGroup>();
        if (_stateGroups != null)
        {
            foreach (var groupSet in _stateGroups)
            {
                if (groupSet?.visibleGroups == null) continue;

                foreach (var cg in groupSet.visibleGroups)
                    AddUniqueGroup(result, cg);
            }
        }

        return result;
    }

    void AddUniqueGroup(List<CanvasGroup> groups, CanvasGroup cg)
    {
        if (groups == null || cg == null || groups.Contains(cg)) return;
        groups.Add(cg);
    }

#if UNITY_EDITOR
    bool SyncGeneratedStateGroupsForInspector()
    {
        var changed = false;

        changed |= RemoveNullAndDuplicateStateGroups();

        if (_promptSharedGroupRect == null)
        {
            var promptRoot = FindPromptSharedRoot(false);
            if (promptRoot != null)
            {
                _promptSharedGroupRect = promptRoot;
                changed = true;
            }
        }
        if (_promptSharedGroup == null && _promptSharedGroupRect != null)
        {
            var promptGroup = _promptSharedGroupRect.GetComponent<CanvasGroup>();
            if (promptGroup != null)
            {
                _promptSharedGroup = promptGroup;
                changed = true;
            }
        }

        if (_gameplayElementsGroupRect == null)
        {
            var gameplayRoot = FindGameplayElementsRoot(false);
            if (gameplayRoot != null)
            {
                _gameplayElementsGroupRect = gameplayRoot;
                changed = true;
            }
        }
        if (_gameplayElementsGroup == null && _gameplayElementsGroupRect != null)
        {
            var gameplayGroup = _gameplayElementsGroupRect.GetComponent<CanvasGroup>();
            if (gameplayGroup != null)
            {
                _gameplayElementsGroup = gameplayGroup;
                changed = true;
            }
        }

        if (_roundResultElementsGroupRect == null)
        {
            var roundResultRoot = FindRoundResultElementsRoot();
            if (roundResultRoot != null)
            {
                _roundResultElementsGroupRect = roundResultRoot;
                changed = true;
            }
        }
        if (_roundResultElementsGroup == null && _roundResultElementsGroupRect != null)
        {
            var roundResultGroup = _roundResultElementsGroupRect.GetComponent<CanvasGroup>();
            if (roundResultGroup != null)
            {
                _roundResultElementsGroup = roundResultGroup;
                changed = true;
            }
        }

        changed |= AddVisibleGroupToSerializedState(MainUIState.PromptShowcase, _promptSharedGroup);
        changed |= AddVisibleGroupToSerializedState(MainUIState.Gameplay, _promptSharedGroup);
        changed |= AddVisibleGroupToSerializedState(MainUIState.Gameplay, _gameplayElementsGroup);
        changed |= AddVisibleGroupToSerializedState(MainUIState.Gameplay, GetInputFieldStateGroup());
        changed |= AddVisibleGroupToSerializedState(MainUIState.Gameplay, _waitingP1Group);
        changed |= AddVisibleGroupToSerializedState(MainUIState.Gameplay, _waitingP2Group);
        changed |= AddVisibleGroupToSerializedState(MainUIState.RoundResult, _roundResultElementsGroup);
        changed |= AddVisibleGroupToSerializedState(MainUIState.RoundResult, _promptSharedGroup);
        changed |= AddVisibleGroupToSerializedState(MainUIState.RoundResult, GetDecorativeLineStateGroup());
        changed |= AddVisibleGroupToSerializedState(MainUIState.RoundResult, _waitingP1Group);
        changed |= AddVisibleGroupToSerializedState(MainUIState.RoundResult, _waitingP2Group);
        changed |= AddVisibleGroupToSerializedState(MainUIState.GameEnd, _roundResultElementsGroup);
        changed |= AddVisibleGroupToSerializedState(MainUIState.GameEnd, GetDecorativeLineStateGroup());
        changed |= AddVisibleGroupToSerializedState(MainUIState.GameEnd, _pressSpaceGroup);

        return changed;
    }

    bool RemoveNullAndDuplicateStateGroups()
    {
        if (_stateGroups == null) return false;

        var changed = false;
        foreach (var groupSet in _stateGroups)
        {
            if (groupSet == null) continue;

            var visibleGroups = RemoveNullAndDuplicateGroups(groupSet.visibleGroups, out var visibleChanged);
            var manuallyRevealedGroups = RemoveNullAndDuplicateGroups(groupSet.manuallyRevealedGroups, out var manualChanged);
            if (visibleChanged)
            {
                groupSet.visibleGroups = visibleGroups;
                changed = true;
            }
            if (manualChanged)
            {
                groupSet.manuallyRevealedGroups = manuallyRevealedGroups;
                changed = true;
            }
        }

        return changed;
    }

    CanvasGroup[] RemoveNullAndDuplicateGroups(CanvasGroup[] groups, out bool changed)
    {
        changed = false;
        if (groups == null) return groups;

        var cleaned = new List<CanvasGroup>();
        foreach (var group in groups)
        {
            if (group == null)
            {
                changed = true;
                continue;
            }

            if (cleaned.Contains(group))
            {
                changed = true;
                continue;
            }

            cleaned.Add(group);
        }

        return changed ? cleaned.ToArray() : groups;
    }

    bool AddVisibleGroupToSerializedState(MainUIState state, CanvasGroup group)
    {
        if (group == null) return false;

        if (_stateGroups == null)
            _stateGroups = new StateCanvasGroupSet[0];

        for (var i = 0; i < _stateGroups.Length; i++)
        {
            var groupSet = _stateGroups[i];
            if (groupSet == null || groupSet.state != state) continue;

            if (ContainsGroup(groupSet.visibleGroups, group)) return false;

            var visibleGroups = new List<CanvasGroup>();
            if (groupSet.visibleGroups != null)
                visibleGroups.AddRange(groupSet.visibleGroups);
            visibleGroups.Add(group);
            groupSet.visibleGroups = visibleGroups.ToArray();
            return true;
        }

        var stateGroups = new List<StateCanvasGroupSet>(_stateGroups)
        {
            new StateCanvasGroupSet
            {
                state = state,
                visibleGroups = new[] { group },
                manuallyRevealedGroups = new CanvasGroup[0]
            }
        };
        _stateGroups = stateGroups.ToArray();
        return true;
    }
#endif

    CanvasGroup GetInputFieldStateGroup()
    {
        if (_inputFieldRect != null)
        {
            var g = _inputFieldRect.GetComponent<CanvasGroup>();
            if (g != null)
                return g;
        }

        return _inputField != null ? _inputField.GetComponent<CanvasGroup>() : null;
    }

    CanvasGroup GetDecorativeLineStateGroup()
    {
        if (_decorativeLines == null) return null;

        foreach (var line in _decorativeLines)
        {
            if (line?.rect == null) continue;
            var parent = line.rect.parent;
            return parent != null ? parent.GetComponent<CanvasGroup>() : null;
        }

        return null;
    }

    bool IsManuallyRevealed(MainUIState state, CanvasGroup cg)
    {
        if (_stateGroups == null || cg == null) return false;

        foreach (var groupSet in _stateGroups)
        {
            if (groupSet == null || groupSet.state != state) continue;
            if (ContainsGroup(groupSet.manuallyRevealedGroups, cg))
                return true;
        }

        return false;
    }

    bool ContainsGroup(CanvasGroup[] groups, CanvasGroup target)
    {
        if (groups == null || target == null) return false;

        foreach (var cg in groups)
        {
            if (cg == target) return true;
        }

        return false;
    }
}
