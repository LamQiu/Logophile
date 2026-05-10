using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace UI
{
    public class UIManager : Singleton<UIManager>
    {
        [SerializeField] private MainMenuUI MainMenuUI;
        [SerializeField] private ConnectionScreenUI ConnectionScreenUI;
        [SerializeField] private WaitingScreenUI WaitingScreenUI;
        [SerializeField] private TutorialScreenUI TutorialScreenUI;
        [Tooltip("Only active while MainUI is in RoomId state; use when it shares the same TMP_InputField as the main menu command field.")]
        [SerializeField] private RoomIdScreenUI RoomIdScreenUI;
        [SerializeField] private GameScreenUI GameScreenUI;
        [SerializeField] private ResolutionScreenUI ResolutionScreenUI;
        [SerializeField] private WinScreenUI WinScreenUI;

        [Header("Main UI (draft)")]
        [Tooltip("When enabled, EnterGameScreen hides GameScreenUI and calls TransitionToGameplay on this controller. Assign MainUI instance in the scene.")]
        [SerializeField] private bool m_useMainUIForGameplay;
        [SerializeField] private MainUIController m_mainUI;

        public GameObject IsNotToBanLetterIcon;

        [Header("Main menu text commands (MainMenuUI command field)")]
        [SerializeField] string m_mainMenuCreateCommand = "create";
        [SerializeField] string m_mainMenuJoinCommand = "join";
        [Tooltip("WaitingScreen room label after create command.")]
        [SerializeField] string m_createFlowWaitingRoomDisplayName = "Room";
        [Tooltip("WaitingScreen connection code string (also used for copy button).")]
        [SerializeField] string m_createFlowWaitingConnectionCode = "";

        Coroutine m_answerInputCaretCoroutine;

        public TMP_InputField AnswerInputField
        {
            get
            {
                if (m_useMainUIForGameplay && m_mainUI != null)
                {
                    var shared = m_mainUI.SharedAnswerInputField;
                    if (shared != null)
                        return shared;
                }

                return GameScreenUI.AnswerInputField;
            }
        }

        bool ShouldEnterGameplayViaMainUI => m_useMainUIForGameplay && m_mainUI != null;

        /// <summary>Inspector-driven; true on every peer when MainUI is assigned. Use in ClientRpc paths (RoundManager's lobby-only flags are not replicated to clients).</summary>
        public bool UsesMainUiGameplayFlow => m_useMainUIForGameplay && m_mainUI != null;

        bool m_awaitingSpaceOnTutorialScreen;

        private Client m_client;

        public Client Client
        {
            get => m_client;
            set => m_client = value;
        }

        protected override void Awake()
        {
            base.Awake();
        }
        
        public void ResetUI()
        {
            MainMenuUI.Hide();
            ConnectionScreenUI.Hide();
            WaitingScreenUI.Hide();
            if (TutorialScreenUI != null)
                TutorialScreenUI.Hide();
            if (RoomIdScreenUI != null)
                RoomIdScreenUI.Hide();
            GameScreenUI.Hide();
            ResolutionScreenUI.Hide();
            WinScreenUI.Hide();

            m_awaitingSpaceOnTutorialScreen = false;
            
            ResolutionScreenUI.Reset();
        }

        /// <summary>
        /// When <see cref="UsesMainUiGameplayFlow"/>, returns Main UI to full-screen Loading until the next <see cref="SetMainUiPrompt"/> / <see cref="MainUIController.NotifyPromptReceivedFromServer"/> (then the normal Loading → PromptShowcase animation).
        /// Call from the same place you reset <see cref="Client"/> / <see cref="RoundManager"/> for a fresh match; do not follow with <see cref="EnterGameScreen"/> unless you intend to skip Loading and jump straight to Gameplay.
        /// </summary>
        public void ResetMainUiMatchFlowToLoadingAwaitingPromptIfPresent()
        {
            if (!ShouldEnterGameplayViaMainUI) return;
            m_mainUI.ResetMatchFlowToLoadingAwaitingPrompt();
        }

        private void Start()
        {
            ResetUI();
            MainMenuUI.Show();
        }

        /// <summary>Shows the tutorial screen root and arms Space to leave tutorial (MainUI RoomId if assigned, else connection screen).</summary>
        /// <param name="transitionMainUiToTutorial">If true and <see cref="m_mainUI"/> is set, also runs MainUI tutorial transition (CMYK / layout).</param>
        public void EnterTutorialScreen(bool transitionMainUiToTutorial = false)
        {
            m_awaitingSpaceOnTutorialScreen = true;
            MainMenuUI.Hide();
            ConnectionScreenUI.Hide();
            WaitingScreenUI.Hide();
            if (TutorialScreenUI == null)
            {
                Debug.LogError("UIManager.EnterTutorialScreen: TutorialScreenUI is not assigned.");
                m_awaitingSpaceOnTutorialScreen = false;
                return;
            }

            TutorialScreenUI.Show();
            if (transitionMainUiToTutorial && m_mainUI != null)
                m_mainUI.TransitionToTutorial();
        }

        void TransitionFromTutorialToRoomId()
        {
            m_awaitingSpaceOnTutorialScreen = false;
            if (TutorialScreenUI != null)
                TutorialScreenUI.Hide();

            if (m_mainUI != null)
                m_mainUI.TransitionToRoomId();
            else
                EnterConnectionScreen();
        }

        public void EnterConnectionScreen()
        {
            MainMenuUI.Hide();
            ConnectionScreenUI.Show();
        }

        public void EnterWaitingScreen(string roomName, string connectionCode)
        {
            ConnectionScreenUI.Hide();
            WaitingScreenUI.Show(roomName, connectionCode);
        }

        public void TransitionMainUiToLoadingIfPresent()
        {
            if (m_mainUI != null)
                m_mainUI.TransitionToLoading();
        }

        public void SetRoomIdScreenForMainUiState(MainUIController.MainUIState state)
        {
            if (RoomIdScreenUI == null) return;
            if (state == MainUIController.MainUIState.RoomId)
                RoomIdScreenUI.Show();
            else
                RoomIdScreenUI.Hide();
        }

        /// <summary>After create or join session succeeds: MainUI → Waiting, then legacy waiting UI.</summary>
        public void OnCreateSessionSucceeded(string roomName, string connectionCode)
        {
            if (m_mainUI != null)
                m_mainUI.TransitionToWaiting(roomName);
            EnterWaitingScreen(roomName, connectionCode);
        }

        /// <summary>Main UI create flow showed Loading then <see cref="ConnectionScreenUI.CreateSessionAsync"/> failed — return to Start and restore main-menu command listeners.</summary>
        public void OnMainUiCreateSessionFailed()
        {
            if (!ShouldEnterGameplayViaMainUI) return;

            m_mainUI.ResetToStart();
            MainMenuUI.Show();
            UpdateAnswerInputFieldInteractability(true);
            ClearAnswerInputField();
            FocusAnswerInputFieldNextFrame();
        }

        /// <summary>Main UI join flow showed Loading while joining; on failure return to RoomId so the player can re-enter a room word.</summary>
        public void OnMainUiJoinSessionFailed()
        {
            if (!ShouldEnterGameplayViaMainUI) return;

            m_mainUI.TransitionToRoomId();
            UpdateAnswerInputFieldInteractability(true);
            ClearAnswerInputField();
            FocusAnswerInputFieldNextFrame();
        }

        /// <summary>MainMenuUI end-edit: match create/join commands (case-insensitive, trimmed).</summary>
        public void TryProcessMainMenuCommand(string rawContent)
        {
            if (string.IsNullOrWhiteSpace(rawContent))
                return;

            var key = rawContent.Trim().ToLowerInvariant();
            var create = (m_mainMenuCreateCommand ?? "create").Trim().ToLowerInvariant();
            var join = (m_mainMenuJoinCommand ?? "join").Trim().ToLowerInvariant();

            if (key == create)
            {
                MainMenuUI.Hide();
                ConnectionScreenUI.Hide();
                TransitionMainUiToLoadingIfPresent();
                ConnectionScreenUI.TriggerCreateSession();
                return;
            }

            if (key == join)
            {
                MainMenuUI.Hide();
                ConnectionScreenUI.Hide();
                if (m_mainUI != null)
                    m_mainUI.TransitionToRoomId();
                else
                    EnterConnectionScreen();
                return;
            }

            // Main UI start screen: typed command is not create/join — clear and re-focus the shared field.
            if (ShouldEnterGameplayViaMainUI && m_mainUI.CurrentState == MainUIController.MainUIState.Start)
            {
                ClearAnswerInputField();
                FocusAnswerInputFieldNextFrame();
            }
        }

        public void EnterGameScreen()
        {
            StartCoroutine(DelayEnterGameScreen());
        }
        
        private const float k_delayEnterGameScreenInSeconds = 0.2f;

        private IEnumerator DelayEnterGameScreen()
        {
            yield return new WaitForSeconds(k_delayEnterGameScreenInSeconds);
            ConnectionScreenUI.Hide();
            WaitingScreenUI.Hide();
            ResolutionScreenUI.Hide();
            WinScreenUI.Hide();

            if (ShouldEnterGameplayViaMainUI)
            {
                GameScreenUI.Hide();
                m_mainUI.TransitionToGameplay();
                var field = m_mainUI.SharedAnswerInputField;
                if (field != null)
                    field.text = string.Empty;
                else
                    Debug.LogWarning("UIManager: m_useMainUIForGameplay is on but MainUIController has no SharedAnswerInputField assigned.");
            }
            else
            {
                if (m_useMainUIForGameplay && m_mainUI == null)
                    Debug.LogWarning("UIManager: m_useMainUIForGameplay is on but m_mainUI is not assigned; using legacy GameScreenUI.");
                GameScreenUI.Show();
                GameScreenUI.ClearWordInputField();
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayTypingMusic();
            }
        }

        public void EnterResolutionScreen()
        {
            GameScreenUI.Hide();
            ResolutionScreenUI.Show();
        }

        /// <summary>
        /// Called when <see cref="RoundManager"/> enters the resolution phase (timeout or both submits). Uses MainUI round-result transition when <see cref="ShouldEnterGameplayViaMainUI"/>; otherwise legacy resolution screen.
        /// </summary>
        public void EnterResolutionPhaseFromRound(string hostAnswer, string clientAnswer, int hostHpAfter, int clientHpAfter, bool hostAnswerLetterEligible, bool clientAnswerLetterEligible)
        {
            if (ShouldEnterGameplayViaMainUI)
            {
                GameScreenUI.Hide();
                ResolutionScreenUI.Hide();

                m_mainUI.BeginResolutionScoreSyncThenRoundResult(hostAnswer, clientAnswer, hostHpAfter, clientHpAfter, hostAnswerLetterEligible, clientAnswerLetterEligible);
                return;
            }

            EnterResolutionScreen();
            UpdateResolutionPressSpaceHintText("press \"space\" to continue ");
            UpdateP1ResolutionScreenAnswerText(hostAnswer);
            UpdateP2ResolutionScreenAnswerText(clientAnswer);
        }

        public void EnterWinScreen()
        {
            WinScreenUI.Show();
        }

        public void UpdateWinText(string text)
        {
            WinScreenUI.UpdateWinText(text);
        }

        #region GameScreen UI
        
        public void SetP1()
        {
            GameScreenUI.SetP1();
        }

        public void SetP2()
        {
            GameScreenUI.SetP2();
        }

        public void UpdateP1LettersCountUI(int lettersCount, bool isOwner )
        {
            if (UsesMainUiGameplayFlow)
                m_mainUI.UpdateGameplayP1LetterBlocks(lettersCount, isOwner);
            else
                GameScreenUI.UpdateP1LettersCountUI(lettersCount, isOwner);
        }

        public void UpdateP2LettersCountUI(int lettersCount, bool isOwner)
        {
            if (UsesMainUiGameplayFlow)
                m_mainUI.UpdateGameplayP2LetterBlocks(lettersCount, isOwner);
            else
                GameScreenUI.UpdateP2LettersCountUI(lettersCount, isOwner);
        }

        /// <summary>
        /// Invalid submit clears the TMP field with SetTextWithoutNotify, so <see cref="Client.LetterCount"/> and MainUI letter blocks can stay stale. Call from local owner after clearing the answer.
        /// </summary>
        public void SyncMainUiGameplayLetterRowsAfterLocalAnswerCleared()
        {
            if (!UsesMainUiGameplayFlow || m_mainUI == null) return;

            Client localOwner = null;
            foreach (var c in FindObjectsByType<Client>(FindObjectsSortMode.InstanceID))
            {
                if (c.IsOwner)
                {
                    localOwner = c;
                    break;
                }
            }

            if (localOwner == null) return;

            Client other = null;
            foreach (var c in FindObjectsByType<Client>(FindObjectsSortMode.InstanceID))
            {
                if (c != localOwner)
                {
                    other = c;
                    break;
                }
            }

            m_mainUI.ResetGameplaySharedInputLetterPreviewForRefresh();

            if (localOwner.IsHost)
            {
                UpdateP1LettersCountUI(0, true);
                if (other != null)
                    UpdateP2LettersCountUI(other.LetterCount.Value, false);
            }
            else
            {
                if (other != null)
                    UpdateP1LettersCountUI(other.LetterCount.Value, false);
                UpdateP2LettersCountUI(0, true);
            }
        }

        public void UpdateCurrentPrompt(string prompt)
        {
            GameScreenUI.UpdateCurrentPrompt(GetTextWithTransparentColor(prompt.ToLower()));
        }

        public void UpdateGameScreenTimer(float timeT, bool roundTimeAccelerated)
        {
            if (UsesMainUiGameplayFlow)
                m_mainUI.UpdateGameplayRoundTimer(timeT, roundTimeAccelerated);
            else
                GameScreenUI.UpdateTimer(timeT, roundTimeAccelerated);
        }

        public void AddListenerToAnswerInputField(UnityAction<string> onWordSubmit)
        {
            var field = AnswerInputField;
            if (field != null)
                field.onValueChanged.AddListener(onWordSubmit);
        }

        public void RemoveListenerFromAnswerInputField(UnityAction<string> onWordSubmit)
        {
            var field = AnswerInputField;
            if (field != null)
                field.onValueChanged.RemoveListener(onWordSubmit);
        }

        public void UpdateAnswerInputField(string answerText)
        {
            var field = AnswerInputField;
            if (field == null) return;

            field.SetTextWithoutNotify(GetTextWithTransparentColor(answerText));
            if (m_answerInputCaretCoroutine != null)
                StopCoroutine(m_answerInputCaretCoroutine);
            m_answerInputCaretCoroutine = StartCoroutine(MoveAnswerCaretToEndNextFrame(field));
        }

        IEnumerator MoveAnswerCaretToEndNextFrame(TMP_InputField field)
        {
            yield return null;
            if (field != null)
                field.MoveTextEnd(false);
            m_answerInputCaretCoroutine = null;
        }

        public void UpdateAnswerInputFieldInteractability(bool interactable)
        {
            var field = AnswerInputField;
            if (field == null) return;

            field.interactable = interactable;
            field.readOnly = !interactable;
            if (!interactable)
                field.DeactivateInputField();
        }

        public void SetAnswerInputEnabled(bool enabled)
        {
            var field = AnswerInputField;
            if (field != null)
                field.enabled = enabled;
        }

        public void SetAnswerInputReadOnly(bool readOnly)
        {
            var field = AnswerInputField;
            if (field != null)
                field.readOnly = readOnly;
        }

        public void ClearAnswerInputField()
        {
            var field = AnswerInputField;
            if (field != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[UIManager] ClearAnswerInputField (before len={field.text?.Length ?? 0})");
#endif
                field.SetTextWithoutNotify(string.Empty);
            }
        }

        public void FocusAnswerInputField()
        {
            var field = AnswerInputField;
            if (field == null) return;

            field.enabled = true;
            field.interactable = true;
            field.readOnly = false;

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(field.gameObject);

            field.Select();
            field.ActivateInputField();
        }

        /// <summary>
        /// TMP often needs one frame after <see cref="SetTextWithoutNotify"/> / clear before <see cref="TMP_InputField.ActivateInputField"/> sticks.
        /// </summary>
        public void FocusAnswerInputFieldNextFrame()
        {
            StartCoroutine(FocusAnswerInputFieldNextFrameRoutine());
        }

        IEnumerator FocusAnswerInputFieldNextFrameRoutine()
        {
            yield return null;
            FocusAnswerInputField();
        }

        public void AddSubmitListenerToAnswerInputField(UnityAction<string> onSubmit)
        {
            var field = AnswerInputField;
            if (field != null)
                field.onSubmit.AddListener(onSubmit);
        }

        public void RemoveSubmitListenerFromAnswerInputField(UnityAction<string> onSubmit)
        {
            var field = AnswerInputField;
            if (field != null)
                field.onSubmit.RemoveListener(onSubmit);
        }

        public void SetMainUiPrompt(string promptText, string bannedLetters)
        {
            if (m_mainUI != null)
            {
                m_mainUI.SetPromptForShowcase(promptText, bannedLetters);
                m_mainUI.NotifyPromptReceivedFromServer();
            }
        }

        public void EnterMainUiLoadingHoldForPromptIfPresent()
        {
            if (m_mainUI != null)
                m_mainUI.EnterLoadingHoldForPrompt();
        }

        /// <summary>
        /// After resolution ends and the next round starts on the server: return to loading + wait for new prompt, then the same Loading → PromptShowcase → Gameplay path as match start.
        /// </summary>
        public void BeginMainUiNextRoundAfterResolution()
        {
            if (!UsesMainUiGameplayFlow) return;
            m_mainUI.BeginNextRoundFromResolution();
            UpdateAnswerInputFieldInteractability(false);
        }

        /// <summary>
        /// End of match: legacy win screen, or MainUI game-end state when using shared MainUI gameplay.
        /// </summary>
        public void EnterWinScreenOrMainUiGameEnd(string playerID)
        {
            var localIdStr = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.LocalClientId.ToString()
                : string.Empty;
            string winText;
            if (playerID == localIdStr)
                winText = "you win";
            else if (playerID == "both" || playerID == "draw")
                winText = "both win";
            else
                winText = "opponent wins";

            if (UsesMainUiGameplayFlow)
            {
                GameScreenUI.Hide();
                ResolutionScreenUI.Hide();
                m_mainUI.TransitionToGameEnd();
                UpdateWinText(winText);
                return;
            }

            EnterWinScreen();
            UpdateWinText(winText);
        }

        public void UpdateBannedLettersText(string invalidLetters, bool isHide = false)
        {
            GameScreenUI.UpdateInvalidLettersText(invalidLetters, isHide);
        }
        
        public void UpdateGameScreenHintText(string hint)
        {
            if (UsesMainUiGameplayFlow)
            {
                if (m_mainUI != null)
                    m_mainUI.SetGameplayHintText(hint);
                return;
            }

            GameScreenUI.UpdateHintText(hint);
        }

        #endregion


        #region ResolutionScreen UI

        public void UpdateP1ResolutionScreenAnswerText(string text)
        {
            Debug.Log($"UpdateP1ResolutionScreenAnswerText: {text}");
            ResolutionScreenUI.UpdateP1AnswerText((text));
        }

        public void UpdateP2ResolutionScreenAnswerText(string text)
        {
            Debug.Log($"UpdateP2ResolutionScreenAnswerText: {text}");
            ResolutionScreenUI.UpdateP2AnswerText((text));
        }

        public void ResolutionScreenSetP1()
        {
            ResolutionScreenUI.SetP1();
        }

        public void ResolutionScreenSetP2()
        {
            ResolutionScreenUI.SetP2();
        }

        public void UpdateResolutionPressSpaceHintText(string content)
        {
            ResolutionScreenUI.UpdateResolutionPressSpaceHintText((content));
        }

        public void UpdatePlayer1FillImage(float fill, int currentHp)
        {
            ResolutionScreenUI.UpdatePlayer1FillImage(fill, currentHp);
        }

        public void UpdatePlayer2FillImage(float fill, int currentScore)
        {
            ResolutionScreenUI.UpdatePlayer2FillImage(fill, currentScore);
        }

        public void UpdatePlayerFillImage(bool isHost, int thisClientScore, int otherClientScore)
        {
            float maxScore = GameManager.Instance.MaxPlayerHp;
            if (isHost)
            {
                UpdatePlayer1FillImage(thisClientScore / maxScore, thisClientScore);
                UpdatePlayer2FillImage(otherClientScore / maxScore, otherClientScore);
            }
            else
            {
                UpdatePlayer2FillImage(thisClientScore / maxScore, thisClientScore);
                UpdatePlayer1FillImage(otherClientScore / maxScore, otherClientScore);
            }
        }

        #endregion

        private void Update()
        {
            if (m_awaitingSpaceOnTutorialScreen && TutorialScreenUI != null && TutorialScreenUI.isActiveAndEnabled)
            {
                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                    TransitionFromTutorialToRoomId();
            }
        }

        private string m_bannedLetters;
        public string BannedLetters => m_bannedLetters;

        public void MarkBannedLetters(string bannedLetters)
        {
            m_bannedLetters = bannedLetters;
            
            Debug.Log($"MarkBannedLetters: {bannedLetters}");
        }

        public string GetTextWithTransparentColor(string text)
        {
            if (text == null) return null;
            
            string result = "";
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (m_bannedLetters != null &&
                    c != ' ' &&
                    m_bannedLetters.ToLower().Contains(char.ToLower(c)))
                {
                    result += $"<color=#A59D98AA>{c}</color>";
                }
                else
                {
                    result += c;
                }
            }


            return result;
        }
        
        public string RemoveColorTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            text = Regex.Replace(text, "<color=.*?>", "");
            text = text.Replace("</color>", "");

            return text;
        }
    }
}