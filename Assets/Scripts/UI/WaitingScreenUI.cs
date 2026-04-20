using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class WaitingScreenUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text ROOMIDText;
        [SerializeField] private Button CopyBtn;

        private string _connectionCode;

        private void OnEnable() { CopyBtn.onClick.AddListener(OnCopyClick); }
        private void OnDisable() { CopyBtn.onClick.RemoveListener(OnCopyClick); }
        private void OnCopyClick() { GUIUtility.systemCopyBuffer = _connectionCode; }

        public void Show(string roomName, string connectionCode)
        {
            _connectionCode = connectionCode;
            ROOMIDText.text = $"room: <color=white>{roomName}</color>  code: <color=white>{connectionCode}</color>";
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}