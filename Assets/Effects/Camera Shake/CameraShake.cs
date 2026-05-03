using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace Effects
{
    public class CameraShake : MonoBehaviour
    {
        [Header("Default Shake Settings")] public float DefaultDuration = 0.25f;
        public float DefaultPositionStrength = 0.2f;
        public float DefaultRotationStrength = 2f;

        public AnimationCurve Falloff = AnimationCurve.EaseInOut(0, 1, 1, 0);

        private Vector3 m_originalLocalPos;
        private Quaternion m_originalLocalRot;

        private float m_timeRemaining;
        private float m_totalTime;

        private float m_posStrength;
        private float m_rotStrength;

        private float m_noiseSeed;

        void Awake()
        {
            m_originalLocalPos = transform.localPosition;
            m_originalLocalRot = transform.localRotation;
            m_noiseSeed = Random.value * 1000f;
        }

        private void OnEnable()
        {
        }

        private void OnDisable()
        {
        }

        private void Update()
        {
            
        }

        void LateUpdate()
        {
            if (m_timeRemaining <= 0f)
                return;

            m_timeRemaining -= Time.deltaTime;

            float t = m_totalTime > 0f ? 1f - (m_timeRemaining / m_totalTime) : 1f;
            float strength = Falloff.Evaluate(Mathf.Clamp01(t));

            float x = (Mathf.PerlinNoise(m_noiseSeed, Time.time * 30f) - 0.5f) * 2f;
            float y = 0;
            float z = 0;

            Vector3 posOffset = new Vector3(x, y, z) * (m_posStrength * strength);
            Vector3 rotOffset = new Vector3(y, x, z) * (m_rotStrength * strength);

            transform.localPosition = m_originalLocalPos + posOffset;
            transform.localRotation = m_originalLocalRot * Quaternion.Euler(rotOffset);

            if (m_timeRemaining <= 0f)
            {
                transform.localPosition = m_originalLocalPos;
                transform.localRotation = m_originalLocalRot;
            }
        }

        public void Shake(float duration, float posStrength, float rotStrength)
        {
            m_totalTime = duration;
            m_timeRemaining = duration;

            m_posStrength = posStrength;
            m_rotStrength = rotStrength;
        }

        public void Shake()
        {
            Shake(DefaultDuration, DefaultPositionStrength, DefaultRotationStrength);
        }
    }
}