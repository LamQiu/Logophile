using System.Collections;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class MainUIController : MonoBehaviour
{
    const string PromptSharedGroupName = "PromptSharedGroup";
    const string DeprecatedPromptShowcaseRootName = "PromptShowcaseRoot";
    const string GameplayElementsGroupName = "GameplayElementsGroup";
    const string DeprecatedGameplayRootName = "GameplayRoot";
    const string RoundResultElementsGroupName = "RoundResultElementsGroup";
    const int RoundResultLayoutVersion = 14;

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
    // K auto-fills remaining width via LayoutElement.flexibleWidth on the K GameObject.

    [Header("Animation")]
    [SerializeField] float _duration = 0.8f;
    [SerializeField] Ease _ease = Ease.InOutQuad;

    [Header("Debug Navigation")]
    [SerializeField] bool _enableDebugNextStateKey = true;
    [SerializeField] KeyCode _debugNextStateKey = KeyCode.Y;

    [Header("State Visibility")]
    [SerializeField] StateCanvasGroupSet[] _stateGroups;
    [SerializeField] MainUIState _currentState = MainUIState.Start;
    [SerializeField, HideInInspector] int _roundResultLayoutVersion;
    [SerializeField] float _fadeOutDuration = 0.4f;

    [Header("Post-Waiting State Animations")]
    [SerializeField] StateAnimationSet[] _stateAnimations;
    [SerializeField] float _postWaitingRevealDelayAfterMotion = 0.1f;
    [SerializeField] float _postWaitingRevealStagger = 0.1f;

    [Header("Tutorial Transition - Input Field")]
    [SerializeField] TMP_InputField _inputField;
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
    [SerializeField] float _roomIdTitleDelay = 0.4f;
    [SerializeField] float _roomIdHintGapAfterTitle = 0.2f;

    [Header("Waiting Transition - InputField")]
    [SerializeField] string _waitingPlaceholder = "ready";

    [Header("Waiting Transition - Black Panel")]
    [SerializeField] RectTransform _waitingPanel;
    [SerializeField] Vector2 _waitingPanelStartAnchoredPos;
    [SerializeField] Vector2 _waitingPanelStartSize;
    [SerializeField] Vector2 _waitingPanelTargetSize;
    [SerializeField] float _waitingPanelRevealAmount = 100f;
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
    [SerializeField] string _promptMaskBannedTextValue = "banned letters";
    [SerializeField] string _promptBannedLetters = "i";
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
    string _gameplayP1Word = "";
    string _gameplayP2Word = "";
    bool _gameplayInputListenerRegistered;

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
    [SerializeField] Color _roundResultTextColor = new Color(0.93333334f, 0.91764706f, 0.89411765f, 1f);
    [SerializeField] Color _roundResultMutedTextColor = new Color(0.53333336f, 0.5254902f, 0.5137255f, 1f);
    [SerializeField] float _roundResultTransitionDuration = 0.45f;
    [SerializeField] float _roundResultFadeGameplayDuration = 0.3f;
    [SerializeField] float _roundResultPanelMorphDuration = 0.55f;
    [SerializeField] float _roundResultStripeRevealDuration = 0.35f;
    [SerializeField] float _roundResultContentFadeDuration = 0.35f;
    [SerializeField] float _roundResultContentStagger = 0.05f;

    [Header("Initial State (capture via context menu)")]
    [SerializeField] Vector2 _initBarPos;
    [SerializeField] Vector2 _initBarSize;
    [SerializeField] float _initMWidth;
    [SerializeField] float _initYWidth;
    [SerializeField] float _initCWidth;
    [SerializeField] float _initSkew = 60f;
    [SerializeField] Vector2 _initInputFieldSize;
    [SerializeField] bool _initialCaptured;

    void Awake()
    {
        DisableSceneOwnedGeneratedUiOrphans();
        if (!_initialCaptured) CaptureInitialState();
        SetStateVisibilityImmediate(_currentState);
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
        if (!needsSharedPromptAndGameplay && !needsRoundResult && !needsRoundResultLayout) return;

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
        if (_inputFieldRect != null) _initInputFieldSize = _inputFieldRect.sizeDelta;
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
        if (_playIntroOnStart) PlayIntro();
    }

    void OnEnable()
    {
        RegisterGameplayInputListener();
    }

    void OnDisable()
    {
        UnregisterGameplayInputListener();
    }

    void Update()
    {
        if (!_enableDebugNextStateKey) return;
        if (Input.GetKeyDown(_debugNextStateKey))
            TransitionToNextDebugState();
    }

    void TransitionToNextDebugState()
    {
        switch (_currentState)
        {
            case MainUIState.Start:
                TransitionToTutorial();
                break;
            case MainUIState.Tutorial:
                TransitionToRoomId();
                break;
            case MainUIState.RoomId:
                TransitionToWaiting();
                break;
            case MainUIState.Waiting:
                TransitionToLoading();
                break;
            case MainUIState.Loading:
                TransitionToPromptShowcase();
                break;
            case MainUIState.PromptShowcase:
                TransitionToGameplay();
                break;
            case MainUIState.Gameplay:
                TransitionToRoundResult();
                break;
            case MainUIState.RoundResult:
                TransitionToGameEnd();
                break;
            case MainUIState.GameEnd:
                ResetToStart();
                break;
        }
    }

    [ContextMenu("Play Intro")]
    public void PlayIntro()
    {
        StopAllCoroutines();
        StartCoroutine(IntroRoutine());
    }

    IEnumerator IntroRoutine()
    {
        _titleTypewriter.Play();
        yield return new WaitUntil(() => !_titleTypewriter.IsPlaying);
        yield return new WaitForSeconds(_introGapSeconds);
        _hintTypewriter.Play();
        yield return new WaitUntil(() => !_hintTypewriter.IsPlaying);
        if (_hintCycler != null) _hintCycler.StartCycling();
    }

    [ContextMenu("Transition To Tutorial")]
    public void TransitionToTutorial()
    {
        DOTween.Kill(this);
        StopAllCoroutines();

        if (_hintCycler != null) _hintCycler.StopCycling();

        var seq = DOTween.Sequence().SetId(this);
        FadeStateDifference(seq, _currentState, MainUIState.Tutorial);

        // Skew -> 0 (parallelogram -> rectangle)
        seq.Join(TweenSkew(_graphicM, 0f));
        seq.Join(TweenSkew(_graphicY, 0f));
        seq.Join(TweenSkew(_graphicC, 0f));
        seq.Join(TweenSkew(_graphicK, 0f));

        // Stripe widths: M/Y/C become narrow, K auto-fills via flexibleWidth
        seq.Join(TweenPreferredWidth(_layoutM, _stripeNarrowWidth));
        seq.Join(TweenPreferredWidth(_layoutY, _stripeNarrowWidth));
        seq.Join(TweenPreferredWidth(_layoutC, _stripeNarrowWidth));

        // Parent: move and resize to tutorial rectangle
        seq.Join(_cmykBar.DOAnchorPos(_barTutorialAnchoredPos, _duration).SetEase(_ease));
        seq.Join(_cmykBar.DOSizeDelta(_barTutorialSize, _duration).SetEase(_ease));

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
        if (_pressSpaceGroup != null) _pressSpaceGroup.alpha = 0f;
        StartCoroutine(TutorialRevealRoutine());
    }

    [ContextMenu("Transition To Room ID")]
    public void TransitionToRoomId()
    {
        DOTween.Kill(this);
        StopAllCoroutines();

        if (_hintCycler != null) _hintCycler.StopCycling();

        var seq = DOTween.Sequence().SetId(this);
        FadeStateDifference(seq, _currentState, MainUIState.RoomId);

        // CMYK bar: back to parallelogram with room-id pos/size and stripe widths
        seq.Join(TweenSkew(_graphicM, _roomIdSkew));
        seq.Join(TweenSkew(_graphicY, _roomIdSkew));
        seq.Join(TweenSkew(_graphicC, _roomIdSkew));
        seq.Join(TweenSkew(_graphicK, _roomIdSkew));
        seq.Join(TweenPreferredWidth(_layoutM, _roomIdMWidth));
        seq.Join(TweenPreferredWidth(_layoutY, _roomIdYWidth));
        seq.Join(TweenPreferredWidth(_layoutC, _roomIdCWidth));
        seq.Join(_cmykBar.DOAnchorPos(_barRoomIdAnchoredPos, _duration).SetEase(_ease));
        seq.Join(_cmykBar.DOSizeDelta(_barRoomIdSize, _duration).SetEase(_ease));

        // Input field: re-enable editing, swap placeholder, fade content in, resize
        if (_inputField != null)
        {
            _inputField.readOnly = false;
            _inputField.text = string.Empty;
        }
        if (_inputFieldPlaceholderText != null)
        {
            _inputFieldPlaceholderText.text = _roomIdPlaceholder;
            _inputFieldPlaceholderText.color = _inputFieldPlaceholderColor;
        }
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
        StartCoroutine(RoomIdRevealRoutine());
    }

    [ContextMenu("Transition To Waiting")]
    public void TransitionToWaiting()
    {
        DOTween.Kill(this);
        StopAllCoroutines();

        if (_hintCycler != null) _hintCycler.StopCycling();

        var seq = DOTween.Sequence().SetId(this);
        FadeStateDifference(seq, _currentState, MainUIState.Waiting);

        // Decorative lines: move to waiting (top stacked) positions
        if (_decorativeLines != null)
        {
            foreach (var l in _decorativeLines)
            {
                if (l?.rect == null) continue;
                seq.Join(l.rect.DOAnchorPos(l.waitingAnchoredPos, _duration).SetEase(_ease));
                seq.Join(l.rect.DOSizeDelta(l.waitingSizeDelta, _duration).SetEase(_ease));
            }
        }

        // InputField: clear text, fade its current content out, leave its rect alone
        if (_inputField != null)
        {
            _inputField.DeactivateInputField();
            _inputField.readOnly = true;
            _inputField.text = string.Empty;
        }
        if (_inputFieldContentGroup != null)
            seq.Join(_inputFieldContentGroup.DOFade(0f, _fadeOutDuration).SetEase(_ease));

        // Black panel: starts at the InputField footprint, grows up to target size
        if (_waitingPanel != null)
        {
            _waitingPanel.anchoredPosition = _waitingPanelStartAnchoredPos;
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
        ConfigureWaitingPlayerIconsLayout();
        if (_waitingP1Group != null) _waitingP1Group.alpha = 0f;
        if (_waitingP2Group != null) _waitingP2Group.alpha = 0f;

        StartCoroutine(WaitingRevealRoutine());
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
        seq.Append(_loadingScreenRect.DOScaleY(1f, _loadingWipeDuration).SetEase(_loadingWipeEase));
        seq.OnComplete(() =>
        {
            SetLoadingWipeComplete();
            StartCoroutine(AutoPromptAfterLoadingRoutine());
        });
        _currentState = MainUIState.Loading;
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

        EnsurePromptSharedView();
        PreparePromptShowcaseStart();

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
        if (_promptBannedMask != null)
            AddPromptEnterTween(seq, ref hasEnterTween, _promptBannedMask.DOAnchorPosX(GetPromptMaskBannedTargetX(), _promptMaskEnterDuration).SetEase(_promptMaskRevealEase));
        if (_promptTitleText != null)
            AddPromptEnterTween(seq, ref hasEnterTween, _promptTitleText.DOFade(1f, _promptTextFadeDuration).SetEase(_ease));
        if (_promptBannedText != null)
            AddPromptEnterTween(seq, ref hasEnterTween, _promptBannedText.DOFade(1f, _promptTextFadeDuration).SetEase(_ease));

        seq.AppendInterval(_promptHoldBeforeRevealSeconds);
        seq.AppendCallback(SetPromptTextForReveal);
        var hasRevealTween = false;
        if (_promptPromptMask != null)
            AddPromptRevealTween(seq, ref hasRevealTween, _promptPromptMask.DOAnchorPosX(GetPromptMaskMainTargetX() + 5000f, _promptMaskRevealDuration).SetEase(_promptMaskRevealEase));
        if (_promptBannedMask != null)
            AddPromptRevealTween(seq, ref hasRevealTween, _promptBannedMask.DOAnchorPosX(GetPromptMaskBannedTargetX() + 1980f, _promptMaskRevealDuration).SetEase(_promptMaskRevealEase));
        if (_promptTitleText != null)
            AddPromptRevealTween(seq, ref hasRevealTween, _promptTitleText.DOColor(_promptInkColor, _promptMaskRevealDuration).SetEase(_promptMaskRevealEase));
        if (_promptBannedText != null)
            AddPromptRevealTween(seq, ref hasRevealTween, _promptBannedText.DOColor(_promptInkColor, _promptMaskRevealDuration).SetEase(_promptMaskRevealEase));

        seq.OnComplete(() =>
        {
            StartCoroutine(AutoGameplayAfterPromptRoutine());
        });

        _currentState = MainUIState.PromptShowcase;
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

        seq.OnComplete(() =>
        {
            SetStateVisibilityImmediate(MainUIState.Gameplay);
            if (_promptSharedGroup != null)
                _promptSharedGroup.alpha = 1f;
            SetSharedPromptVisibleForGameplay();
            SetGameplayInputFieldVisible();
            SetGameplayPlayerIconsVisible();
            StartGameplayTimerPreview();
            FocusGameplayInputField();
        });

        _currentState = MainUIState.Gameplay;
    }

    void AddPromptToGameplayTween(Sequence seq)
    {
        if (seq == null) return;

        if (_promptTitleText != null)
        {
            _promptTitleText.alignment = TextAlignmentOptions.Left;
            seq.Join(_promptTitleText.rectTransform.DOAnchorPos(GetGameplayPromptPosition(), _gameplayFadeDuration).SetEase(_ease));
            seq.Join(_promptTitleText.rectTransform.DOSizeDelta(new Vector2(1300f, 190f), _gameplayFadeDuration).SetEase(_ease));
            seq.Join(DOTween.To(() => _promptTitleText.fontSize, x => _promptTitleText.fontSize = x, 150f, _gameplayFadeDuration).SetEase(_ease));
        }

        if (_promptBannedText != null)
        {
            _promptBannedText.alignment = TextAlignmentOptions.Left;
            seq.Join(_promptBannedText.rectTransform.DOAnchorPos(GetGameplayBannedLabelPosition(), _gameplayFadeDuration).SetEase(_ease));
            seq.Join(_promptBannedText.rectTransform.DOSizeDelta(new Vector2(1000f, 80f), _gameplayFadeDuration).SetEase(_ease));
            seq.Join(DOTween.To(() => _promptBannedText.fontSize, x => _promptBannedText.fontSize = x, 48f, _gameplayFadeDuration).SetEase(_ease));
        }
    }

    void AddPromptToRoundResultTween(Sequence seq)
    {
        if (seq == null) return;

        if (_promptTitleText != null)
        {
            _promptTitleText.alignment = TextAlignmentOptions.Left;
            _promptTitleText.color = _roundResultTextColor;
            _promptTitleText.text = GetPromptTextWithBannedLetters(_promptText);
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
            _promptBannedText.text = GetBannedLetterRevealText();
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

    void AddRoundResultPlayerIconTween(Sequence seq, CanvasGroup group, Vector2 targetPosition, bool showLocalIndicator)
    {
        if (seq == null || group == null) return;
        if (!(group.transform is RectTransform rect)) return;

        ConfigurePlayerIconBoxForGameplay(rect);
        ConfigurePlayerIconIndicatorForGameplay(rect, showLocalIndicator);
        seq.Join(group.DOFade(1f, _roundResultTransitionDuration).SetEase(_ease));
        seq.Join(rect.DOAnchorPos(targetPosition, _roundResultTransitionDuration).SetEase(_ease));
        seq.Join(rect.DOSizeDelta(new Vector2(100f, 100f), _roundResultTransitionDuration).SetEase(_ease));
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
        DOTween.Kill(this);
        StopAllCoroutines();

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

        var fadeSeq = DOTween.Sequence().SetId(this);
        if (_promptSharedGroup != null)
            fadeSeq.Join(_promptSharedGroup.DOFade(0f, _roundResultFadeGameplayDuration).SetEase(_ease));
        if (_gameplayElementsGroup != null)
            fadeSeq.Join(_gameplayElementsGroup.DOFade(0f, _roundResultFadeGameplayDuration).SetEase(_ease));
        if (_waitingP1Group != null)
            fadeSeq.Join(_waitingP1Group.DOFade(0f, _roundResultFadeGameplayDuration).SetEase(_ease));
        if (_waitingP2Group != null)
            fadeSeq.Join(_waitingP2Group.DOFade(0f, _roundResultFadeGameplayDuration).SetEase(_ease));
        if (_inputFieldContentGroup != null)
            fadeSeq.Join(_inputFieldContentGroup.DOFade(0f, _roundResultFadeGameplayDuration).SetEase(_ease));
        seq.Append(fadeSeq);

        var inputGroup = GetInputFieldStateGroup();
        if (inputGroup != null)
        {
            inputGroup.alpha = 1f;
            inputGroup.interactable = false;
            inputGroup.blocksRaycasts = false;
        }

        if (_inputField != null)
            _inputField.DeactivateInputField();

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
            PrepareRoundResultContentForReveal();
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

    [ContextMenu("Transition To Game End")]
    public void TransitionToGameEnd()
    {
        TransitionToConfiguredState(MainUIState.GameEnd);
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
        FadeStateDifference(seq, _currentState, targetState);

        var animationSet = GetStateAnimationSet(targetState);
        if (animationSet != null)
        {
            AddRectTweens(seq, animationSet.rectTargets);
            AddCanvasGroupTweens(seq, animationSet.canvasGroupTargets);
            ResetTypewriters(animationSet.typewriterTargets);
            StartCoroutine(RevealTypewritersRoutine(animationSet.typewriterTargets));
        }

        if (targetState == MainUIState.Gameplay)
        {
            seq.OnComplete(() =>
            {
                StartGameplayTimerPreview();
                FocusGameplayInputField();
            });
        }
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
            var basePos = _waitingPanel.anchoredPosition;
            var revealedPos = basePos + new Vector2(0f, _waitingPanelRevealAmount);
            var revealedSize = new Vector2(
                _waitingPanelTargetSize.x,
                _waitingPanelTargetSize.y - _waitingPanelRevealAmount);
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

        // 3) P1 + P2 fade in (the only non-typewriter elements)
        if (_waitingP1Group != null)
            _waitingP1Group.DOFade(1f, _waitingContentFadeDuration).SetEase(_ease).SetId(this);
        if (_waitingP2Group != null)
            _waitingP2Group.DOFade(1f, _waitingContentFadeDuration).SetEase(_ease).SetId(this);
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
            _inputFieldPlaceholderText.text = _waitingPlaceholder;
            _inputFieldPlaceholderText.color = _inputFieldPlaceholderColor;
        }
        if (_inputFieldContentGroup != null)
            _inputFieldContentGroup.DOFade(1f, _waitingContentFadeDuration).SetEase(_ease).SetId(this);
        if (_inputField != null)
        {
            _inputField.DeactivateInputField();
            _inputField.enabled = false;
        }
    }

    IEnumerator RoomIdRevealRoutine()
    {
        yield return new WaitForSeconds(_roomIdTitleDelay);
        if (_roomIdTitleGroup != null) _roomIdTitleGroup.alpha = 1f;
        if (_roomIdTitleTypewriter != null)
        {
            _roomIdTitleTypewriter.Play();
            yield return new WaitUntil(() => !_roomIdTitleTypewriter.IsPlaying);
        }
        yield return new WaitForSeconds(_roomIdHintGapAfterTitle);
        if (_roomIdHintGroup != null) _roomIdHintGroup.alpha = 1f;
        if (_roomIdHintTypewriter != null) _roomIdHintTypewriter.Play();
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
        if (_inputFieldRect != null) _inputFieldRect.sizeDelta = _initInputFieldSize;
        if (_inputFieldContentGroup != null) _inputFieldContentGroup.alpha = 1f;

        if (_decorativeLines != null)
        {
            foreach (var l in _decorativeLines)
            {
                if (l?.rect == null) continue;
                l.rect.anchoredPosition = l.initialAnchoredPos;
                l.rect.sizeDelta = l.initialSizeDelta;
            }
        }

        SetStateVisibilityImmediate(MainUIState.Start);

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
        ConfigureWaitingPlayerIconsLayout();
        if (_waitingP1Group != null) _waitingP1Group.alpha = 0f;
        if (_waitingP2Group != null) _waitingP2Group.alpha = 0f;
        if (_loadingScreenRect != null)
            SetLoadingWipeComplete();
        if (_loadingScreenGroup != null)
            _loadingScreenGroup.alpha = 0f;

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

    void PreparePromptShowcaseStart()
    {
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
            _promptTitleText.text = _promptMaskText;
            _promptTitleText.alpha = 0f;
            _promptTitleText.fontSize = 184f;
            _promptTitleText.alignment = TextAlignmentOptions.Left;
            _promptTitleText.rectTransform.anchoredPosition = GetPromptTitlePosition();
            _promptTitleText.rectTransform.sizeDelta = new Vector2(1400f, 260f);
            _promptTitleText.transform.SetAsLastSibling();
        }
        if (_promptBannedText != null)
        {
            _promptBannedText.richText = false;
            _promptBannedText.overrideColorTags = true;
            _promptBannedText.color = _promptMaskBannedTextColor;
            _promptBannedText.text = _promptMaskBannedTextValue;
            _promptBannedText.alpha = 0f;
            _promptBannedText.fontSize = 58f;
            _promptBannedText.alignment = TextAlignmentOptions.Left;
            _promptBannedText.rectTransform.anchoredPosition = GetPromptBannedTextPosition();
            _promptBannedText.rectTransform.sizeDelta = new Vector2(1100f, 100f);
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
            _promptBannedMask.gameObject.SetActive(true);
            _promptBannedMask.anchorMin = new Vector2(0.5f, 0.5f);
            _promptBannedMask.anchorMax = new Vector2(0.5f, 0.5f);
            _promptBannedMask.pivot = new Vector2(0.5f, 0.5f);
            _promptBannedMask.anchoredPosition = new Vector2(GetPromptMaskBannedStartX(), -185f);
            _promptBannedMask.sizeDelta = new Vector2(1980f, 133f);
            _promptBannedMask.localScale = Vector3.one;
            _promptBannedMask.transform.SetSiblingIndex(Mathf.Max(0, _promptSharedGroupRect.childCount - 3));
        }
    }

    float GetPromptMaskMainTargetX() => 1480f;
    float GetPromptMaskMainStartX() => GetPromptMaskMainTargetX() - 5000f;
    float GetPromptMaskBannedTargetX() => -30f;
    float GetPromptMaskBannedStartX() => GetPromptMaskBannedTargetX() - 1980f;
    Vector2 GetPromptTitlePosition() => new Vector2(24f, 65f);
    Vector2 GetPromptBannedTextPosition() => new Vector2(297f, -185f);

    void SetPromptTextForReveal()
    {
        if (_promptTitleText != null)
        {
            _promptTitleText.richText = true;
            _promptTitleText.overrideColorTags = false;
            _promptTitleText.text = GetPromptTextWithBannedLetters(_promptText);
            _promptTitleText.color = _promptMaskTitleColor;
            _promptTitleText.alignment = TextAlignmentOptions.Left;
            _promptTitleText.alpha = 1f;
        }
        if (_promptBannedText != null)
        {
            _promptBannedText.richText = true;
            _promptBannedText.overrideColorTags = false;
            _promptBannedText.text = GetBannedLetterRevealText();
            _promptBannedText.color = _promptMaskBannedTextColor;
            _promptBannedText.alignment = TextAlignmentOptions.Left;
            _promptBannedText.alpha = 1f;
        }
    }

    string GetBannedLetterRevealText()
    {
        if (string.IsNullOrEmpty(_promptBannedLetters))
            return "banned letters";

        var colorHex = ColorUtility.ToHtmlStringRGB(_promptBannedLetterColor);
        var coloredLetters = $"<color=#{colorHex}>{_promptBannedLetters}</color>";
        return _promptBannedLetters.Length == 1
            ? $"banned letter \"{coloredLetters}\""
            : $"banned letters \"{coloredLetters}\"";
    }

    string GetPromptTextWithBannedLetters(string source)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(_promptBannedLetters))
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
        if (string.IsNullOrEmpty(_promptBannedLetters))
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

        current.text = text;
        current.color = _promptInkColor;
        current.fontSize = fontSize;
        current.enableAutoSizing = false;
        current.richText = true;
        current.alignment = alignment;
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

        PrepareGameplayInputFieldStart();
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

        ConfigureRect(_gameplayTimerBar, new Vector2(-900f, -322.5f), new Vector2(1620f, 35f), new Vector2(0f, 0.5f));
        ConfigureRect(_gameplayP1LetterGroup, GetGameplayP1LetterGroupPosition(), new Vector2(520f, 50f), new Vector2(0f, 0.5f));
        ConfigureRect(_gameplayP2LetterGroup, GetGameplayP2LetterGroupPosition(), new Vector2(520f, 50f), new Vector2(0f, 0.5f));

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

        PrepareRoundResultStripeForReveal(_decorativeLines[0]?.rect, new Vector2(-900f, 470f));
        PrepareRoundResultStripeForReveal(_decorativeLines[1]?.rect, new Vector2(-900f, 436f));
        PrepareRoundResultStripeForReveal(_decorativeLines[2]?.rect, new Vector2(-900f, 402f));

        var stripeGroup = GetDecorativeLineStateGroup();
        if (stripeGroup != null)
        {
            stripeGroup.alpha = 1f;
            stripeGroup.interactable = false;
            stripeGroup.blocksRaycasts = false;
        }
    }

    void PrepareRoundResultStripeForReveal(RectTransform rect, Vector2 leftCenterPosition)
    {
        if (rect == null) return;

        ConfigureRect(rect, leftCenterPosition, new Vector2(0f, 20f), new Vector2(0f, 0.5f));
        rect.gameObject.SetActive(true);
        var graphic = rect.GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.color = _gameplayLetterNeutralColor;
            graphic.raycastTarget = false;
        }
    }

    void AddRoundResultStripeRevealTween(Sequence seq)
    {
        if (seq == null || _decorativeLines == null || _decorativeLines.Length < 3) return;

        var stripeSeq = DOTween.Sequence().SetId(this);
        AddRoundResultStripeWidthTween(stripeSeq, _decorativeLines[0]?.rect);
        AddRoundResultStripeWidthTween(stripeSeq, _decorativeLines[1]?.rect);
        AddRoundResultStripeWidthTween(stripeSeq, _decorativeLines[2]?.rect);
        seq.Append(stripeSeq);
    }

    void AddRoundResultStripeWidthTween(Sequence stripeSeq, RectTransform rect)
    {
        if (stripeSeq == null || rect == null) return;

        stripeSeq.Join(rect.DOSizeDelta(new Vector2(1800f, 20f), _roundResultStripeRevealDuration).SetEase(_ease));
    }

    void AddRoundResultContentRevealTween(Sequence seq)
    {
        if (seq == null) return;

        var contentSeq = DOTween.Sequence().SetId(this);
        AddRoundResultFadeTween(contentSeq, _promptTitleText);
        AddRoundResultFadeTween(contentSeq, _promptBannedText);
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

    void ConfigureRoundResultElements()
    {
        ConfigureImageRect(_roundResultPanel, GetRoundResultPanelPosition(), GetRoundResultPanelSize(), _promptInkColor);
        RemoveDeprecatedRoundResultStripeCopies();

        ConfigureRoundResultTopLeftText(_roundResultP1WordText, GetRoundResultWordText(_roundResultP1Word, _roundResultP2Word, _gameplayP1LetterColor), GetRoundResultP1WordTopLeftPosition(), new Vector2(520f, 70f), 49f, _roundResultTextColor);
        ConfigureRoundResultTopLeftText(_roundResultP2WordText, GetRoundResultWordText(_roundResultP2Word, _roundResultP1Word, _gameplayP2LetterColor), GetRoundResultP2WordTopLeftPosition(), new Vector2(520f, 70f), 49f, _roundResultTextColor);
        ConfigureRoundResultTopCenterText(_roundResultDeathLabelText, "death.", GetRoundResultDeathLabelTopCenterPosition(), new Vector2(140f, 60f), 36f, _roundResultMutedTextColor);
        ConfigureRoundResultCenterLeftText(_roundResultP1ScoreText, _roundResultP1Score.ToString(), GetRoundResultP1ScoreTextLeftCenterPosition(), new Vector2(120f, 70f), 49f, _roundResultTextColor);
        ConfigureRoundResultCenterLeftText(_roundResultP2ScoreText, _roundResultP2Score.ToString(), GetRoundResultP2ScoreTextLeftCenterPosition(), new Vector2(120f, 70f), 49f, _roundResultTextColor);

        ConfigureRect(_roundResultP1ScoreBar, GetRoundResultP1ScoreBarPosition(), new Vector2(217f, 45f), new Vector2(0f, 0.5f));
        ConfigureRect(_roundResultP2ScoreBar, GetRoundResultP2ScoreBarPosition(), new Vector2(190f, 45f), new Vector2(0f, 0.5f));
        ConfigureRect(_roundResultDeathLineGroup, GetRoundResultDeathLinePosition(), new Vector2(5f, 338f), new Vector2(0.5f, 0.5f));
        LayoutRoundResultDeathLineSegments();
        SetRoundResultSiblingOrder();
    }

    void ConfigureRoundResultStripes()
    {
        if (_decorativeLines == null || _decorativeLines.Length < 3) return;

        ConfigureRoundResultStripe(_decorativeLines[0]?.rect, new Vector2(0f, 470f));
        ConfigureRoundResultStripe(_decorativeLines[1]?.rect, new Vector2(0f, 436f));
        ConfigureRoundResultStripe(_decorativeLines[2]?.rect, new Vector2(0f, 402f));

        var stripeGroup = GetDecorativeLineStateGroup();
        if (stripeGroup != null)
        {
            stripeGroup.interactable = false;
            stripeGroup.blocksRaycasts = false;
        }
    }

    void ConfigureRoundResultStripe(RectTransform rect, Vector2 position)
    {
        if (rect == null) return;

        ConfigureRect(rect, position, new Vector2(1800f, 20f), new Vector2(0.5f, 0.5f));
        rect.gameObject.SetActive(true);
        var graphic = rect.GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.color = _gameplayLetterNeutralColor;
            graphic.raycastTarget = false;
        }
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
        text.text = value;
        text.color = color;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.alpha = 1f;
        text.raycastTarget = false;
        ConfigureRect(text.rectTransform, position, text.rectTransform.sizeDelta, new Vector2(0.5f, 0.5f));
    }

    void ConfigureRoundResultTopLeftText(TMP_Text text, string value, Vector2 topLeftPosition, Vector2 size, float fontSize, Color color)
    {
        if (text == null) return;

        text.richText = true;
        text.overrideColorTags = false;
        text.text = value;
        text.color = color;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.alpha = 1f;
        text.raycastTarget = false;
        ConfigureTopLeftRect(text.rectTransform, topLeftPosition, size);
    }

    void ConfigureRoundResultTopCenterText(TMP_Text text, string value, Vector2 topCenterPosition, Vector2 size, float fontSize, Color color)
    {
        if (text == null) return;

        text.richText = true;
        text.overrideColorTags = false;
        text.text = value;
        text.color = color;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Top;
        text.alpha = 1f;
        text.raycastTarget = false;
        ConfigureTopCenterRect(text.rectTransform, topCenterPosition, size);
    }

    void ConfigureRoundResultCenterLeftText(TMP_Text text, string value, Vector2 leftCenterPosition, Vector2 size, float fontSize, Color color)
    {
        if (text == null) return;

        text.richText = true;
        text.overrideColorTags = false;
        text.text = value;
        text.color = color;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Left;
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

    string GetRoundResultWordText(string word, string opposingWord, Color advantageColor)
    {
        if (string.IsNullOrEmpty(word)) return string.Empty;

        var colorHex = ColorUtility.ToHtmlStringRGB(advantageColor);
        var ownLetterCount = CountLetters(word);
        var opposingLetterCount = CountLetters(opposingWord);
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
        text.color = color;
        text.text = value;
        text.alpha = 0f;
        text.rectTransform.anchoredPosition = targetPosition - new Vector2(0f, _gameplaySlideOffset);
    }

    Vector2 GetGameplayPromptPosition() => new Vector2(-250f, 330f);
    Vector2 GetGameplayBannedLabelPosition() => new Vector2(-392f, 230f);
    Vector2 GetGameplayInputFieldPosition() => new Vector2(0f, -420f);
    Vector2 GetGameplayInputFieldSize() => new Vector2(1800f, 120f);
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
    Vector2 GetRoundResultP1ScoreTextLeftCenterPosition() => new Vector2(-95f, -100f);
    Vector2 GetRoundResultP2ScoreTextLeftCenterPosition() => new Vector2(-122f, -295f);
    Vector2 GetRoundResultPanelPosition() => new Vector2(0f, -52f);
    Vector2 GetRoundResultPanelSize() => new Vector2(1800f, 856f);

    void SetSharedPromptVisibleForGameplay()
    {
        SetOptionalGameObjectActive(_promptSharedBackground, false);
        if (_promptPromptMask != null) _promptPromptMask.gameObject.SetActive(false);
        if (_promptBannedMask != null) _promptBannedMask.gameObject.SetActive(false);

        if (_promptTitleText != null)
        {
            _promptTitleText.alignment = TextAlignmentOptions.Left;
            _promptTitleText.color = _promptInkColor;
            _promptTitleText.text = GetPromptTextWithBannedLetters(_promptText);
            _promptTitleText.fontSize = 150f;
            _promptTitleText.rectTransform.sizeDelta = new Vector2(1300f, 190f);
            _promptTitleText.alpha = 1f;
            _promptTitleText.rectTransform.anchoredPosition = GetGameplayPromptPosition();
        }
        if (_promptBannedText != null)
        {
            _promptBannedText.richText = true;
            _promptBannedText.overrideColorTags = false;
            _promptBannedText.alignment = TextAlignmentOptions.Left;
            _promptBannedText.color = _promptInkColor;
            _promptBannedText.text = GetBannedLetterRevealText();
            _promptBannedText.fontSize = 48f;
            _promptBannedText.rectTransform.sizeDelta = new Vector2(1000f, 80f);
            _promptBannedText.alpha = 1f;
            _promptBannedText.rectTransform.anchoredPosition = GetGameplayBannedLabelPosition();
        }

        SetGameplayInputFieldVisible();
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
            _promptTitleText.text = GetPromptTextWithBannedLetters(_promptText);
            _promptTitleText.fontSize = 170f;
            _promptTitleText.alpha = 1f;
            ConfigureTopLeftRect(_promptTitleText.rectTransform, GetRoundResultPromptTopLeftPosition(), new Vector2(1400f, 220f));
        }

        if (_promptBannedText != null)
        {
            _promptBannedText.richText = true;
            _promptBannedText.overrideColorTags = false;
            _promptBannedText.alignment = TextAlignmentOptions.TopLeft;
            _promptBannedText.color = _roundResultTextColor;
            _promptBannedText.text = GetBannedLetterRevealText();
            _promptBannedText.fontSize = 59f;
            _promptBannedText.alpha = 1f;
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

    void SetGameplayInputFieldVisible()
    {
        if (_inputFieldRect != null)
        {
            ConfigureRect(_inputFieldRect, GetGameplayInputFieldPosition(), GetGameplayInputFieldSize(), new Vector2(0.5f, 0.5f));
            SetGameplayInputFieldSiblingOrder();
        }

        ConfigureGameplayInputFieldContent(1f);
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

    void ConfigureGameplayInputFieldContent(float alpha)
    {
        if (_inputField != null)
        {
            _inputField.enabled = true;
            _inputField.interactable = true;
            _inputField.transition = Selectable.Transition.None;
            _inputField.readOnly = false;
            _inputField.SetTextWithoutNotify(string.Empty);
            if (_inputField.targetGraphic != null)
                _inputField.targetGraphic.color = _promptInkColor;
        }
        if (_inputFieldPlaceholderText != null)
        {
            _inputFieldPlaceholderText.text = _gameplayInputPlaceholder;
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

        _inputField.enabled = true;
        _inputField.interactable = true;
        _inputField.readOnly = false;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_inputField.gameObject);

        _inputField.Select();
        _inputField.ActivateInputField();
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

    void ConfigureGameplayPlayerIcon(CanvasGroup group, Vector2 anchoredPosition, bool showLocalIndicator)
    {
        if (group == null || !(group.transform is RectTransform rect)) return;

        group.alpha = 1f;
        group.interactable = false;
        group.blocksRaycasts = false;
        ConfigureRect(rect, anchoredPosition, new Vector2(100f, 100f), new Vector2(0.5f, 0.5f));
        ConfigurePlayerIconBoxForGameplay(rect);
        ConfigurePlayerIconIndicatorForGameplay(rect, showLocalIndicator);
        rect.SetAsLastSibling();
    }

    void ConfigureRoundResultPlayerIcon(CanvasGroup group, Vector2 anchoredPosition, bool showLocalIndicator)
    {
        if (group == null || !(group.transform is RectTransform rect)) return;

        group.alpha = 1f;
        group.interactable = false;
        group.blocksRaycasts = false;
        ConfigureRect(rect, anchoredPosition, new Vector2(100f, 100f), new Vector2(0.5f, 0.5f));
        ConfigurePlayerIconBoxForGameplay(rect);
        ConfigurePlayerIconIndicatorForRoundResult(rect, showLocalIndicator);
        rect.SetAsLastSibling();
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
            ConfigureRect(indicator, new Vector2(-70f, 0f), new Vector2(24f, 19f), new Vector2(0.5f, 0.5f));
        }
        if (triangle != null)
        {
            triangle.gameObject.SetActive(showLocalIndicator);
            ConfigureRect(triangle, Vector2.zero, new Vector2(24f, 19f), new Vector2(0.5f, 0.5f));
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
            ConfigureRect(indicator, new Vector2(-70f, 0f), new Vector2(24f, 19f), new Vector2(0.5f, 0.5f));
        }
        if (triangle != null)
        {
            triangle.gameObject.SetActive(showLocalIndicator);
            ConfigureRect(triangle, Vector2.zero, new Vector2(24f, 19f), new Vector2(0.5f, 0.5f));
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
            idText.fontSize = 51f;
            idText.characterSpacing = 0f;
            idText.alignment = TextAlignmentOptions.Center;
            idText.margin = Vector4.zero;
            idText.text = playerIconRoot.name.Contains("2") ? "P2" : "P1";
            if (idText.rectTransform != null)
                StretchToParent(idText.rectTransform);
        }
    }

    void ConfigureWaitingPlayerIconsLayout()
    {
        ConfigureWaitingPlayerIcon(_waitingP1Group, new Vector2(-687.54f, -122.601395f), true);
        ConfigureWaitingPlayerIcon(_waitingP2Group, new Vector2(-399.5f, -122.601395f), false);
    }

    void ConfigureWaitingPlayerIcon(CanvasGroup group, Vector2 anchoredPosition, bool showLocalIndicator)
    {
        if (group == null || !(group.transform is RectTransform rect)) return;

        ConfigureRect(rect, anchoredPosition, new Vector2(220.1179f, 214.7207f), new Vector2(0.5f, 0.5f));
        ConfigurePlayerIconBoxForWaiting(rect);
        ConfigurePlayerIconIndicatorForWaiting(rect, showLocalIndicator);
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
            ConfigureRect(indicator, new Vector2(0f, -148f), new Vector2(93.0246f, 90f), new Vector2(0.5f, 0.5f));
        }
        if (triangle != null)
        {
            triangle.gameObject.SetActive(showLocalIndicator);
            ConfigureRect(triangle, new Vector2(0f, -8f), new Vector2(27.0481f, 19.1138f), new Vector2(0.5f, 0.5f));
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
            ConfigureRect(youText, new Vector2(1.9196f, -32f), new Vector2(71f, 48.8f), new Vector2(0.5f, 0.5f));
        }
    }

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
            RefreshGameplayLetterBlocks();
    }

    void RefreshGameplayLetterBlocks()
    {
        var p1Count = CountLetters(_gameplayP1Word);
        var p2Count = CountLetters(_gameplayP2Word);

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

        return result.ToArray();
    }

    void FadeStateDifference(Sequence seq, MainUIState from, MainUIState to)
    {
        var fromGroups = GetVisibleGroups(from);
        var toGroups = GetVisibleGroups(to);

        foreach (var cg in fromGroups)
        {
            if (cg == null || ContainsGroup(toGroups, cg)) continue;
            seq.Join(cg.DOFade(0f, _fadeOutDuration).SetEase(_ease));
        }

        foreach (var cg in toGroups)
        {
            if (cg == null || ContainsGroup(fromGroups, cg)) continue;

            cg.alpha = 0f;
            if (IsManuallyRevealed(to, cg)) continue;

            seq.Join(cg.DOFade(1f, _fadeOutDuration).SetEase(_ease));
        }

        _currentState = to;
    }

    void SetStateVisibilityImmediate(MainUIState state)
    {
        foreach (var cg in GetAllStateGroups())
        {
            if (cg == null) continue;
            cg.alpha = ContainsGroup(GetVisibleGroups(state), cg) && !IsManuallyRevealed(state, cg) ? 1f : 0f;
        }

        _currentState = state;
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
        }
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
        return _inputFieldRect != null ? _inputFieldRect.GetComponent<CanvasGroup>() : null;
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
            return ContainsGroup(groupSet.manuallyRevealedGroups, cg);
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
