using UnityEngine;
using UnityEngine.EventSystems;

namespace UI
{
    public class ClosePopup : MonoBehaviour
    {

        private void OnEnable() { EventSystem.current.SetSelectedGameObject(gameObject); }
        private void Update()
        {
            if (Input.anyKeyDown) gameObject.SetActive(false);
        }
    }
}
