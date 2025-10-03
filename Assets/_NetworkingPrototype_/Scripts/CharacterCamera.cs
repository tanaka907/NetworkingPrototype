using System;
using UnityEngine;

namespace NetworkingPrototype
{
    public class CharacterCamera : MonoBehaviour
    {
        [SerializeField] private Transform m_Target;
        [SerializeField] private Config m_Config = new();

        public ref Input input => ref m_Input;
        public ref State state => ref m_State;
        public Config config => m_Config;
        
        private Input m_Input;
        private State m_State;

        private void Awake()
        {
            m_State = new State
            {
                zoom = config.defaultZoom,
                targetZoom = config.defaultZoom,
                obstructionOffset = config.defaultZoom
            };
        }

        public void SetTarget(Transform newTarget)
        {
            m_Target = newTarget;
            
            if (m_Target == null)
                return;
            
            state.pivot = GetTargetPivot();
            UpdateObstruction(0f);
        }

        public void UpdateLook(float deltaTime)
        {
            if (m_Target == null) 
                return;

            state.lookSpeed = MathUtility.Decay(state.lookSpeed, input.look, config.rotationDecay, deltaTime);
            state.yaw += state.lookSpeed.x * config.sensitivity;
            state.pitch -= state.lookSpeed.y * config.sensitivity;
            state.pitch = Mathf.Clamp(state.pitch, config.minPitch, config.maxPitch);

            state.targetZoom = Mathf.Clamp(state.targetZoom - input.zoom * config.zoomSpeed, config.minZoom, config.maxZoom);
            state.zoom = MathUtility.Decay(state.zoom, state.targetZoom, config.zoomDecay, deltaTime);
            state.pivot = MathUtility.Decay(state.pivot, GetTargetPivot(), config.positionDecay, deltaTime);
        }

        public void UpdateObstruction(float deltaTime)
        {
            var cameraRay = new Ray(state.pivot, state.LookRotation() * -Vector3.forward);
            if (Physics.SphereCast(cameraRay, config.obstructionRadius, out var obstructionHit, state.zoom, config.obstructionMask))
            {
                if (obstructionHit.distance < state.obstructionOffset)
                    state.obstructionOffset = obstructionHit.distance;
                else
                    state.obstructionOffset = MathUtility.Decay(state.obstructionOffset, obstructionHit.distance, config.obstructionDecay, deltaTime);

                state.isObstructed = true;
            }
            else
            {
                state.obstructionOffset = MathUtility.Decay(state.obstructionOffset, state.zoom, config.obstructionDecay, deltaTime);
                state.isObstructed = false;
            }

            transform.localRotation = state.LookRotation();
            transform.localPosition = state.pivot + state.LookRotation() * new Vector3(0f, 0f, -state.obstructionOffset);
        }

        private Vector3 GetTargetPivot()
        {
            return m_Target.position + state.YawRotation() * config.offset;
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
                return;

            if (m_Target != null)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(GetTargetPivot(), state.pivot);
            }

            Gizmos.color = state.isObstructed ? Color.red : Color.gray;
            Gizmos.DrawLine(state.pivot, transform.localPosition);
            Gizmos.DrawWireSphere(transform.localPosition, config.obstructionRadius);
        }

        [Serializable]
        public struct Input
        {
            public Vector2 look;
            public float zoom;
        }

        [Serializable]
        public struct State
        {
            public Vector2 lookSpeed;
            public float yaw;
            public float pitch;
            public float zoom;
            public float targetZoom;
            public float obstructionOffset;
            public Vector3 pivot;
            public bool isObstructed;

            public Quaternion YawRotation() => Quaternion.Euler(0f, yaw, 0f);
            public Quaternion LookRotation() => Quaternion.Euler(pitch, yaw, 0f);
        }

        [Serializable]
        public class Config
        {
            public float sensitivity = 0.05f;
            public float zoomSpeed = 1f;
            public float zoomDecay = 16f;
            public float minZoom = 1.4f;
            public float maxZoom = 10f;
            public float defaultZoom = 3f;
            public float minPitch = -60f;
            public float maxPitch = 89;
            public float rotationDecay = 16f;
            public float positionDecay = 10f;
            public Vector3 offset = new(0f, 1.3f, 0f);
            public float obstructionRadius = 0.39f;
            public float obstructionDecay = 4f;
            public LayerMask obstructionMask = 1 << 0;
        }
    }
}