using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
[AddComponentMenu("Logophile UI/Box Frame")]
public class BoxFrameGraphic : MaskableGraphic
{
    [SerializeField, Min(0f)] float _thickness = 4f;
    [SerializeField] bool _insetFromEdge = true;
    [SerializeField] Color _fillColor = Color.clear;

    public float Thickness
    {
        get => _thickness;
        set
        {
            if (Mathf.Approximately(_thickness, value)) return;
            _thickness = Mathf.Max(0f, value);
            SetVerticesDirty();
        }
    }

    public bool InsetFromEdge
    {
        get => _insetFromEdge;
        set
        {
            if (_insetFromEdge == value) return;
            _insetFromEdge = value;
            SetVerticesDirty();
        }
    }

    public Color FillColor
    {
        get => _fillColor;
        set
        {
            if (_fillColor == value) return;
            _fillColor = value;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = GetPixelAdjustedRect();
        if (r.width <= 0f || r.height <= 0f || _thickness <= 0f) return;

        // Clamp thickness so the inner rect never inverts.
        float maxT = 0.5f * Mathf.Min(r.width, r.height);
        float t = Mathf.Min(_thickness, maxT);

        Rect outer, inner;
        if (_insetFromEdge)
        {
            // Outer = the RectTransform; inner is shrunk by thickness on every side.
            outer = r;
            inner = new Rect(r.xMin + t, r.yMin + t, r.width - 2f * t, r.height - 2f * t);
        }
        else
        {
            // Outer is grown by thickness; the RectTransform is the inner edge.
            outer = new Rect(r.xMin - t, r.yMin - t, r.width + 2f * t, r.height + 2f * t);
            inner = r;
        }

        if (_fillColor.a > 0f)
            AddQuad(vh, inner, _fillColor);

        var v = UIVertex.simpleVert;
        v.color = color;
        var offset = vh.currentVertCount;

        // 0..3 outer corners (BL, TL, TR, BR), 4..7 inner corners
        v.position = new Vector3(outer.xMin, outer.yMin); vh.AddVert(v);
        v.position = new Vector3(outer.xMin, outer.yMax); vh.AddVert(v);
        v.position = new Vector3(outer.xMax, outer.yMax); vh.AddVert(v);
        v.position = new Vector3(outer.xMax, outer.yMin); vh.AddVert(v);
        v.position = new Vector3(inner.xMin, inner.yMin); vh.AddVert(v);
        v.position = new Vector3(inner.xMin, inner.yMax); vh.AddVert(v);
        v.position = new Vector3(inner.xMax, inner.yMax); vh.AddVert(v);
        v.position = new Vector3(inner.xMax, inner.yMin); vh.AddVert(v);

        // Left side
        vh.AddTriangle(offset + 0, offset + 1, offset + 5);
        vh.AddTriangle(offset + 5, offset + 4, offset + 0);
        // Top side
        vh.AddTriangle(offset + 1, offset + 2, offset + 6);
        vh.AddTriangle(offset + 6, offset + 5, offset + 1);
        // Right side
        vh.AddTriangle(offset + 2, offset + 3, offset + 7);
        vh.AddTriangle(offset + 7, offset + 6, offset + 2);
        // Bottom side
        vh.AddTriangle(offset + 3, offset + 0, offset + 4);
        vh.AddTriangle(offset + 4, offset + 7, offset + 3);
    }

    static void AddQuad(VertexHelper vh, Rect r, Color color)
    {
        var offset = vh.currentVertCount;
        var v = UIVertex.simpleVert;
        v.color = color;

        v.position = new Vector3(r.xMin, r.yMin); vh.AddVert(v);
        v.position = new Vector3(r.xMin, r.yMax); vh.AddVert(v);
        v.position = new Vector3(r.xMax, r.yMax); vh.AddVert(v);
        v.position = new Vector3(r.xMax, r.yMin); vh.AddVert(v);

        vh.AddTriangle(offset, offset + 1, offset + 2);
        vh.AddTriangle(offset + 2, offset + 3, offset);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        _thickness = Mathf.Max(0f, _thickness);
        SetVerticesDirty();
    }
#endif
}
