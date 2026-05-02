using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
[AddComponentMenu("UI/Box Frame")]
public class BoxFrameGraphic : MaskableGraphic
{
    [SerializeField, Min(0f)] float _thickness = 4f;
    [SerializeField] bool _insetFromEdge = true;

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

        var v = UIVertex.simpleVert;
        v.color = color;

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
        vh.AddTriangle(0, 1, 5);
        vh.AddTriangle(5, 4, 0);
        // Top side
        vh.AddTriangle(1, 2, 6);
        vh.AddTriangle(6, 5, 1);
        // Right side
        vh.AddTriangle(2, 3, 7);
        vh.AddTriangle(7, 6, 2);
        // Bottom side
        vh.AddTriangle(3, 0, 4);
        vh.AddTriangle(4, 7, 3);
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
