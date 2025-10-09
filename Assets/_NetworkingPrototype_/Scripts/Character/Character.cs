using System;
using PurrNet.Prediction;
using UnityEngine;

namespace NetworkingPrototype
{
    public interface ICharacterInputProvider
    {
        void UpdateInput(ref Character.Input input);
    }
    
    [RequireComponent(typeof(IStats))]
    [RequireComponent(typeof(PredictedRigidbody))]
    public class Character : PredictedIdentity<Character.Input, Character.State>
    {
        [SerializeField] private Transform m_body;
        [SerializeField] private Config m_config;

        public Config config => m_config;

        private IStats m_stats;
        private PredictedRigidbody m_rigidbody;
        private CharacterAnimator m_animation;
        private CapsuleCollider m_capsuleCollider;
        private ICharacterInputProvider m_inputProvider;
        private float m_rotationVelocity;
        private State m_previousViewState;

        protected override void LateAwake()
        {
            m_stats = GetComponent<IStats>();
            m_rigidbody = GetComponent<PredictedRigidbody>();
            m_animation = GetComponentInChildren<CharacterAnimator>();
            m_capsuleCollider = GetComponent<CapsuleCollider>();
            m_inputProvider = GetComponent<ICharacterInputProvider>();

            m_rigidbody.rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            m_rigidbody.rigidbody.useGravity = false;
            SetCapsuleHeight(config.normalCharacterHeight);
        }

        protected override State GetInitialState()
        {
            return new State
            {
                lookDirection = m_body.forward
            };
        }

        protected override void UpdateInput(ref Input input)
        {
            m_inputProvider.UpdateInput(ref input);
        }

        protected override void Simulate(Input input, ref State state, float delta)
        {
            if (state.jumpCooldown > 0f)
                state.jumpCooldown -= delta;
            
            if (input.lookDirection.HasValue) 
                state.lookDirection = input.lookDirection.Value;
            
            var ray = new Ray(m_rigidbody.position + new Vector3(0f, config.groundCheckOffset, 0f), Vector3.down);

            if (Physics.SphereCast(ray, config.groundCheckRadius, out var hit, config.groundCheckDistance, config.groundMask) && state.jumpCooldown <= 0f)
            {
                state.isGrounded = true;
            }
            else
            {
                state.isGrounded = false;
            }

            if (state.isGrounded && input.jump && state.jumpCooldown <= 0f)
            {
                state.isGrounded = false;
                state.isJumping = true;
                state.jumpCooldown = 0.2f;
                var jumpForce = Mathf.Sqrt(config.jumpHeight * 2f * config.gravity);
                m_rigidbody.AddForce(new Vector3(0f, jumpForce, 0f), ForceMode.VelocityChange);
            }
            else
            {
                state.isJumping = false;
            }
            
            if (m_rigidbody.linearVelocity.y > -53f)
                m_rigidbody.AddForce(new Vector3(0f, -config.gravity, 0f), ForceMode.Acceleration);

            if (state.isSprinting)
            {
                if (input.moveDirection == Vector3.zero)
                {
                    state.stayInPlaceTime += delta;

                    if (state.stayInPlaceTime > 0.2f)
                        state.isSprinting = false;
                }
                else
                {
                    state.stayInPlaceTime = 0f;
                }
            }
            else if (state.stayInPlaceTime > 0f)
            {
                state.stayInPlaceTime = 0f;
            }

            if (input.toggleCrouch)
            {
                if (state.isCrouching)
                    StopCrouch(ref state);
                else
                    StartCrouch(ref state);
            }

            if (state.isCrouching && input.toggleSprint)
                StopCrouch(ref state);

            if (!state.isCrouching)
            {
                switch (config.sprintMode)
                {
                    case SprintMode.Hold:
                        state.isSprinting = input.holdSprint;
                        break;
                    case SprintMode.Toggle:
                        if (input.toggleSprint) state.isSprinting = !state.isSprinting;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            float speed;
            float acceleration;

            if (state.isGrounded)
            {
                if (state.isCrouching)
                    speed = config.crouchSpeed;
                else if (state.isSprinting)
                    speed = config.sprintSpeed;
                else
                    speed = config.moveSpeed;

                acceleration = config.groundAcceleration;
            }
            else
            {
                speed = config.sprintSpeed;
                acceleration = config.airAcceleration;
            }

            speed *= m_stats.Get(StatType.MoveSpeed).value;

            if (input.moveDirectionNullable.HasValue)
            {
                state.moveDirection = input.moveDirectionNullable.Value;
                state.animationMoveVelocity = input.moveDirectionNullable.Value * speed;
            }
            
            var targetVelocity = input.moveDirection * speed;
            var horizontalVelocity = new Vector3(m_rigidbody.linearVelocity.x, 0f, m_rigidbody.linearVelocity.z);
            var force = (targetVelocity - horizontalVelocity) * acceleration;
            m_rigidbody.AddForce(force, ForceMode.Acceleration);
        }

        protected override void UpdateView(State state, State? verified)
        {
            UpdateView(state, m_previousViewState, verified);
            m_previousViewState = state;
        }

        private void UpdateView(State state, State previous, State? verified)
        {
            if (state.moveDirection != Vector3.zero && state.isGrounded)
            {
                var inputDirection = state.moveDirection.normalized;
                var targetBodyRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg;
                m_body.localRotation = Quaternion.Euler(0f, Mathf.SmoothDampAngle(m_body.eulerAngles.y, targetBodyRotation, ref m_rotationVelocity, config.rotationSmoothTime), 0f);
            }

            if (state.isGrounded != previous.isGrounded)
            {
                if (state.isGrounded) m_animation.OnLand();
                m_animation.SetIsGrounded(state.isGrounded);
            }

            if (state.isJumping != previous.isJumping)
            {
                if (state.isJumping) m_animation.OnJump();
            }
            
            if (state.isCrouching != previous.isCrouching)
            {
                m_animation.SetIsCrouching(state.isCrouching);
            }
            
            m_animation.SetMoveVelocity(state.animationMoveVelocity);
            m_animation.SetLookAtDirection(state.lookDirection);
        }

        private void StartCrouch(ref State state)
        {
            state.isSprinting = false;
            state.isCrouching = true;
            SetCapsuleHeight(config.crouchedCharacterHeight);
        }

        private void StopCrouch(ref State state)
        {
            if (!CanUncrouch())
                return;

            state.isCrouching = false;
            SetCapsuleHeight(config.normalCharacterHeight);
        }

        private bool CanUncrouch()
        {
            return true;
        }

        private void SetCapsuleHeight(float height)
        {
            m_capsuleCollider.radius = config.characterRadius;
            m_capsuleCollider.center = new Vector3(0f, height / 2f, 0f);
            m_capsuleCollider.height = height;
        }

        private void OnDrawGizmos()
        {
            var origin = transform.position + new Vector3(0f, config.groundCheckOffset, 0f);
            var point = origin + new Vector3(0f, -config.groundCheckDistance, 0f);
            Gizmos.DrawWireSphere(origin, config.groundCheckRadius);
            Gizmos.DrawLine(origin, point);
            Gizmos.color = viewState.isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(point, config.groundCheckRadius);
        }

        public enum SprintMode
        {
            Hold,
            Toggle
        }

        public struct Input : IPredictedData
        {
            public Vector3? lookDirection;
            public Vector3? moveDirectionNullable;
            public Vector3 moveDirection;
            public bool holdSprint;
            public bool toggleSprint;
            public bool toggleCrouch;
            public bool jump;

            public void Dispose() { }
        }

        public struct State : IPredictedData<State>
        {
            public Vector3 lookDirection;
            public Vector3 moveDirection;
            public Vector3 animationMoveVelocity;
            public bool isSprinting;
            public bool isCrouching;
            public bool isGrounded;
            public float stayInPlaceTime;
            public bool isJumping;
            public float jumpCooldown;

            public void Dispose() { }
        }

        [Serializable]
        public class Config
        {
            public float moveSpeed = 4f;
            public float sprintSpeed = 8f;
            public float crouchSpeed = 3f;
            public float groundAcceleration = 8f;
            public float airAcceleration = 1f;
            public float jumpHeight = 1.5f;
            public float rotationSmoothTime = 0.16f;
            public float gravity = 15f;
            public float characterRadius = 0.4f;
            public float normalCharacterHeight = 1.8f;
            public float crouchedCharacterHeight = 1f;
            public float groundCheckRadius = 0.39f;
            public float groundCheckOffset = 0.5f;
            public float groundCheckDistance = 0.2f;
            public LayerMask groundMask = 1 << 0;
            public SprintMode sprintMode = SprintMode.Hold;
        }
    }
}