using TMPro;
using UnityEngine;

namespace UI
{
    public class RoomIdScreenUI : MonoBehaviour
    {
        [SerializeField] ConnectionScreenUI connectionScreenUI;

        public void Show()
        {
            gameObject.SetActive(true);
            UIManager.Instance?.FocusAnswerInputField();
        }

        public void Hide() => gameObject.SetActive(false);

        void OnEnable()
        {
            UIManager.Instance?.AddSubmitListenerToAnswerInputField(OnRoomCodeSubmit);
        }

        void OnDisable()
        {
            UIManager.Instance?.RemoveSubmitListenerFromAnswerInputField(OnRoomCodeSubmit);
        }

        void OnRoomCodeSubmit(string _)
        {
            Debug.Log(
                $"[RoomIdScreenUI] OnRoomCodeSubmit arg={_ ?? "<null>"} frame={Time.frameCount} " +
                $"hasField={UIManager.Instance != null && UIManager.Instance.AnswerInputField != null} connNull={connectionScreenUI == null}");

            var field = UIManager.Instance != null ? UIManager.Instance.AnswerInputField : null;
            if (field == null || connectionScreenUI == null)
            {
                Debug.LogError("RoomIdScreenUI: UIManager.AnswerInputField or connectionScreenUI missing.");
                return;
            }

            var word = field.text;
            Debug.Log(
                $"[RoomIdScreenUI] text len={word?.Length ?? 0} isEmpty={string.IsNullOrEmpty(word)} " +
                $"enabled={field.enabled} readOnly={field.readOnly} " +
                $"isFocused={field.isFocused}");

            if (string.IsNullOrWhiteSpace(word))
            {
                Debug.Log("[RoomIdScreenUI] skip join: whitespace/empty text");
                return;
            }

            Debug.Log($"[RoomIdScreenUI] TriggerJoinSession -> {word}");
            connectionScreenUI.TriggerJoinSession(word);
        }
    }
}
