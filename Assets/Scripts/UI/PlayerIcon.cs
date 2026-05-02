using TMPro;
using UnityEngine;

[AddComponentMenu("Logophile UI/Player Icon")]
public class PlayerIcon : MonoBehaviour
{
    public enum Slot { P1, P2 }

    [SerializeField] Slot _slot = Slot.P1;
    [SerializeField] bool _isLocal;
    [SerializeField] BoxFrameGraphic _box;
    [SerializeField] TMP_Text _idText;
    [SerializeField] GameObject _youIndicator;
    [SerializeField] Color _p1Color = new Color(1f, 0.92f, 0.16f);   // yellow
    [SerializeField] Color _p2Color = new Color(0.32f, 0.74f, 1f);   // blue

    public Slot CurrentSlot
    {
        get => _slot;
        set
        {
            if (_slot == value) return;
            _slot = value;
            Apply();
        }
    }

    public bool IsLocal
    {
        get => _isLocal;
        set
        {
            if (_isLocal == value) return;
            _isLocal = value;
            Apply();
        }
    }

    void Awake() => Apply();

    void Apply()
    {
        var c = _slot == Slot.P1 ? _p1Color : _p2Color;
        var label = _slot == Slot.P1 ? "P1" : "P2";
        if (_box != null) _box.color = c;
        if (_idText != null)
        {
            _idText.text = label;
            _idText.color = c;
        }
        if (_youIndicator != null) _youIndicator.SetActive(_isLocal);
    }

#if UNITY_EDITOR
    void OnValidate() => Apply();

    [ContextMenu("Auto-Wire Children")]
    void AutoWireChildren()
    {
        if (_box == null) _box = GetComponentInChildren<BoxFrameGraphic>(true);
        if (_idText == null) _idText = GetComponentInChildren<TMP_Text>(true);
        Apply();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
