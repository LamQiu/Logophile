using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UI
{
    public class GameScreenUI : MonoBehaviour
    {
        [SerializeField] private GameObject P1BG;
        [SerializeField] private GameObject P2BG;
        [SerializeField] private TMP_Text Player1NameText;
        [SerializeField] private TMP_Text Player2NameText;
        [SerializeField] private Color PlayerActiveTextColor;
        [SerializeField] private Color PlayerInactiveTextColor;
        [SerializeField] private GameObject P1LettersCountUI;
        [SerializeField] private GameObject P2LettersCountUI;
        [SerializeField] private TMP_Text CurrentPromptText;
        [SerializeField] private Image TimerImage;
        [SerializeField] private Color RoundTimerBarNormalColor = new Color(0.52156866f, 0.52156866f, 0.52156866f, 1f);
        [SerializeField] private Color RoundTimerBarAcceleratedColor = new Color(1f, 0.45f, 0.2f, 1f);
        public TMP_InputField AnswerInputField;
        [SerializeField] private TMP_Text InvalidLettersText;
        [SerializeField] private TMP_Text ResolutionInvalidLettersText;
        [SerializeField] private TMP_Text HintText;


        public void Show()
        {
            gameObject.SetActive(true);
            AnswerInputField.ActivateInputField();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetP1()
        {
            P1BG.gameObject.SetActive(true);
            P2BG.gameObject.SetActive(false);
            Player1NameText.color = PlayerActiveTextColor;
            Player2NameText.color = PlayerInactiveTextColor;
        }

        public void SetP2()
        {
            P1BG.gameObject.SetActive(false);
            P2BG.gameObject.SetActive(true);
            Player1NameText.color = PlayerInactiveTextColor;
            Player2NameText.color = PlayerActiveTextColor;
        }

        public void UpdateP1LettersCountUI(int lettersCount, bool isOwner)
        {
            UpdateLettersCountUI(P1LettersCountUI, lettersCount, isOwner);
        }

        public void UpdateP2LettersCountUI(int lettersCount, bool isOwner)
        {
            UpdateLettersCountUI(P2LettersCountUI, lettersCount, isOwner);
        }

        private const float k_isOwnerLetterCountScaleY = 2.5f;

        private void UpdateLettersCountUI(GameObject letterCountUI, int lettersCount, bool isOwner)
        {
            for (int i = 0; i < letterCountUI.transform.childCount; i++)
            {
                if (i < lettersCount)
                {
                    letterCountUI.transform.GetChild(i).gameObject.SetActive(true);
                    float scaleY = isOwner ? k_isOwnerLetterCountScaleY : 1;
                    letterCountUI.transform.localScale = new Vector3(1, scaleY, 1);
                }
                else
                {
                    letterCountUI.transform.GetChild(i).gameObject.SetActive(false);
                }
            }
        }

        public void UpdateCurrentPrompt(string prompt)
        {
            CurrentPromptText.text = prompt;
        }

        public void UpdateTimer(float timeT, bool roundTimeAccelerated)
        {
            TimerImage.fillAmount = timeT;
            TimerImage.color = roundTimeAccelerated ? RoundTimerBarAcceleratedColor : RoundTimerBarNormalColor;
        }

        public void AddListenerToAnswerInputField(UnityAction<string> onWordSubmit)
        {
            AnswerInputField.onValueChanged.AddListener(onWordSubmit);
        }

        private Coroutine m_wordInputDisplaySyncCoroutine;

        public void UpdateAnswerInputField(string content)
        {
            AnswerInputField.SetTextWithoutNotify(content);
            if (m_wordInputDisplaySyncCoroutine != null) StopCoroutine(m_wordInputDisplaySyncCoroutine);
            if (gameObject.activeSelf)
                m_wordInputDisplaySyncCoroutine = StartCoroutine(FixCaret());
        }

        private IEnumerator FixCaret()
        {
            yield return null;
            AnswerInputField.MoveTextEnd(false);
        }

        public void ClearWordInputField()
        {
            AnswerInputField.text = "";
        }

        public void UpdateAnswerInputFieldInteractability(bool interactable)
        {
            AnswerInputField.interactable = interactable;
        }

        public void UpdateInvalidLettersText(string invalidLetters, bool isHide = false)
        {
            string spaced = string.Join("  ", invalidLetters.ToLower().ToCharArray());
            string prefix = isHide ? "" : UIManager.Instance.GetTextWithTransparentColor("banned letter:  ");
            string spacedWithTransparentColor = UIManager.Instance.GetTextWithTransparentColor(spaced);

            Debug.Log($"UpdateInvalidLettersText: {prefix + spacedWithTransparentColor}");
            
            InvalidLettersText.text = prefix + spacedWithTransparentColor;
            ResolutionInvalidLettersText.text = prefix + spacedWithTransparentColor;
        }
        
        public void UpdateHintText(string hint)
        {
            HintText.text = hint;
        }

        private void Update()
        {
            if (gameObject.activeSelf)
            {
                AnswerInputField.Select();
                AnswerInputField.ActivateInputField();
            }
        }
    }
}