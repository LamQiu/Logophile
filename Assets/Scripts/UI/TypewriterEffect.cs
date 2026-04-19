using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
[AddComponentMenu("UI/Typewriter Effect")]
public class TypewriterEffect : MonoBehaviour
{
    [SerializeField] float _charactersPerSecond = 30f;
    [SerializeField] bool _hideOnAwake = true;
    [SerializeField] bool _playOnEnable = false;

    TMP_Text _text;
    Coroutine _running;

    public bool IsPlaying => _running != null;

    void Awake()
    {
        _text = GetComponent<TMP_Text>();
        if (_hideOnAwake) _text.maxVisibleCharacters = 0;
    }

    void OnEnable()
    {
        if (_playOnEnable) Play();
    }

    public void Play()
    {
        Stop();
        _running = StartCoroutine(Run());
    }

    public void Erase()
    {
        Stop();
        _running = StartCoroutine(RunErase());
    }

    public void Stop()
    {
        if (_running != null) StopCoroutine(_running);
        _running = null;
    }

    public void ShowAll()
    {
        Stop();
        _text.ForceMeshUpdate();
        _text.maxVisibleCharacters = _text.textInfo.characterCount;
    }

    public void Hide()
    {
        Stop();
        _text.maxVisibleCharacters = 0;
    }

    IEnumerator Run()
    {
        _text.ForceMeshUpdate();
        int total = _text.textInfo.characterCount;
        _text.maxVisibleCharacters = 0;

        float secondsPerChar = 1f / Mathf.Max(0.01f, _charactersPerSecond);
        for (int i = 1; i <= total; i++)
        {
            _text.maxVisibleCharacters = i;
            yield return new WaitForSeconds(secondsPerChar);
        }
        _running = null;
    }

    IEnumerator RunErase()
    {
        int start = _text.maxVisibleCharacters;
        float secondsPerChar = 1f / Mathf.Max(0.01f, _charactersPerSecond);
        for (int i = start - 1; i >= 0; i--)
        {
            _text.maxVisibleCharacters = i;
            yield return new WaitForSeconds(secondsPerChar);
        }
        _running = null;
    }
}
