using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Scripting;

namespace NetworkingPrototype
{
    public class CharacterAnimator : MonoBehaviour
    {
        private static readonly int FORWARD_SPEED = Animator.StringToHash("ForwardSpeed");
        private static readonly int RIGHT_SPEED = Animator.StringToHash("RightSpeed");
        private static readonly int IS_GROUNDED = Animator.StringToHash("IsGrounded");
        private static readonly int IS_CROUCHING = Animator.StringToHash("IsCrouching");
        private static readonly int JUMP = Animator.StringToHash("Jump");
        private static readonly int LAND = Animator.StringToHash("Land");

        [SerializeField] private FootstepConfig m_footsteps;
        [SerializeField] private Rig m_lookAtRig;
        [SerializeField] private Transform m_lookAtTarget;
        [SerializeField] private Transform m_body;

        private Animator m_animator;
        private Dictionary<Rig, Coroutine> m_rigCoroutineMap = new();

        private void Awake()
        {
            m_animator = GetComponent<Animator>();
        }

        public void SetMoveVelocity(Vector3 worldVelocity)
        {
            var localVelocity = m_body.InverseTransformVector(worldVelocity);
            m_animator.SetFloat(FORWARD_SPEED, localVelocity.z, 0.15f, Time.deltaTime);
            m_animator.SetFloat(RIGHT_SPEED, localVelocity.x, 0.15f, Time.deltaTime);
        }

        public void SetIsGrounded(bool isGrounded)
        {
            m_animator.SetBool(IS_GROUNDED, isGrounded);
        }

        public void SetIsCrouching(bool isCrouching)
        {
            m_animator.SetBool(IS_CROUCHING, isCrouching);
        }

        public void OnJump()
        {
            m_animator.SetTrigger(JUMP);
            m_footsteps.PlayJump(transform.position);
        }

        public void OnLand()
        {
            m_animator.SetTrigger(LAND);
            m_footsteps.PlayLand(transform.position);
        }

        public void SetLookAtDirection(Vector3 direction)
        {
            Debug.DrawRay(m_lookAtTarget.position, direction, Color.white);
            
            m_lookAtTarget.forward = ClampDirectionAngle(m_body.forward, m_lookAtTarget.forward, 89f);
            var targetDirection = ClampDirectionAngle(m_body.forward, direction, 89f);

            var turnSpeed = (1f - Mathf.Clamp01(Vector3.Angle(m_lookAtTarget.forward, targetDirection) / 180f)) * 20f;
            
            m_lookAtTarget.localRotation = Quaternion.Slerp(
                m_lookAtTarget.localRotation, 
                Quaternion.LookRotation(targetDirection), 
                turnSpeed * Time.deltaTime);
            
            Debug.DrawRay(m_lookAtTarget.position, targetDirection, Color.blue);
        }

        private static Vector3 ClampDirectionAngle(Vector3 baseDirection, Vector3 unclampedDirection, float maxAngle)
        {
            var clampedDirection = Vector3.RotateTowards(
                baseDirection, 
                new Vector3(unclampedDirection.x, 0f, unclampedDirection.z).normalized, 
                Mathf.Deg2Rad * maxAngle,
                0f);

            clampedDirection = clampedDirection.normalized * new Vector3(unclampedDirection.x, 0f, unclampedDirection.z).magnitude;
            clampedDirection.y = unclampedDirection.y;
            return clampedDirection.normalized;
        }
        
        public void SetLookAtWeightImmediate(float weight)
        {
            SetRigWeightImmediate(m_lookAtRig, weight);
        }
        
        public void SetLookAtWeight(float weight, float duration)
        {
            SetRigWeight(m_lookAtRig, weight, duration);
        }

        private void SetRigWeightImmediate(Rig rig, float weight)
        {
            if (m_rigCoroutineMap.Remove(rig, out var coroutine))
                StopCoroutine(coroutine);
            
            rig.weight = weight;
        }

        private void SetRigWeight(Rig rig, float weight, float duration)
        {
            if (m_rigCoroutineMap.Remove(rig, out var coroutine))
                StopCoroutine(coroutine);

            m_rigCoroutineMap.Add(rig, StartCoroutine(SetRigWeightRoutine(rig, weight, duration)));
        }

        private IEnumerator SetRigWeightRoutine(Rig rig, float weight, float duration)
        {
            var time = 0f;
            var fromWeight = rig.weight;

            while (time < duration)
            {
                var t = time / duration;
                rig.weight = Mathf.Lerp(fromWeight, weight, t);
                time += Time.deltaTime;
                yield return null;
            }

            rig.weight = weight;
        }

        [Preserve]
        private void OnFootstep(AnimationEvent evt)
        {
            if (evt.animatorClipInfo.weight > 0.5f)
            {
                if (evt.intParameter == 1)
                    m_footsteps.PlayWalk(transform.position);
                else
                    m_footsteps.PlayRun(transform.position);
            }
        }
    }
}