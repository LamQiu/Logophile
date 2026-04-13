using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Sirenix.OdinInspector;
using Unity.Multiplayer.Center.NetcodeForGameObjectsExample.DistributedAuthority;
using Unity.Netcode;
using UnityEngine;
using UIManager = UI.UIManager;

public class PromptGenerator : NetworkBehaviour
{
    public bool randomize;
    private PromptType m_lastPromptType = PromptType.None;
    [SerializeField] private Prompt[] prompts;

    private List<PromptEntryData> _entryPrompts = new List<PromptEntryData>();

    private void Awake()
    {
        _entryPrompts = PromptEntryJsonLoader.Load();
    }

    public List<Prompt> UsesPrompts
    {
        get => usedPrompts;
        set => usedPrompts = value;
    }

    [SerializeField] private List<Prompt> usedPrompts = new List<Prompt>();
    public NetworkVariable<Prompt> CurrentPrompt = new NetworkVariable<Prompt>();

    public override void OnNetworkSpawn()
    {
        // 每个客户端收到广播时触发
        CurrentPrompt.OnValueChanged += OnPromptChanged;
    }

    [ContextMenu("Update Prompt")]
    public void TryUpdatePrompt()
    {
        UpdatePromptServerRpc();
    }

    [Rpc(SendTo.Server)]
    private void UpdatePromptServerRpc()
    {
        var unusedPrompts = prompts.Where(p => !usedPrompts.Contains(p)).ToList();

        if (unusedPrompts.Count == 0)
        {
            usedPrompts.Clear();
            unusedPrompts = prompts.ToList();
        }

        var randomPrompt = unusedPrompts[Random.Range(0, unusedPrompts.Count)];
        if (randomize)
        {
            var newType = RandomTypeExceptNone();
            if (newType == PromptType.Entry)
                randomPrompt = RandomEntryPrompt();
            else
                randomPrompt = new Prompt(newType, RandomContentExceptNone());
        }

        CurrentPrompt.Value = randomPrompt;
        usedPrompts.Add(randomPrompt);
    }

    PromptType RandomTypeExceptNone()
    {
        var all = System.Enum.GetValues(typeof(PromptType)).Cast<PromptType>()
            .Where(t => t != PromptType.None)
            .ToList();

        if (_entryPrompts.Count == 0)
            all.Remove(PromptType.Entry);

        all.Remove(m_lastPromptType);

        if (all.Count == 0)
        {
            all = System.Enum.GetValues(typeof(PromptType)).Cast<PromptType>()
                .Where(t => t != PromptType.None)
                .ToList();
            if (_entryPrompts.Count == 0)
                all.Remove(PromptType.Entry);
        }

        var result = all[Random.Range(0, all.Count)];
        m_lastPromptType = result;
        return result;
    }

    Prompt RandomEntryPrompt()
    {
        var idx = Random.Range(0, _entryPrompts.Count);
        var entry = _entryPrompts[idx];
        return new Prompt(PromptType.Entry, idx, entry.Question);
    }

    PromptContent RandomContentExceptNone()
    {
        var banned = UIManager.Instance.BannedLetters;

        var all = System.Enum.GetValues(typeof(PromptContent))
            .Cast<PromptContent>()
            .Where(c => c != PromptContent.None)
            .ToList();

        // 过滤 banned letters
        all = all.Where(c =>
        {
            string content = c.ToString().ToLower();
            foreach (char bannedChar in banned)
            {
                if (content.Contains(bannedChar))
                    return false;
            }

            return true;
        }).ToList();

        if (all.Count == 0)
        {
            Debug.LogWarning("No valid prompt content after banning letters!");
            return PromptContent.A; // fallback
        }

        return all[Random.Range(0, all.Count)];
    }

    private void OnPromptChanged(Prompt oldVal, Prompt newVal)
    {
        Debug.Log($"Received new prompt: {newVal.type} {newVal.content}");
    }

    public enum PromptType
    {
        None,
        StartWith,
        Contains,
        EndWith,
        Entry
    }

    public enum PromptContent
    {
        None,
        A,
        B,
        C,
        D,
        E,
        G,
        H,
        I,
        K,
        L,
        M,
        N,
        O,
        P,
        R,
        S,
        T,
        Y,
        ER,
        ST,
        OR,
        IN,
        AN,
    }

    [System.Serializable]
    public struct Prompt : INetworkSerializable
    {
        public PromptGenerator.PromptType type;
        public PromptGenerator.PromptContent content;
        public int entryIndex;
        public string questionText;

        public Prompt(PromptGenerator.PromptType t, PromptGenerator.PromptContent c)
        {
            type = t;
            content = c;
            entryIndex = -1;
            questionText = string.Empty;
        }

        public Prompt(PromptGenerator.PromptType t, int index, string question)
        {
            type = t;
            content = PromptContent.None;
            entryIndex = index;
            questionText = question ?? string.Empty;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref type);
            serializer.SerializeValue(ref content);
            serializer.SerializeValue(ref entryIndex);
            // Netcode WriteValueSafe throws if string is null
            var q = questionText ?? string.Empty;
            serializer.SerializeValue(ref q);
            questionText = q;
        }

        public override string ToString()
        {
            if (type == PromptType.Entry)
                return questionText ?? "";

            return Regex.Replace(type.ToString(), "([a-z])([A-Z])", "$1 $2")
                   + " " + " \"" + content + "\"";
        }
    }
}
