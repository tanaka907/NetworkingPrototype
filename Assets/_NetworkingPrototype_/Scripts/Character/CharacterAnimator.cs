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

        [SerializeField] private FootstepConfig m_Footsteps;
        [SerializeField] private Rig m_LookAtRig;
        [SerializeField] private Transform m_LookAtTarget;
        [SerializeField] private Transform m_Body;

        private Animator m_Animator;
        private Dictionary<Rig, Coroutine> m_RigCoroutineMap = new();

        private void Awake()
        {
            m_Animator = GetComponent<Animator>();
        }

        public void SetMoveVelocity(Vector3 worldVelocity)
        {
            var localVelocity = m_Body.InverseTransformVector(worldVelocity);
            m_Animator.SetFloat(FORWARD_SPEED, localVelocity.z, 0.15f, Time.deltaTime);
            m_Animator.SetFloat(RIGHT_SPEED, localVelocity.x, 0.15f, Time.deltaTime);
        }

        public void SetIsGrounded(bool isGrounded)
        {
            m_Animator.SetBool(IS_GROUNDED, isGrounded);
        }

        public void SetIsCrouching(bool isCrouching)
        {
            m_Animator.SetBool(IS_CROUCHING, isCrouching);
        }

        public void OnJump()
        {
            m_Animator.SetTrigger(JUMP);
            m_Footsteps.PlayJump(transform.position);
        }

        public void OnLand()
        {
            m_Animator.SetTrigger(LAND);
            m_Footsteps.PlayLand(transform.position);
        }

        public void SetLookAtDirection(Vector3 direction)
        {
            Debug.DrawRay(m_LookAtTarget.position, direction, Color.white);
            
            m_LookAtTarget.forward = ClampDirectionAngle(m_Body.forward, m_LookAtTarget.forward, 89f);
            var targetDirection = ClampDirectionAngle(m_Body.forward, direction, 89f);

            var turnSpeed = (1f - Mathf.Clamp01(Vector3.Angle(m_LookAtTarget.forward, targetDirection) / 180f)) * 20f;
            
            m_LookAtTarget.localRotation = Quaternion.Slerp(
                m_LookAtTarget.localRotation, 
                Quaternion.LookRotation(targetDirection), 
                turnSpeed * Time.deltaTime);
            
            Debug.DrawRay(m_LookAtTarget.position, targetDirection, Color.blue);
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
            SetRigWeightImmediate(m_LookAtRig, weight);
        }
        
        public void SetLookAtWeight(float weight, float duration)
        {
            SetRigWeight(m_LookAtRig, weight, duration);
        }

        private void SetRigWeightImmediate(Rig rig, float weight)
        {
            if (m_RigCoroutineMap.Remove(rig, out var coroutine))
                StopCoroutine(coroutine);
            
            rig.weight = weight;
        }

        private void SetRigWeight(Rig rig, float weight, float duration)
        {
            if (m_RigCoroutineMap.Remove(rig, out var coroutine))
                StopCoroutine(coroutine);

            m_RigCoroutineMap.Add(rig, StartCoroutine(SetRigWeightRoutine(rig, weight, duration)));
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
                    m_Footsteps.PlayWalk(transform.position);
                else
                    m_Footsteps.PlayRun(transform.position);
            }
        }
    }
}