using UnityEngine;
using UnityEngine.InputSystem;

public class UIShake : MonoBehaviour
{
    public RectTransform target;
    public float duration = 0.3f;
    public float strength = 10f;

    Vector2 originalPos;
    float timer;

    public void Shake()
    {
        originalPos = target.anchoredPosition;
        timer = duration;
    }

    void Update()
    {
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            Shake();
        }
        
        if (timer > 0)
        {
            timer -= Time.deltaTime;

            Vector2 offset = Random.insideUnitCircle * strength;
            target.anchoredPosition = originalPos + offset;
        }
        else
        {
            target.anchoredPosition = originalPos;
        }
    }
}