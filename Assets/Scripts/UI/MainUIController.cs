using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class MainUIController : MonoBehaviour
{
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

    Vector2 _initBarPos, _initBarSize;
    float _initMWidth, _initYWidth, _initCWidth;
    float _initSkew;

    void Awake()
    {
        _initBarPos = _cmykBar.anchoredPosition;
        _initBarSize = _cmykBar.sizeDelta;
        _initMWidth = _layoutM.preferredWidth;
        _initYWidth = _layoutY.preferredWidth;
        _initCWidth = _layoutC.preferredWidth;
        _initSkew = _graphicM.Skew;
    }

    [ContextMenu("Transition To Tutorial")]
    public void TransitionToTutorial()
    {
        DOTween.Kill(this);

        var seq = DOTween.Sequence().SetId(this);

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
    }

    [ContextMenu("Reset To Start")]
    public void ResetToStart()
    {
        DOTween.Kill(this);

        _cmykBar.anchoredPosition = _initBarPos;
        _cmykBar.sizeDelta = _initBarSize;
        _layoutM.preferredWidth = _initMWidth;
        _layoutY.preferredWidth = _initYWidth;
        _layoutC.preferredWidth = _initCWidth;
        _graphicM.Skew = _initSkew;
        _graphicY.Skew = _initSkew;
        _graphicC.Skew = _initSkew;
        _graphicK.Skew = _initSkew;
    }

    [ContextMenu("Capture Current As Tutorial Target")]
    void CaptureCurrentAsTutorialTarget()
    {
        _barTutorialAnchoredPos = _cmykBar.anchoredPosition;
        _barTutorialSize = _cmykBar.sizeDelta;
        Debug.Log($"Captured: pos={_barTutorialAnchoredPos}, size={_barTutorialSize}");
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
