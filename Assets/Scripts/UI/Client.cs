using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UI;
using UnityEngine.InputSystem;

public class Client : NetworkBehaviour
{
    public string HintText;

    #region ===== Network Variables =====

    public NetworkVariable<int> LetterCount = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    public NetworkVariable<int> CurrentHp = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> AnswerCheckedValid = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    #endregion
    
    private WordChecker _wordChecker;
    private RoundManager _roundManager;
    private PromptGenerator.Prompt _currentPrompt;

    private string m_answer = "";
    public string Answer => m_answer;
    private bool m_answerCheckedValid = false;
    private Client m_otherClient;
    private List<string> m_usedAnswers = new List<string>();

    #region ===== Reset Helpers =====

    private void ResetLocalStates()
    {
        m_answerCheckedValid = false;
        m_answer = "";
        m_usedAnswers.Clear();
    }

    private void ResetUI()
    {
        HintText = "";
    }

    private void ResetNetworkVariables()
    {
        if (IsOwner)
        {
            LetterCount.Value = 0;
            AnswerCheckedValid.Value = false;
        }

        if (IsServer)
        {
            CurrentHp.Value = GameManager.Instance.MaxPlayerHp;
        }
    }

    public void ResetClient()
    {
        ResetLocalStates();
        ResetUI();
        ResetNetworkVariables();
    }

    #endregion

    #region ===== Network Spawn =====

    public override void OnNetworkSpawn()
    {
        // Every peer needs the local PlayerManager map (e.g. MainUI resolution HP from GetHost/GetClient).
        // Server-only registration left non-host clients with an empty Players dictionary.
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.RegisterPlayer(OwnerClientId, this);

        if (IsOwner)
        {
            LetterCount.OnValueChanged += OnLetterCountChanged;
            CurrentHp.OnValueChanged += OnCurrentScoreChanged;

            var promptGenerator = FindAnyObjectByType<PromptGenerator>();
            if (promptGenerator != null)
            {
                promptGenerator.CurrentPrompt.OnValueChanged += OnPromptChanged;
            }
        }

        _roundManager = FindAnyObjectByType<RoundManager>();
        _roundManager.RoundTimeRemainingInSeconds.OnValueChanged += OnTimeRemainingChanged;

        ResetClient();

        if (IsHost || IsClient)
        {
            UIManager.Instance.Client = this;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.UnregisterPlayer(OwnerClientId);
    }

    #endregion

    #region ===== UI Updates =====

    private void UpdateTimerUI(float timerRemainingInSeconds)
    {
        float limit = _roundManager.RoundTimeLimitInSeconds;
        float normalized = limit > 0f ? timerRemainingInSeconds / limit : 0f;
        UIManager.Instance.UpdateGameScreenTimer(normalized, _roundManager.AnyPlayerSubmittedThisRound.Value);
    }

    private void UpdatePrompt(PromptGenerator.Prompt value)
    {
        _currentPrompt = value;
        UIManager.Instance.UpdateCurrentPrompt(value.ToString());
    }

    private void ClearCurrentAnswer(bool refocusSharedInputField = true)
    {
        m_answer = "";
        if (IsOwner)
        {
            LetterCount.Value = 0;
            UpdateServerAnswerServerRpc("");
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateAnswerInputField("");
            UIManager.Instance.SyncMainUiGameplayLetterRowsAfterLocalAnswerCleared();
            UIManager.Instance.UpdateAnswerInputFieldInteractability(true);
            if (refocusSharedInputField)
                UIManager.Instance.FocusAnswerInputFieldNextFrame();
        }
    }

    #endregion


    #region ===== Callbacks =====

    private void OnTimeRemainingChanged(float prev, float value)
    {
        //UpdateTimerUI(value);
    }

    private void OnLetterCountChanged(int prev, int value)
    {
        
    }

    private Client GetOtherClient()
    {
        Client result = null;
        Client[] clients = FindObjectsByType<Client>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        foreach (Client client in clients)
        {
            if (client != this && client.OwnerClientId != OwnerClientId)
            {
                result = client;
                break;
            }
        }

        return result;
    }

    private void OnCurrentScoreChanged(int prev, int value)
    {
        Debug.Log(m_otherClient == null);
        if(m_otherClient == null)
        {
            return;
        }
        
        UIManager.Instance.UpdatePlayerFillImage(IsHost, CurrentHp.Value, m_otherClient.CurrentHp.Value);
    }

    private void OnPromptChanged(PromptGenerator.Prompt prev, PromptGenerator.Prompt value)
    {
        UpdatePrompt(value);
    }

    #endregion

    #region ===== Phase Handling =====

    public void OnEnterResolutionPhase()
    {
        CheckWinStateServerRpc(OwnerClientId);
        UIManager.Instance.UpdatePlayerFillImage(IsHost, CurrentHp.Value, m_otherClient.CurrentHp.Value);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void UpdateScoreUIClientRpc()
    {
        if (IsOwner)
        {
            if (IsHost)
            {
                UIManager.Instance.UpdatePlayer1FillImage(CurrentHp.Value / (float)GameManager.Instance.MaxPlayerHp,
                    CurrentHp.Value);
                if (m_otherClient != null)
                {
                    UIManager.Instance.UpdatePlayer2FillImage(
                        m_otherClient.CurrentHp.Value / (float)GameManager.Instance.MaxPlayerHp,
                        m_otherClient.CurrentHp.Value);
                }
            }
            else if (IsClient)
            {
                UIManager.Instance.UpdatePlayer2FillImage(CurrentHp.Value / (float)GameManager.Instance.MaxPlayerHp,
                    CurrentHp.Value);
                if (m_otherClient != null)
                {
                    UIManager.Instance.UpdatePlayer1FillImage(
                        m_otherClient.CurrentHp.Value / (float)GameManager.Instance.MaxPlayerHp,
                        m_otherClient.CurrentHp.Value);
                }
            }
        }
    }

    public void OnEndResolutionPhase()
    {
        UIManager.Instance.UpdateAnswerInputFieldInteractability(true);
        ClearCurrentAnswer(refocusSharedInputField: false);
    }

    [Rpc(SendTo.Server)]
    public void CheckWinStateServerRpc(ulong id)
    {
        if (CurrentHp.Value <= 0)
        {
            GameManager.Instance.EndGameServerRpc();
        }
    }

    public void OnEnterNextRound()
    {
        if (m_otherClient == null)
        {
            m_otherClient = GetOtherClient();
        }

        HintText = "press enter to submit";

        if (IsHost)
        {
            UIManager.Instance.SetP1();
            UIManager.Instance.ResolutionScreenSetP1();
        }
        else if (IsClient)
        {
            UIManager.Instance.SetP2();
            UIManager.Instance.ResolutionScreenSetP2();
        }

        LetterCount.Value = 0;
        m_answerCheckedValid = false;
        AnswerCheckedValid.Value = false;

        UIManager.Instance.UpdateAnswerInputFieldInteractability(true);
        UIManager.Instance.UpdateGameScreenHintText(HintText);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void UpdateConfirmClientRpc(ulong id)
    {
        if (NetworkManager.Singleton.LocalClientId == id)
        {
            Debug.Log(($"Update Resolution Press Space Hint Text: clientID: {id}"));
            HintText = "";
            UIManager.Instance.UpdateResolutionPressSpaceHintText("");
        }
    }

    #endregion

    #region ===== Input Handling =====

    bool _gameplayInputRegistered;

    /// <summary>
    /// Called when the game has actually entered the gameplay screen (hook this up later).
    /// </summary>
    public void OnEnteredGameplayScreen()
    {
        if (!IsOwner) return;
        if (_gameplayInputRegistered) return;

        // (Intentionally lightweight; UI can exist before gameplay starts.)
        if (UIManager.Instance != null)
        {
            UIManager.Instance.AddListenerToAnswerInputField(OnLocalInputFieldChanged);
            UIManager.Instance.AddSubmitListenerToAnswerInputField(OnAnswerInputSubmit);
        }

        _wordChecker ??= new WordChecker();
        _gameplayInputRegistered = true;
    }

    /// <summary>
    /// Clears shared input listeners so a later <see cref="OnEnteredGameplayScreen"/> can attach again (e.g. next round after MainUI Loading → PromptShowcase → Gameplay).
    /// </summary>
    public void ResetGameplayAnswerListenersForNewRound()
    {
        if (!IsOwner) return;
        if (!_gameplayInputRegistered) return;
        if (UIManager.Instance != null)
        {
            UIManager.Instance.RemoveListenerFromAnswerInputField(OnLocalInputFieldChanged);
            UIManager.Instance.RemoveSubmitListenerFromAnswerInputField(OnAnswerInputSubmit);
        }

        _gameplayInputRegistered = false;
    }

    void OnAnswerInputSubmit(string submittedLine)
    {
        if (_roundManager != null && _roundManager.IsResolutionPhase.Value)
            return;
        if (m_answerCheckedValid)
            return;

        if (!TrySubmitAnswer(submittedLine))
            ClearCurrentAnswer();

        UIManager.Instance?.UpdateGameScreenHintText(HintText);
    }

    private void Start()
    {
        // NOTE: gameplay input is registered later via OnEnteredGameplayScreen().
        if (IsOwner)
            _wordChecker ??= new WordChecker();
    }

    private void Update()
    {
        UpdateTimerUI(_roundManager.LocalRoundTimeRemainingInSeconds);

        if (!IsOwner) return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (_roundManager.IsResolutionPhase.Value)
            {
                _roundManager.ConfirmResolutionServerRpc(OwnerClientId);
            }
        }

        if (!m_answerCheckedValid && Keyboard.current.enterKey.wasPressedThisFrame)
        {
            if (!_roundManager.IsResolutionPhase.Value)
            {
                if (!TrySubmitAnswer())
                {
                    ClearCurrentAnswer();
                }

                UIManager.Instance.UpdateGameScreenHintText(HintText);
            }
        }

        if (IsHost)
        {
            UIManager.Instance.UpdateP1LettersCountUI(LetterCount.Value, true);
            if (m_otherClient != null)
            {
                UIManager.Instance.UpdateP2LettersCountUI(m_otherClient.LetterCount.Value, false);
            }
        }
        else if (IsClient)
        {
            if (m_otherClient != null)
            {
                UIManager.Instance.UpdateP1LettersCountUI(m_otherClient.LetterCount.Value, false);
            }

            UIManager.Instance.UpdateP2LettersCountUI(LetterCount.Value, true);
        }
    }

    public override void OnDestroy()
    {
        if (_roundManager != null)
            _roundManager.RoundTimeRemainingInSeconds.OnValueChanged -= OnTimeRemainingChanged;

        if (IsOwner && _gameplayInputRegistered && UIManager.Instance != null)
        {
            UIManager.Instance.RemoveListenerFromAnswerInputField(OnLocalInputFieldChanged);
            UIManager.Instance.RemoveSubmitListenerFromAnswerInputField(OnAnswerInputSubmit);
            _gameplayInputRegistered = false;
        }
    }

    #endregion

    #region ===== Word Checking & Submitting =====

    /// <summary>
    /// TMP may clear the input before <see cref="OnAnswerInputSubmit"/> runs; merge submit line, field text, and cached <see cref="m_answer"/> so validation hints keep the typed word.
    /// </summary>
    string ResolveAnswerForSubmitAttempt(string tmpSubmitLine, string cachedAnswer)
    {
        string TrimRemove(string s)
        {
            if (UIManager.Instance == null) return (s ?? "").Trim();
            return UIManager.Instance.RemoveColorTags(s ?? "").Trim();
        }

        var fromSubmit = TrimRemove(tmpSubmitLine);
        if (!string.IsNullOrEmpty(fromSubmit))
            return fromSubmit;

        if (UIManager.Instance != null)
        {
            var field = UIManager.Instance.AnswerInputField;
            if (field != null)
            {
                var fromField = TrimRemove(field.text);
                if (!string.IsNullOrEmpty(fromField))
                    return fromField;
            }
        }

        return TrimRemove(cachedAnswer);
    }

    /// <param name="forRoundTimeout">When true (round timer expiry), run the same checks as a manual submit; if anything fails or the answer is empty, still notify the server with an empty string.</param>
    public bool TrySubmitAnswer(string tmpSubmitLine = null, bool forRoundTimeout = false)
    {
        var answer = ResolveAnswerForSubmitAttempt(tmpSubmitLine, m_answer);
        m_answer = answer;

        if (string.IsNullOrEmpty(answer))
        {
            if (forRoundTimeout)
                SubmitEmptyAnswerForRoundTimeout();
            return false;
        }

        bool isAnswerValidInDictionary = _wordChecker.CheckWordDictionaryValidity(answer);
        if (!isAnswerValidInDictionary)
        {
            HintText = $"invalid word \"{answer}\". try again";
            if (forRoundTimeout)
                SubmitEmptyAnswerForRoundTimeout();
            return false;
        }

        bool isAnswerValidForCurrentPrompt = _wordChecker.CheckWordPromptValidity(answer, _currentPrompt);
        if (!isAnswerValidForCurrentPrompt)
        {
            HintText = $"word \"{answer}\" doesn't match the prompt. try again";
            if (forRoundTimeout)
                SubmitEmptyAnswerForRoundTimeout();
            return false;
        }

        bool isAnswerUsed = IsAnswerUsed(answer);
        if (isAnswerUsed)
        {
            HintText = "word already used";
            if (forRoundTimeout)
                SubmitEmptyAnswerForRoundTimeout();
            return false;
        }

        bool doesAnswerContainBannedLetter = _roundManager.HasBannedLetterInAnswer(answer);
        if (doesAnswerContainBannedLetter)
        {
            HintText = "word contains banned letter";
            if (forRoundTimeout)
                SubmitEmptyAnswerForRoundTimeout();
            return false;
        }

        HintText = $"\"{answer}\" submitted";
        MarkUsedWord(answer.ToLower());

        Debug.Log($"SubmitAnswerServerRpc {OwnerClientId}");
        _roundManager.SubmitAnswerServerRpc(OwnerClientId, answer);
        m_answerCheckedValid = true;
        AnswerCheckedValid.Value = true;
        UIManager.Instance.UpdateAnswerInputFieldInteractability(false);
        AudioManager.Instance.PlaySubmitSfxServerRpc();

        return true;
    }

    void SubmitEmptyAnswerForRoundTimeout()
    {
        m_answer = "";
        m_answerCheckedValid = false;
        LetterCount.Value = 0;
        AnswerCheckedValid.Value = false;
        UpdateServerAnswerServerRpc("");
        Debug.Log($"SubmitAnswerServerRpc (timeout empty) {OwnerClientId}");
        _roundManager.SubmitAnswerServerRpc(OwnerClientId, string.Empty);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateAnswerInputField("");
            UIManager.Instance.SyncMainUiGameplayLetterRowsAfterLocalAnswerCleared();
        }
    }

    #endregion

    #region ===== Used Words Sync =====
    
    private void MarkUsedWord(string word)
    {
        m_usedAnswers.Add(word);
    }
    
    private bool IsAnswerUsed(string answer)
    {
        return m_usedAnswers.Contains(answer.ToLower());
    }

    #endregion

    private void OnLocalInputFieldChanged(string value)
    {
        //if (AudioManager.Instance != null)
        Debug.Log("type");
            AudioManager.Instance.PlayTypingSFX();

        m_answer = UIManager.Instance.RemoveColorTags(value);
        UpdateServerAnswerServerRpc(m_answer);
        LetterCount.Value = _roundManager.GetValidLetterCount(m_answer);
        Debug.Log($"LetterCount in OnLocalInputFieldChanged: {m_answer} set to {LetterCount.Value}");
        UIManager.Instance.UpdateAnswerInputField(m_answer);
    }

    [Rpc(SendTo.Server)]
    private void UpdateServerAnswerServerRpc(string answer)
    {
        m_answer = answer;
    }
}