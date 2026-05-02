using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
[AddComponentMenu("Logophile UI/Triangle")]
public class TriangleGraphic : MaskableGraphic
{
    public enum Direction { Up, Down, Left, Right }

    [SerializeField] Direction _direction = Direction.Up;

    public Direction PointingDirection
    {
        get => _direction;
        set
        {
            if (_direction == value) return;
            _direction = value;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = GetPixelAdjustedRect();
        if (r.width <= 0f || r.height <= 0f) return;

        var v = UIVertex.simpleVert;
        v.color = color;

        // Three corners depending on which side the apex points to.
        Vector2 a, b, c;
        switch (_direction)
        {
            case Direction.Down:
                a = new Vector2(r.xMin, r.yMax);                  // top-left
                b = new Vector2(r.xMax, r.yMax);                  // top-right
                c = new Vector2((r.xMin + r.xMax) * 0.5f, r.yMin); // bottom apex
                break;
            case Direction.Left:
                a = new Vector2(r.xMax, r.yMin);                  // bottom-right
                b = new Vector2(r.xMax, r.yMax);                  // top-right
                c = new Vector2(r.xMin, (r.yMin + r.yMax) * 0.5f); // left apex
                break;
            case Direction.Right:
                a = new Vector2(r.xMin, r.yMax);                  // top-left
                b = new Vector2(r.xMin, r.yMin);                  // bottom-left
                c = new Vector2(r.xMax, (r.yMin + r.yMax) * 0.5f); // right apex
                break;
            case Direction.Up:
            default:
                a = new Vector2(r.xMin, r.yMin);                  // bottom-left
                b = new Vector2(r.xMax, r.yMin);                  // bottom-right
                c = new Vector2((r.xMin + r.xMax) * 0.5f, r.yMax); // top apex
                break;
        }

        v.position = a; vh.AddVert(v);
        v.position = b; vh.AddVert(v);
        v.position = c; vh.AddVert(v);

        vh.AddTriangle(0, 1, 2);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif
}
