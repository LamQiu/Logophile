using UnityEngine;

namespace UI
{
    public class TutorialScreenUI : MonoBehaviour
    {
        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);
    }
}
