using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace UI
{
    public class UIManager : Singleton<UIManager>
    {
        [SerializeField] private MainMenuUI MainMenuUI;
        [SerializeField] private ConnectionScreenUI ConnectionScreenUI;
        [SerializeField] private WaitingScreenUI WaitingScreenUI;
        [SerializeField] private GameScreenUI GameScreenUI;
        [SerializeField] private ResolutionScreenUI ResolutionScreenUI;
        [SerializeField] private WinScreenUI WinScreenUI;
        public GameObject IsNotToBanLetterIcon;
        public string MainMenuCommandInputFieldEnterPlayKey = "play";
        
        public TMP_InputField AnswerInputField => GameScreenUI.AnswerInputField;
        
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
            GameScreenUI.Hide();
            ResolutionScreenUI.Hide();
            WinScreenUI.Hide();
            
            ResolutionScreenUI.Reset();
        }

        private void Start()
        {
            ResetUI();
            MainMenuUI.Show();
        }

        public void EnterConnectionScreen()
        {
            MainMenuUI.Hide();
            ConnectionScreenUI.Show();
        }

        public void EnterWaitingScreen(string roomID)
        {
            ConnectionScreenUI.Hide();
            WaitingScreenUI.Show(roomID);
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
            GameScreenUI.Show();
            GameScreenUI.ClearWordInputField();
        }

        public void EnterResolutionScreen()
        {
            GameScreenUI.Hide();
            ResolutionScreenUI.Show();
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
            GameScreenUI.UpdateP1LettersCountUI(lettersCount, isOwner);
        }

        public void UpdateP2LettersCountUI(int lettersCount, bool isOwner)
        {
            GameScreenUI.UpdateP2LettersCountUI(lettersCount, isOwner);
        }

        public void UpdateCurrentPrompt(string prompt)
        {
            GameScreenUI.UpdateCurrentPrompt(GetTextWithTransparentColor(prompt.ToLower()));
        }

        public void UpdateGameScreenTimer(float timeT)
        {
            GameScreenUI.UpdateTimer(timeT);
        }

        public void AddListenerToAnswerInputField(UnityAction<string> onWordSubmit)
        {
            GameScreenUI.AddListenerToAnswerInputField(onWordSubmit);
        }

        public void UpdateAnswerInputField(string answerText)
        {
            GameScreenUI.UpdateAnswerInputField(GetTextWithTransparentColor(answerText));
        }

        public void UpdateAnswerInputFieldInteractability(bool interactable)
        {
            GameScreenUI.UpdateAnswerInputFieldInteractability(interactable);
        }

        public void UpdateBannedLettersText(string invalidLetters, bool isHide = false)
        {
            GameScreenUI.UpdateInvalidLettersText(invalidLetters, isHide);
        }
        
        public void UpdateGameScreenHintText(string hint)
        {
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

        private bool m_isGameStarted = false;

        private void Update()
        {
            if (!m_isGameStarted && NetworkManager.Singleton.ConnectedClients.Count == 2)
            {
                m_isGameStarted = true;
                EnterGameScreen();
            }
        }

        private string m_bannedLetters;
        public string BannedLetters => m_bannedLetters;

        public void MarkBannedLetters(string bannedLetters)
        {
            m_bannedLetters = bannedLetters;
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