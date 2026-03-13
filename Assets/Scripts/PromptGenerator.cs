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
            randomPrompt = new Prompt(RandomTypeExceptNone(), RandomContentExceptNone());
        }

        CurrentPrompt.Value = randomPrompt;
        usedPrompts.Add(randomPrompt);
    }

    PromptType RandomTypeExceptNone()
    {
        var all = System.Enum.GetValues(typeof(PromptType)).Cast<PromptType>().ToList();
        all.Remove(PromptType.None);
        all.Remove(m_lastPromptType);
        var result = all[Random.Range(0, all.Count)];
        m_lastPromptType = result;
        return result;
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
            string content = c.ToString();
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
        EndWith
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

        public Prompt(PromptGenerator.PromptType t, PromptGenerator.PromptContent c)
        {
            type = t;
            content = c;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref type);
            serializer.SerializeValue(ref content);
        }

        public override string ToString()
        {
            return Regex.Replace(type.ToString(), "([a-z])([A-Z])", "$1 $2")
                   + " " + " \"" + content + "\"";
        }
    }
}