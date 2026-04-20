using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
[AddComponentMenu("UI/Hint Cycler")]
public class HintCycler : MonoBehaviour
{
    [SerializeField] TypewriterEffect _typewriter;
    [TextArea] [SerializeField] string[] _hints;
    [SerializeField] float _holdSeconds = 4f;

    TMP_Text _text;
    Coroutine _running;
    int _lastIndex = -1;

    void Awake()
    {
        _text = GetComponent<TMP_Text>();
        if (_typewriter == null) _typewriter = GetComponent<TypewriterEffect>();
    }

    public void StartCycling()
    {
        StopCycling();
        _running = StartCoroutine(CycleRoutine());
    }

    public void StopCycling()
    {
        if (_running != null) StopCoroutine(_running);
        _running = null;
    }

    IEnumerator CycleRoutine()
    {
        while (true)
        {
            // let any in-progress typing finish first
            yield return new WaitUntil(() => !_typewriter.IsPlaying);
            yield return new WaitForSeconds(_holdSeconds);

            _typewriter.Erase();
            yield return new WaitUntil(() => !_typewriter.IsPlaying);

            _text.text = PickRandomHint();
            _typewriter.Play();
        }
    }

    string PickRandomHint()
    {
        if (_hints == null || _hints.Length == 0) return "";
        if (_hints.Length == 1) return _hints[0];

        int idx;
        do { idx = Random.Range(0, _hints.Length); } while (idx == _lastIndex);
        _lastIndex = idx;
        return _hints[idx];
    }
}
