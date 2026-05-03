using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainUIController : MonoBehaviour
{
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

    [Header("State Visibility")]
    [SerializeField] StateCanvasGroupSet[] _stateGroups;
    [SerializeField] MainUIState _currentState = MainUIState.Start;
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
        if (!_initialCaptured) CaptureInitialState();
        SetStateVisibilityImmediate(_currentState);
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
        if (_playIntroOnStart) PlayIntro();
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

        // Skew → 0 (parallelogram → rectangle)
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
            _inputFieldPlaceholderText.text = _roomIdPlaceholder;
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
        seq.OnComplete(SetLoadingWipeComplete);
        _currentState = MainUIState.Loading;
    }

    void PrepareLoadingWipeStart()
    {
        var parentSize = GetParentRectSize(_loadingScreenRect);

        _loadingScreenRect.anchorMin = new Vector2(0.5f, 0f);
        _loadingScreenRect.anchorMax = new Vector2(0.5f, 0f);
        _loadingScreenRect.pivot = new Vector2(0.5f, 0f);
        _loadingScreenRect.anchoredPosition = new Vector2(0f, -8f);
        _loadingScreenRect.sizeDelta = new Vector2(parentSize.x + 64f, parentSize.y + 16f);
        _loadingScreenRect.localScale = new Vector3(1f, 0f, 1f);

        if (_loadingScreenImage != null)
        {
            _loadingScreenImage.type = Image.Type.Simple;
            _loadingScreenImage.fillAmount = 1f;
        }
    }

    void SetLoadingWipeComplete()
    {
        if (_loadingScreenRect == null) return;

        var parentSize = GetParentRectSize(_loadingScreenRect);

        _loadingScreenRect.anchorMin = new Vector2(0.5f, 0f);
        _loadingScreenRect.anchorMax = new Vector2(0.5f, 0f);
        _loadingScreenRect.pivot = new Vector2(0.5f, 0f);
        _loadingScreenRect.anchoredPosition = new Vector2(0f, -8f);
        _loadingScreenRect.sizeDelta = new Vector2(parentSize.x + 64f, parentSize.y + 16f);
        _loadingScreenRect.localScale = Vector3.one;
        if (_loadingScreenImage != null)
        {
            _loadingScreenImage.type = Image.Type.Simple;
            _loadingScreenImage.fillAmount = 1f;
        }
        if (_loadingScreenGroup != null)
            _loadingScreenGroup.alpha = 1f;
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
        TransitionToConfiguredState(MainUIState.PromptShowcase);
    }

    [ContextMenu("Transition To Gameplay")]
    public void TransitionToGameplay()
    {
        TransitionToConfiguredState(MainUIState.Gameplay);
    }

    [ContextMenu("Transition To Round Result")]
    public void TransitionToRoundResult()
    {
        TransitionToConfiguredState(MainUIState.RoundResult);
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

        // Typewriter sequence: title → room id → hint, with P1/P2 fading in mid-sequence
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

        // Show placeholder ("ready") in the bottom strip but disable typing —
        // "ready" is captured elsewhere (key listener), the InputField is now
        // purely a visual element.
        if (_inputFieldPlaceholderText != null)
            _inputFieldPlaceholderText.text = _waitingPlaceholder;
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
        if (_waitingP1Group != null) _waitingP1Group.alpha = 0f;
        if (_waitingP2Group != null) _waitingP2Group.alpha = 0f;
        if (_loadingScreenRect != null)
            SetLoadingWipeComplete();
        if (_loadingScreenGroup != null)
            _loadingScreenGroup.alpha = 0f;
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
        if (_stateGroups == null) return new CanvasGroup[0];

        foreach (var groupSet in _stateGroups)
        {
            if (groupSet != null && groupSet.state == state)
                return groupSet.visibleGroups ?? new CanvasGroup[0];
        }

        return new CanvasGroup[0];
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

    List<CanvasGroup> GetAllStateGroups()
    {
        var result = new List<CanvasGroup>();
        if (_stateGroups == null) return result;

        foreach (var groupSet in _stateGroups)
        {
            if (groupSet?.visibleGroups == null) continue;

            foreach (var cg in groupSet.visibleGroups)
            {
                if (cg != null && !result.Contains(cg))
                    result.Add(cg);
            }
        }

        return result;
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
