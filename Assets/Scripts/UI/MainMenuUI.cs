using TMPro;
using UnityEngine;

namespace UI
{
    public class MainMenuUI : MonoBehaviour
    {
        int _lastCommandHandledFrame = -1;
        bool _listenerRegistered;

        private void OnEnable()
        {
            TryRegisterListeners();
        }

        private void OnDisable()
        {
            TryUnregisterListeners();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            TryRegisterListeners();
            UIManager.Instance?.FocusAnswerInputField();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        void Update()
        {
            // UIManager / AnswerInputField may come online after this is enabled.
            if (!_listenerRegistered)
                TryRegisterListeners();
        }

        void TryRegisterListeners()
        {
            if (_listenerRegistered) return;
            if (UIManager.Instance == null) return;

            var field = UIManager.Instance.AnswerInputField;
            if (field == null) return;

            field.onSubmit.AddListener(OnCommandInputSubmitOrEndEdit);
            field.onEndEdit.AddListener(OnCommandInputSubmitOrEndEdit);
            _listenerRegistered = true;
        }

        void TryUnregisterListeners()
        {
            if (!_listenerRegistered) return;
            if (UIManager.Instance == null) { _listenerRegistered = false; return; }

            var field = UIManager.Instance.AnswerInputField;
            if (field != null)
            {
                field.onSubmit.RemoveListener(OnCommandInputSubmitOrEndEdit);
                field.onEndEdit.RemoveListener(OnCommandInputSubmitOrEndEdit);
            }

            _listenerRegistered = false;
        }

        void OnCommandInputSubmitOrEndEdit(string content)
        {
            if (UIManager.Instance == null) return;

            if (Time.frameCount == _lastCommandHandledFrame)
                return;

            _lastCommandHandledFrame = Time.frameCount;
            UIManager.Instance.TryProcessMainMenuCommand(content);
        }
    }
}
