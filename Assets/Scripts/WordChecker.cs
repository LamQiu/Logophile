using System;
using System.Collections.Generic;
using UnityEngine;

public class PromptEntryData
{
    public string Question;
    public HashSet<string> WordsNormalized;
}

public static class PromptEntryJsonLoader
{
    public const string DefaultResourceName = "EntryPrompts";

    [Serializable]
    private class Root
    {
        public Entry[] entries;
    }

    [Serializable]
    private class Entry
    {
        public string question;
        public string[] words;
    }

    public static List<PromptEntryData> Load(string resourceName = DefaultResourceName)
    {
        var result = new List<PromptEntryData>();
        var asset = Resources.Load<TextAsset>(resourceName);
        if (asset == null || string.IsNullOrWhiteSpace(asset.text))
        {
            Debug.LogWarning($"PromptEntryJsonLoader: missing or empty resource '{resourceName}'.");
            return result;
        }

        var root = JsonUtility.FromJson<Root>(asset.text);
        if (root?.entries == null)
            return result;

        foreach (var e in root.entries)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.question) || e.words == null || e.words.Length == 0)
                continue;

            var set = new HashSet<string>();
            foreach (var w in e.words)
            {
                var t = w?.Trim().ToLower();
                if (!string.IsNullOrEmpty(t))
                    set.Add(t);
            }

            if (set.Count == 0)
                continue;

            result.Add(new PromptEntryData
            {
                Question = e.question.Trim(),
                WordsNormalized = set
            });
        }

        return result;
    }
}

public class WordChecker
{
    private TextAsset _wordFile;
    private HashSet<string> _dictionary;
    private readonly List<PromptEntryData> _entryPrompts;

    public WordChecker()
    {
        _wordFile = Resources.Load<TextAsset>("Scrabble Dictionary");
        _dictionary = new HashSet<string>();

        foreach (var line in _wordFile.text.Split('\n'))
        {
            string word = line.Trim().ToLower();
            if (!string.IsNullOrEmpty(word))
                _dictionary.Add(word);
        }

        _entryPrompts = PromptEntryJsonLoader.Load();
    }

    public bool CheckWordDictionaryValidity(string word)
    {
        return _dictionary.Contains(word.ToLower());
    }

    public bool CheckWordPromptValidity(string input, PromptGenerator.Prompt prompt)
    {
        var isValid = CheckWordDictionaryValidity(input);
        if (!isValid) return false;

        var lowerInput = input.ToLower();

        switch (prompt.type)
        {
            case PromptGenerator.PromptType.None:
                return false;
            case PromptGenerator.PromptType.Entry:
                if (prompt.entryIndex < 0 || prompt.entryIndex >= _entryPrompts.Count)
                    return false;
                return _entryPrompts[prompt.entryIndex].WordsNormalized.Contains(lowerInput);
            case PromptGenerator.PromptType.StartWith:
            {
                var content = prompt.content.ToString().ToLower();
                if (content.Length > lowerInput.Length)
                    return false;
                return lowerInput.StartsWith(content);
            }
            case PromptGenerator.PromptType.Contains:
            {
                var content = prompt.content.ToString().ToLower();
                if (content.Length > lowerInput.Length)
                    return false;
                return lowerInput.Contains(content);
            }
            case PromptGenerator.PromptType.EndWith:
            {
                var content = prompt.content.ToString().ToLower();
                if (content.Length > lowerInput.Length)
                    return false;
                return lowerInput.EndsWith(content);
            }
            default:
                return false;
        }
    }
}
