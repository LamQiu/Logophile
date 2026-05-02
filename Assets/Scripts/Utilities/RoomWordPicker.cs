using System.Collections.Generic;
using UnityEngine;

public static class RoomWordPicker
{
    private static List<string> _words;
    const int MinLen = 4, MaxLen = 8;

    static void EnsureLoaded()
    {
        if (_words != null) return;
        _words = new List<string>();
        var asset = Resources.Load<TextAsset>("Scrabble Dictionary");
        if (asset == null)
        {
            Debug.LogError("[RoomWordPicker] No Scrabble Dictionary found in Resources folder");
            return;
        }
        foreach (var line in asset.text.Split('\n'))
        {
            var w = line.Trim();
            if (w.Length >= MinLen && w.Length <= MaxLen)
                _words.Add(w);
        }
    }

    public static string GetRandomWord()
    {
        EnsureLoaded();
        if (_words == null || _words.Count == 0) return "LOBBY";
        return _words[Random.Range(0, _words.Count)];
    }
}
