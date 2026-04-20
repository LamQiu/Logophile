using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainUIController : MonoBehaviour
{
    [System.Serializable]
    public class LineTarget
    {
        public RectTransform rect;
        public Vector2 tutorialAnchoredPos;
        public Vector2 tutorialSizeDelta;
        [HideInInspector] public Vector2 initialAnchoredPos;
        [HideInInspector] public Vector2 initialSizeDelta;
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

    [Header("Tutorial Transition - Fade Out")]
    [SerializeField] CanvasGroup[] _startFadeOutGroups;
    [SerializeField] float _fadeOutDuration = 0.4f;

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

        // Fade out Starting Screen elements
        if (_startFadeOutGroups != null)
        {
            foreach (var cg in _startFadeOutGroups)
            {
                if (cg == null) continue;
                seq.Join(cg.DOFade(0f, _fadeOutDuration).SetEase(_ease));
            }
        }

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

        if (_inputField != null) _inputField.readOnly = false;
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

        if (_startFadeOutGroups != null)
            foreach (var cg in _startFadeOutGroups) if (cg != null) cg.alpha = 1f;
        if (_tutorialTitleGroup != null) _tutorialTitleGroup.alpha = 0f;
        if (_tutorialTitleTypewriter != null) _tutorialTitleTypewriter.Hide();
        if (_pressSpaceGroup != null) _pressSpaceGroup.alpha = 0f;
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

    Tween TweenSkew(ParallelogramGraphic g, float target)
    {
        return DOTween.To(() => g.Skew, x => g.Skew = x, target, _duration).SetEase(_ease);
    }

    Tween TweenPreferredWidth(LayoutElement le, float target)
    {
        return DOTween.To(() => le.preferredWidth, x => le.preferredWidth = x, target, _duration).SetEase(_ease);
    }
}
