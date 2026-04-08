using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class ClashScreenUI : MonoBehaviour
    {
        [SerializeField] private GameObject AnnouncementPanel;
        [SerializeField] private TMP_Text AnnouncementText;
        [SerializeField] private GameObject TypingPanel;
        [SerializeField] private TMP_Text ClashWordText;
        public TMP_InputField ClashInputField;
        [SerializeField] private TMP_Text HintText;

        public void ShowAnnouncement(string word)
        {
            gameObject.SetActive(true);
            AnnouncementPanel.SetActive(true);
            TypingPanel.SetActive(false);
            AnnouncementText.text = "CLASH!";
        }

        public void ShowTyping(string word)
        {
            AnnouncementPanel.SetActive(false);
            TypingPanel.SetActive(true);
            ClashWordText.text = word;
            HintText.text = "type the word as fast as you can!";
            ClashInputField.text = "";
            ClashInputField.interactable = true;
            ClashInputField.ActivateInputField();
        }

        public void DisableInput()
        {
            ClashInputField.interactable = false;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void ClearInput()
        {
            ClashInputField.text = "";
        }
    }
}
