using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Audio;
using TMPro;
using UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class RoundManager : NetworkBehaviour
{
    /// <summary>Round phase countdown; sourced from <see cref="GameplayTestManager"/> when present, else a fixed default.</summary>
    public float RoundTimeLimitInSeconds => GameplayTestManager.EffectiveRoundTimeLimitInSeconds;

    /// <summary>Multiplier on round timer delta after any player submits (server + clients stay in sync via <see cref="AnyPlayerSubmittedThisRound"/>).</summary>
    public float RoundTimeSpeedMultiplierAfterAnySubmit => GameplayTestManager.EffectiveRoundTimeSpeedMultiplierAfterAnySubmit;

    /// <summary>Resolution phase countdown; sourced from <see cref="GameplayTestManager"/> when present, else a fixed default.</summary>
    public float ResolutionTimeLimitInSeconds => GameplayTestManager.EffectiveResolutionTimeLimitInSeconds;
    public int BanLetterAtStartOfResolutionPhaseOfRound = 3;
    private int m_currentRoundIndex = 0;
    public List<string> SubmittedAnswers = new List<string>();

    public NetworkVariable<bool> IsResolutionPhase = new NetworkVariable<bool>();
    public NetworkVariable<bool> AnyPlayerSubmittedThisRound = new NetworkVariable<bool>();
    public NetworkVariable<float> RoundTimeRemainingInSeconds = new NetworkVariable<float>();
    public NetworkVariable<float> ResolutionTimeRemainingInSeconds = new NetworkVariable<float>();

    private float m_localRoundTimeRemainingInSeconds;
    public float LocalRoundTimeRemainingInSeconds => m_localRoundTimeRemainingInSeconds;
    private float m_localResolutionTimeRemainingInSeconds;
    public float LocalResolutionTimeRemainingInSeconds => m_localResolutionTimeRemainingInSeconds;

    private bool m_isToBanLetter;

    private bool _started;
    private bool _ended;
    private bool _promptGenerated;
    private bool _roundTimerStarted;
    private readonly HashSet<ulong> _gameplayUiEnteredClients = new HashSet<ulong>();
    bool _startedFromLobbyReadyFlow;
    bool _useMainUiLobbyFlow;
    readonly HashSet<ulong> _promptShowcaseFinishedClients = new HashSet<ulong>();
    private bool _startResolute;

    private readonly List<ulong> m_submittedAnswerClients = new List<ulong>();
    private readonly List<ulong> m_confirmedResolutionClients = new List<ulong>();

    private bool m_isGameEnd = false;

    private void Start()
    {
        ResetRoundManager();
        m_isToBanLetter = true;
        UIManager.Instance.IsNotToBanLetterIcon.SetActive(false);
        //ThemeMusicManager.Instance.PlayMainMenuTheme();
        AudioManager.Instance.PlayMainMenuMusic();
    }

    public override void OnNetworkSpawn()
    {
        ResetRoundManager();
        UIManager.Instance.IsNotToBanLetterIcon.SetActive(false);
        UIManager.Instance.UpdateBannedLettersText("", !m_isToBanLetter);
        GameManager.Instance.GameStartedState.OnValueChanged += OnGameStartedStateChanged;

        RoundTimeRemainingInSeconds.OnValueChanged += OnTimeRemainingChanged;
        ResolutionTimeRemainingInSeconds.OnValueChanged += OnResolutionTimeRemainingChanged;
    }

    public override void OnDestroy()
    {
        RoundTimeRemainingInSeconds.OnValueChanged -= OnTimeRemainingChanged;
        ResolutionTimeRemainingInSeconds.OnValueChanged -= OnResolutionTimeRemainingChanged;
    }

    private void OnTimeRemainingChanged(float oldValue, float newValue)
    {
        m_localRoundTimeRemainingInSeconds = newValue;
    }

    private void OnResolutionTimeRemainingChanged(float previousValue, float newValue)
    {
        m_localResolutionTimeRemainingInSeconds = newValue;
    }

    public void ResetRoundManager()
    {
        m_currentRoundIndex = 0;
        m_localRoundTimeRemainingInSeconds = RoundTimeLimitInSeconds;
        m_localResolutionTimeRemainingInSeconds = ResolutionTimeLimitInSeconds;

        _started = false;
        _ended = false;
        _promptGenerated = false;
        _roundTimerStarted = false;
        _gameplayUiEnteredClients.Clear();
        _startedFromLobbyReadyFlow = false;
        _useMainUiLobbyFlow = false;
        _promptShowcaseFinishedClients.Clear();
        _startResolute = false;
        m_bannedLettersText = "";
        UIManager.Instance.MarkBannedLetters("");
        UIManager.Instance.UpdateBannedLettersText("");
        SubmittedAnswers.Clear();

        //m_usedAnswers.Clear();
        FindAnyObjectByType<PromptGenerator>().UsesPrompts.Clear();

        if (IsServer)
        {
            RoundTimeRemainingInSeconds.Value = RoundTimeLimitInSeconds;
            ResolutionTimeRemainingInSeconds.Value = ResolutionTimeLimitInSeconds;
            IsResolutionPhase.Value = false;
            AnyPlayerSubmittedThisRound.Value = false;
            m_confirmedResolutionClients.Clear();
            m_submittedAnswerClients.Clear();
            m_isGameEnd = false;
        }

        Debug.Log("RoundManager has been reset.");
    }


    private void Update()
    {
        if (!IsSpawned && Keyboard.current.equalsKey.wasPressedThisFrame)
        {
            m_isToBanLetter = !m_isToBanLetter;
            UIManager.Instance.IsNotToBanLetterIcon.SetActive(!m_isToBanLetter);
        }

        if (IsResolutionPhase.Value && !m_isGameEnd)
        {
            HandleResolutionPhase();
            return;
        }

        if (!m_isGameEnd)
            HandleRoundPhase();

        if (IsServer)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (m_isGameEnd)
                {
                    GameManager gm = FindAnyObjectByType<GameManager>();
                    if (gm != null)
                    {
                        gm.ResetGame();
                    }

                    m_isGameEnd = false;
                }
            }
        }
    }

    private void HandleRoundPhase()
    {
        if (!_started) return;

        float roundTickScale = AnyPlayerSubmittedThisRound.Value ? RoundTimeSpeedMultiplierAfterAnySubmit : 1f;
        if (_roundTimerStarted)
            m_localRoundTimeRemainingInSeconds -= Time.deltaTime * roundTickScale;

        if (!IsServer) return;

        if (!_promptGenerated)
        {
            GeneratePrompt();
            Debug.Log("Prompt generated");
            _promptGenerated = true;
            _roundTimerStarted = false;
            _gameplayUiEnteredClients.Clear();
        }

        if (_roundTimerStarted)
            RoundTimeRemainingInSeconds.Value -= Time.deltaTime * roundTickScale;

        if (_roundTimerStarted && RoundTimeRemainingInSeconds.Value < 0)
        {
            OnRoundTimeOutClientRpc();
            EnterResolutionPhase();
        }
    }

    private void HandleResolutionPhase()
    {
        if (!_started) return;

        m_localResolutionTimeRemainingInSeconds -= Time.deltaTime;

        if (!IsServer) return;

        ResolutionTimeRemainingInSeconds.Value -= Time.deltaTime;

        if (_startResolute)
        {
            _startResolute = false;
            StartCoroutine(DelayResolve());
        }

        if (ResolutionTimeRemainingInSeconds.Value < 0)
        {
            EndResolutionPhase();
        }
    }

    private IEnumerator DelayEnterNextRound()
    {
        yield return new WaitForSeconds(0.1f);
        EnterNextRound();
    }

    private const float k_resolveDelayTimeInSeconds = 0.3f;

    private void EnterNextRound()
    {
        Debug.Log("Entering Next Round");

        m_submittedAnswerClients.Clear();
        AnyPlayerSubmittedThisRound.Value = false;

        m_currentRoundIndex++;

        RoundTimeRemainingInSeconds.Value = RoundTimeLimitInSeconds;
        ResolutionTimeRemainingInSeconds.Value = ResolutionTimeLimitInSeconds;

        EnterNextRoundClientRpc();
    }

    private void EnterResolutionPhase()
    {
        m_submittedAnswerClients.Clear();
        IsResolutionPhase.Value = true;
        _startResolute = true;
    }

    private IEnumerator DelayResolve()
    {
        yield return new WaitForSeconds(k_resolveDelayTimeInSeconds);
        ResoluteServerRpc();
    }

    [Rpc(SendTo.Server)]
    private void ResoluteServerRpc()
    {
        //string text = "";
        string hostAnswer = "";
        string clientAnswer = "";
        var hostHpAfter = 0;
        var clientHpAfter = 0;
        var hostAnswerLetterEligible = false;
        var clientAnswerLetterEligible = false;

        if (FindAnyObjectByType<PlayerManager>() is PlayerManager pm)
        {
            Client host = pm.GetHost();
            Client client = pm.GetClient(1);

            hostAnswer = String.IsNullOrEmpty(host.Answer) ? "" : host.Answer;
            clientAnswer = String.IsNullOrEmpty(client.Answer) ? "" : client.Answer;

            int hostScore = host.AnswerCheckedValid.Value ? host.LetterCount.Value : 0;
            int clientScore = client.AnswerCheckedValid.Value ? client.LetterCount.Value : 0;

            hostAnswerLetterEligible = host != null && host.AnswerCheckedValid.Value;
            clientAnswerLetterEligible = client != null && client.AnswerCheckedValid.Value;

            int difference = hostScore - clientScore;

            if (difference > 0) // Host wins
            {
                client.CurrentHp.Value -= difference;
            }
            else if (difference < 0) // Client wins
            {
                host.CurrentHp.Value += difference;
            }

            //host.CurrentHp.Value += hostScore;
            //client.CurrentHp.Value += clientScore;

            // End game immediately on the server so _ended is true before resolution can end (Space x2).
            // Otherwise CheckWinState only runs after DelayCheckWinStateNUpdateScoreUI (0.1s) and EndResolutionPhase can call EnterNextRound() by mistake.
            if (host.CurrentHp.Value <= 0 || client.CurrentHp.Value <= 0)
            {
                var gm = GameManager.Instance;
                if (gm != null)
                    gm.EndGameServerRpc();
            }

            StartCoroutine(DelayCheckWinStateNUpdateScoreUI(host, client));

            hostHpAfter = host != null ? host.CurrentHp.Value : 0;
            clientHpAfter = client != null ? client.CurrentHp.Value : 0;
        }

        // Ban Letter
        if (m_currentRoundIndex % BanLetterAtStartOfResolutionPhaseOfRound == 0)
        {
            if (IsServer)
                BanLetter();
        }

        EnterResolutionPhaseClientRpc(hostAnswer, clientAnswer, hostHpAfter, clientHpAfter, hostAnswerLetterEligible, clientAnswerLetterEligible);
    }

    private void EndResolutionPhase()
    {
        m_confirmedResolutionClients.Clear();
        IsResolutionPhase.Value = false;

        _promptGenerated = false;

        if (_ended)
        {
            _ended = false;
            m_isGameEnd = true;
            bool isHostWin = PlayerManager.Instance.GetHost().CurrentHp.Value >
                             PlayerManager.Instance.GetClient(1).CurrentHp.Value;
            bool isDraw = PlayerManager.Instance.GetHost().CurrentHp.Value ==
                          PlayerManager.Instance.GetClient(1).CurrentHp.Value;
            // string winText =
            //     isDraw ? "both" : isHostWin ? "P1" : "P2";
            
            string playerID = isDraw ? "draw" : isHostWin ? PlayerManager.Instance.GetHost().OwnerClientId.ToString() : PlayerManager.Instance.GetClient(1).OwnerClientId.ToString();

            EndGameClientRpc(playerID);
            return;
        }

        EnterNextRound();
        EndResolutionPhaseClientRpc();
    }

    private void OnGameStartedStateChanged(bool previousStartState, bool start)
    {
        if (start)
        {
            _started = true;

            // In the new lobby-ready flow we start the match and generate prompt manually.
            // Do NOT auto-enter next round here, or it will interrupt the PromptShowcase animation.
            if (IsServer && !_startedFromLobbyReadyFlow)
                StartCoroutine(DelayEnterNextRound());

            return;
        }

        if (!IsServer) return;

        _ended = true;
    }

    [Rpc(SendTo.Server)]
    public void SubmitAnswerServerRpc(ulong clientId, string answer)
    {
        // Mark player as submitted
        if (!m_submittedAnswerClients.Contains(clientId))
        {
            m_submittedAnswerClients.Add(clientId);
            if (m_submittedAnswerClients.Count == 1)
                AnyPlayerSubmittedThisRound.Value = true;
        }

        // Mark answer in round words list
        SubmittedAnswers.Add(answer);

        //SoundManager.Instance?.PlaySubmitSfxServerRpc();

        if (m_submittedAnswerClients.Count >= 2)
            EnterResolutionPhase();
    }

    private void BanLetter()
    {
        char selectedLetter = '\0';

        if (SubmittedAnswers.Count == 0)
        {
            var availableLetters = Enumerable.Range('a', 26)
                .Select(i => (char)i)
                .Where(c => !m_bannedLettersText.Contains(c))
                .ToList();

            selectedLetter = availableLetters[Random.Range(0, availableLetters.Count)];

            m_bannedLettersText += selectedLetter;
        }
        else
        {
            var letterFrequencies = SubmittedAnswers
                .SelectMany(s => s.ToCharArray())
                .Where(char.IsLetter)
                .Where(c => !m_bannedLettersText.Contains(c))
                .GroupBy(c => c)
                .OrderByDescending(g => g.Count())
                .ToList();

            foreach (var group in letterFrequencies)
            {
                char letter = group.Key;

                if (!m_bannedLettersText.Contains(letter))
                {
                    selectedLetter = letter;
                    break;
                }
            }
        }

        SubmittedAnswers.Clear();

        if (selectedLetter == '\0')
        {
            Debug.LogWarning("No available letter to ban!");
        }
        else
        {
            UpdateBannedLettersTextClientRpc(selectedLetter);
            Debug.Log($"Banned letter {selectedLetter}");
        }
    }

    private string m_bannedLettersText = "";

    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateBannedLettersTextClientRpc(char bannedLetter)
    {
        if (!m_isToBanLetter)
        {
            UIManager.Instance.UpdateBannedLettersText("", true);
            return;
        }

        var letter = bannedLetter.ToString();
        m_bannedLettersText = letter;
        m_bannedLettersText = m_bannedLettersText.ToLower();
        string bannedLetters = m_bannedLettersText;
        UIManager.Instance.MarkBannedLetters(bannedLetters);
        UIManager.Instance.UpdateBannedLettersText(bannedLetters, !m_isToBanLetter);
    }

    public bool HasBannedLetterInAnswer(string answer)
    {
        if (string.IsNullOrEmpty(m_bannedLettersText))
            return false;
        return answer.Contains(m_bannedLettersText);
    }

    public int GetValidLetterCount(string text)
    {
        if (m_bannedLettersText == null) return text.Length;

        int count = 0;

        foreach (char c in text)
        {
            if (char.IsLetter(c) && !m_bannedLettersText.Contains(char.ToLower(c)))
            {
                count++;
            }
        }

        return count;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void OnSubmitAnswerClientRpc()
    {
    }

    [Rpc(SendTo.Server)]
    public void ConfirmResolutionServerRpc(ulong clientId)
    {
        Debug.Log($"Confirming resolution from client {clientId}");

        if (!m_confirmedResolutionClients.Contains(clientId))
            m_confirmedResolutionClients.Add(clientId);

        PlayerManager.Instance.GetHost().UpdateConfirmClientRpc(clientId);
        PlayerManager.Instance.GetClient(1).UpdateConfirmClientRpc(clientId);

        AudioManager.Instance.PlaySubmitSfxServerRpc();

        if (m_confirmedResolutionClients.Count >= 2)
            EndResolutionPhase();

        ConfirmResolutionClientRpc(clientId);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void ConfirmResolutionClientRpc(ulong clientId)
    {
        if (OwnerClientId == clientId)
        {
            //UIManager.Instance.UpdateResolutionPressSpaceHintText("");
        }
    }

    // ============================================================
    // Prompt & Resolution
    // ============================================================
    private void GeneratePrompt()
    {
        if (FindAnyObjectByType<PromptGenerator>() is PromptGenerator pg)
        {
            Debug.Log("Generating prompt for round");
            pg.TryUpdatePrompt();
        }
    }

    /// <summary>
    /// Server-only: marks the match started, generates the first prompt, but does NOT start the round timer.
    /// Round timer begins when both clients notify they've entered Gameplay UI (see <see cref="NotifyGameplayUiEnteredServerRpc"/>).
    /// </summary>
    public void BeginMatchFromLobbyServer()
    {
        if (!IsServer) return;

        _startedFromLobbyReadyFlow = true;
        _useMainUiLobbyFlow = true;
        _promptShowcaseFinishedClients.Clear();
        _started = true;
        _ended = false;
        m_isGameEnd = false;
        IsResolutionPhase.Value = false;
        AnyPlayerSubmittedThisRound.Value = false;

        _promptGenerated = false;
        _roundTimerStarted = false;
        _gameplayUiEnteredClients.Clear();
        m_submittedAnswerClients.Clear();
        m_confirmedResolutionClients.Clear();

        RoundTimeRemainingInSeconds.Value = RoundTimeLimitInSeconds;
        ResolutionTimeRemainingInSeconds.Value = ResolutionTimeLimitInSeconds;

        GeneratePrompt();
        _promptGenerated = true;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void NotifyPromptShowcaseFinishedServerRpc(ulong clientId)
    {
        if (!IsServer) return;
        if (!_started) return;
        if (!_useMainUiLobbyFlow) return;

        _promptShowcaseFinishedClients.Add(clientId);
        if (_promptShowcaseFinishedClients.Count >= 2)
        {
            _promptShowcaseFinishedClients.Clear();
            EnterNextRound();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void NotifyGameplayUiEnteredServerRpc(ulong clientId)
    {
        if (!IsServer) return;
        if (!_started) return;
        if (!_promptGenerated) return;
        if (_roundTimerStarted) return;

        _gameplayUiEnteredClients.Add(clientId);
        if (_gameplayUiEnteredClients.Count >= 2)
        {
            _roundTimerStarted = true;
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void UpdateMainUiPromptClientRpc(string promptText, string bannedLetters)
    {
        if (UIManager.Instance != null)
            UIManager.Instance.SetMainUiPrompt(promptText, bannedLetters);
    }

    private const float k_checkWinStateDelayInSeconds = 0.1f;

    IEnumerator DelayCheckWinStateNUpdateScoreUI(Client host, Client client)
    {
        yield return new WaitForSeconds(k_checkWinStateDelayInSeconds);
        host.CheckWinStateServerRpc(host.OwnerClientId);
        client.CheckWinStateServerRpc(client.OwnerClientId);
        host.UpdateScoreUIClientRpc();
        client.UpdateScoreUIClientRpc();
    }

    // ============================================================
    // Client RPCs
    // ============================================================
    [Rpc(SendTo.ClientsAndHost)]
    private void OnRoundTimeOutClientRpc()
    {
        foreach (Client client in FindObjectsByType<Client>(FindObjectsSortMode.InstanceID))
        {
            if (!client.IsOwner) continue;
            if (client.AnswerCheckedValid.Value) continue;

            if (!client.TrySubmitAnswer())
            {
                client.LetterCount.Value = 0;
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void EnterResolutionPhaseClientRpc(string hostAnswer, string clientAnswer, int hostHpAfter, int clientHpAfter, bool hostAnswerLetterEligible, bool clientAnswerLetterEligible)
    {
        //ThemeMusicManager.Instance.PlayScoringTheme();
        AudioManager.Instance.PlayWaitingMusic();

        foreach (var c in FindObjectsByType<Client>(FindObjectsSortMode.InstanceID))
        {
            if (c.IsOwner)
            {
                c.OnEnterResolutionPhase();
            }
        }

        UIManager.Instance.EnterResolutionPhaseFromRound(hostAnswer, clientAnswer, hostHpAfter, clientHpAfter, hostAnswerLetterEligible, clientAnswerLetterEligible);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void EndResolutionPhaseClientRpc()
    {
        foreach (var c in FindObjectsByType<Client>(FindObjectsSortMode.InstanceID))
            c.OnEndResolutionPhase();

        if (UIManager.Instance != null && UIManager.Instance.UsesMainUiGameplayFlow)
            UIManager.Instance.BeginMainUiNextRoundAfterResolution();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void EnterNextRoundClientRpc()
    {
        //ThemeMusicManager.Instance.PlayTypingTheme();
        // Typing music starts when gameplay UI is actually shown (UIManager / MainUIController), not here.
        m_localRoundTimeRemainingInSeconds = RoundTimeLimitInSeconds;
        m_localResolutionTimeRemainingInSeconds = ResolutionTimeLimitInSeconds;

        foreach (var c in FindObjectsByType<Client>(FindObjectsSortMode.InstanceID))
        {
            if (c.IsOwner)
            {
                c.OnEnterNextRound();
            }
        }

        if (UIManager.Instance != null && UIManager.Instance.UsesMainUiGameplayFlow)
            return;

        UIManager.Instance.EnterGameScreen();
        UIManager.Instance.UpdateAnswerInputFieldInteractability(true);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void EndGameClientRpc(string playerID)
    {
        if (UIManager.Instance != null)
            UIManager.Instance.EnterWinScreenOrMainUiGameEnd(playerID);
        //ThemeMusicManager.Instance.PlayScoringTheme();
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayWaitingMusic();
    }

    // private List<string> m_usedAnswers = new List<string>();
    // public List<string> UsedAnswers => m_usedAnswers;
    //
    // [Rpc(SendTo.Server)]
    // public void MarkUsedWordServerRpc(string answer)
    // {
    //     if (!m_usedAnswers.Contains(answer))
    //     {
    //         m_usedAnswers.Add(answer);
    //         string packedAnswers = string.Join(",", m_usedAnswers);
    //         UpdateUsedWordsClientRpc(packedAnswers);
    //     }
    // }
    //
    // [Rpc(SendTo.ClientsAndHost)]
    // private void UpdateUsedWordsClientRpc(string packedAnswers)
    // {
    //     m_usedAnswers = packedAnswers.Split(',').ToList();
    // }
    //
    // public bool IsAnswerUsed(string answer)
    // {
    //     return UsedAnswers != null && UsedAnswers.Contains(answer.ToLower());
    // }
}