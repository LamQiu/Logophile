using System.Threading.Tasks;
using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class ConnectionScreenUI : MonoBehaviour
    {
        [SerializeField] private TMP_InputField CreateSessionCodeInputField;
        [SerializeField] private GameObject CreateSessionWidget;
        [SerializeField] private TMP_InputField CreateSessionWidgetInputField;
        [SerializeField] private Button CreateSessionButton;
        [SerializeField] private GameObject QuickJoinWidget;
        [SerializeField] private TMP_InputField QuickJoinWidgetInputField;
        [SerializeField] private Button QuickJoinButton;
        [SerializeField] private GameObject JoinErrorPopup;

        private void OnEnable()
        {
            CreateSessionCodeInputField.onSubmit.AddListener(OnCreateSubmit);
            QuickJoinWidgetInputField.onSubmit.AddListener(OnJoinSubmit);
        }

        private void OnDisable()
        {
            CreateSessionCodeInputField.onSubmit.RemoveListener(OnCreateSubmit);
            QuickJoinWidgetInputField.onSubmit.RemoveListener(OnJoinSubmit);
        }

        private void OnCreateSubmit(string name) { var __ = CreateSessionAsync(name); }

        private void OnJoinSubmit(string _) { var __ = JoinSessionAsync(QuickJoinWidgetInputField.text); }

        private async Task CreateSessionAsync(string sessionName)
        {
            var options = new SessionOptions { MaxPlayers = 2, Type = "default-session", Name = sessionName }
                .WithRelayNetwork()
                .WithPlayerName();
            var session = await MultiplayerService.Instance.CreateSessionAsync(options);
            Debug.Log($"[Room Created] Code: {session.Code}");
            UIManager.Instance.EnterWaitingScreen(session.Name, session.Code);
        }

        private async Task JoinSessionAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            var joinOptions = new JoinSessionOptions { Type = "default-session" }.WithPlayerName();
            try
            {
                var session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, joinOptions);
                UIManager.Instance.EnterWaitingScreen(session.Name, code);
            }
            catch (SessionException e)
            {
                Debug.LogError($"[Join Failed] {e.Message}");
                JoinErrorPopup.SetActive(true);
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
            CreateSessionCodeInputField.ActivateInputField();
            // CreateSessionWidget.SetActive(true);
            // QuickJoinWidget.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            CreateSessionWidget.SetActive(false);
            QuickJoinWidget.SetActive(false);
        }
    }
}
