using System.Collections.Generic;
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
        [SerializeField] private GameObject QuickJoinWidget;
        [SerializeField] private TMP_InputField QuickJoinWidgetInputField;
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

        private void OnCreateSubmit(string _) { var __ = CreateSessionAsync(); }

        private void OnJoinSubmit(string _) { var __ = JoinSessionAsync(QuickJoinWidgetInputField.text); }

        private async Task CreateSessionAsync()
        {
            try
            {
                var word = await PickUniqueRoomWordAsync();
                CreateSessionCodeInputField.text = word;
                var options = new SessionOptions { MaxPlayers = 2, Type = "default-session", Name = word }
                    .WithRelayNetwork()
                    .WithPlayerName();
                var session = await MultiplayerService.Instance.CreateSessionAsync(options);
                Debug.Log($"[Room Created] Word: {session.Name}, Code: {session.Code}");
                UIManager.Instance.EnterWaitingScreen(session.Name, session.Name);
            }
            catch (SessionException e)
            {
                Debug.LogError($"[Create Session Failed] {e.Message}");
            }
        }

        // 最多尝试 10 次，找到一个当前没有对应 session 的词
        private async Task<string> PickUniqueRoomWordAsync()
        {
            for (int i = 0; i < 10; i++)
            {
                var candidate = RoomWordPicker.GetRandomWord();
                var result = await MultiplayerService.Instance.QuerySessionsAsync(
                    new QuerySessionsOptions
                    {
                        Count = 1,
                        FilterOptions = new List<FilterOption>
                        {
                            new FilterOption(FilterField.Name, candidate, FilterOperation.Equal)
                        }
                    });
                if (result.Sessions == null || result.Sessions.Count == 0)
                    return candidate;
            }
            return RoomWordPicker.GetRandomWord();
        }

        private async Task JoinSessionAsync(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return;
            var wordUpper = word.Trim().ToUpper();
            JoinErrorPopup.SetActive(false);
            var joinOptions = new JoinSessionOptions { Type = "default-session" }.WithPlayerName();
            try
            {
                var queryResult = await MultiplayerService.Instance.QuerySessionsAsync(
                    new QuerySessionsOptions
                    {
                        Count = 5,
                        FilterOptions = new List<FilterOption>
                        {
                            new FilterOption(FilterField.Name, wordUpper, FilterOperation.Equal),
                            new FilterOption(FilterField.AvailableSlots, "0", FilterOperation.Greater)
                        }
                    });

                if (queryResult.Sessions == null || queryResult.Sessions.Count == 0)
                {
                    JoinErrorPopup.SetActive(true);
                    return;
                }

                var sessionId = queryResult.Sessions[0].Id;
                var session = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId, joinOptions);
                UIManager.Instance.EnterWaitingScreen(session.Name, session.Name);
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
            QuickJoinWidgetInputField.ActivateInputField();
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
