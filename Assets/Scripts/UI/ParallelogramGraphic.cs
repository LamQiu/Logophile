using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
[AddComponentMenu("Logophile UI/Parallelogram")]
public class ParallelogramGraphic : MaskableGraphic
{
    // Horizontal offset of the top edge relative to the bottom, in local pixels.
    // Positive = top slides right (parallelogram leans right).
    [SerializeField] float _skew = 60f;
    [SerializeField] bool _snapNearFortyFiveDegrees = true;
    [SerializeField, Min(0f)] float _snapTolerance = 6f;
    [SerializeField, Min(0f)] float _cornerTrim;

    public float Skew
    {
        get => _skew;
        set
        {
            if (Mathf.Approximately(_skew, value)) return;
            _skew = value;
            SetVerticesDirty();
        }
    }

    public float CornerTrim
    {
        get => _cornerTrim;
        set
        {
            value = Mathf.Max(0f, value);
            if (Mathf.Approximately(_cornerTrim, value)) return;
            _cornerTrim = value;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = GetPixelAdjustedRect();
        var skew = GetRenderSkew(r);
        var bottomLeft = new Vector2(r.xMin, r.yMin);
        var topLeft = new Vector2(r.xMin + skew, r.yMax);
        var topRight = new Vector2(r.xMax + skew, r.yMax);
        var bottomRight = new Vector2(r.xMax, r.yMin);

        if (_cornerTrim > 0f)
            TrimAcuteCorners(ref bottomLeft, ref topLeft, ref topRight, ref bottomRight, _cornerTrim);

        AddSolidParallelogram(vh, bottomLeft, topLeft, topRight, bottomRight, color);
    }

    float GetRenderSkew(Rect r)
    {
        if (!_snapNearFortyFiveDegrees)
            return _skew;

        var height = Mathf.Abs(r.height);
        var skewAbs = Mathf.Abs(_skew);
        if (height <= Mathf.Epsilon || skewAbs <= Mathf.Epsilon)
            return _skew;

        if (Mathf.Abs(skewAbs - height) > _snapTolerance)
            return _skew;

        return Mathf.Sign(_skew) * Mathf.Round(height);
    }

    static void AddSolidParallelogram(
        VertexHelper vh,
        Vector2 bottomLeft,
        Vector2 topLeft,
        Vector2 topRight,
        Vector2 bottomRight,
        Color color)
    {
        var offset = vh.currentVertCount;
        AddVertex(vh, bottomLeft, color);
        AddVertex(vh, topLeft, color);
        AddVertex(vh, topRight, color);
        AddVertex(vh, bottomRight, color);

        vh.AddTriangle(offset, offset + 1, offset + 2);
        vh.AddTriangle(offset + 2, offset + 3, offset);
    }

    static void TrimAcuteCorners(
        ref Vector2 bottomLeft,
        ref Vector2 topLeft,
        ref Vector2 topRight,
        ref Vector2 bottomRight,
        float trim)
    {
        // Trim only the two acute diagonal/horizontal corners. This removes the one-pixel
        // "tooth" visible when Game view is zoomed without introducing transparent seams
        // between adjacent differently colored stripes.
        if (topLeft.x < bottomLeft.x)
            topLeft.x += trim;
        else if (topLeft.x > bottomLeft.x)
            bottomLeft.x += trim;

        if (topRight.x < bottomRight.x)
            bottomRight.x -= trim;
        else if (topRight.x > bottomRight.x)
            topRight.x -= trim;
    }

    static void AddVertex(VertexHelper vh, Vector2 position, Color color)
    {
        var v = UIVertex.simpleVert;
        v.color = color;
        v.position = position;
        vh.AddVert(v);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif
}
