using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
[AddComponentMenu("UI/Parallelogram")]
public class ParallelogramGraphic : MaskableGraphic
{
    // Horizontal offset of the top edge relative to the bottom, in local pixels.
    // Positive = top slides right (parallelogram leans right).
    [SerializeField] float _skew = 60f;

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

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = GetPixelAdjustedRect();

        var v = UIVertex.simpleVert;
        v.color = color;

        v.position = new Vector3(r.xMin,          r.yMin); vh.AddVert(v); // bottom-left
        v.position = new Vector3(r.xMin + _skew,  r.yMax); vh.AddVert(v); // top-left
        v.position = new Vector3(r.xMax + _skew,  r.yMax); vh.AddVert(v); // top-right
        v.position = new Vector3(r.xMax,          r.yMin); vh.AddVert(v); // bottom-right

        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif
}
