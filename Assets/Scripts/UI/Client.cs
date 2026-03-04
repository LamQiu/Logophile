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
        if (IsServer)
        {
            PlayerManager.Instance.RegisterPlayer(OwnerClientId, this);
        }

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

    #endregion

    #region ===== UI Updates =====

    private void UpdateTimerUI(float timerRemainingInSeconds)
    {
        UIManager.Instance.UpdateGameScreenTimer(timerRemainingInSeconds / _roundManager.RoundTimeLimitInSeconds);
    }

    private void UpdatePrompt(PromptGenerator.Prompt value)
    {
        _currentPrompt = value;
        UIManager.Instance.UpdateCurrentPrompt(value.ToString());
    }

    private void ClearCurrentAnswer()
    {
        m_answer = "";
        UIManager.Instance.UpdateAnswerInputField("");
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
        ClearCurrentAnswer();
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

    private void Start()
    {
        if (IsOwner)
        {
            UIManager.Instance.AddListenerToAnswerInputField(OnLocalInputFieldChanged);
            _wordChecker = new WordChecker();
        }
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
        _roundManager.RoundTimeRemainingInSeconds.OnValueChanged -= OnTimeRemainingChanged;
    }

    #endregion

    #region ===== Word Checking & Submitting =====

    public bool TrySubmitAnswer()
    {
        if (m_answer == null) return false;
        
        string answer = m_answer;
        bool isAnswerValidInDictionary = _wordChecker.CheckWordDictionaryValidity(answer);
        if (!isAnswerValidInDictionary)
        {
            HintText = $"invalid word \"{answer}\". try again";
            return false;
        }

        bool isAnswerValidForCurrentPrompt = _wordChecker.CheckWordPromptValidity(answer, _currentPrompt);
        if (!isAnswerValidForCurrentPrompt)
        {
            HintText = $"word \"{answer}\" doesn't match the prompt. try again";
            return false;
        }

        bool isAnswerUsed = IsAnswerUsed(answer);
        if (isAnswerUsed)
        {
            HintText = "word already used";
            return false;
        }
        
        bool doesAnswerContainBannedLetter = _roundManager.HasBannedLetterInAnswer(answer);
        if (doesAnswerContainBannedLetter)
        {
            HintText = "word contains banned letter";
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