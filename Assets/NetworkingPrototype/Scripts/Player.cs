using PurrNet.Prediction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetworkingPrototype
{
    [RequireComponent(typeof(PredictedRigidbody))]
    public class Player : PredictedIdentity<Player.Input, Player.State>
    {
        private static readonly Collider[] COLLIDER_BUFFER = new Collider[16];

        [SerializeField] private PlayerConfig _config;
        [SerializeField] private Transform _camera;

        private PredictedRigidbody _rigidbody;
        private float _yaw;
        private float _pitch;
        private Vector2 _lookSpeed;

        protected override void LateAwake()
        {
            _rigidbody = GetComponent<PredictedRigidbody>();
            _camera.gameObject.SetActive(isOwner);
        }

        protected override void Update()
        {
            _lookSpeed = MathUtility.Decay(
                _lookSpeed,
                Mouse.current.delta.ReadValue() * _config.lookSensitivity,
                _config.lookDecay,
                Time.deltaTime);

            _pitch -= _lookSpeed.y;
            _pitch = Mathf.Clamp(_pitch, -85f, 85f);
            _yaw += _lookSpeed.x;
            
            base.Update();
        }

        protected override void UpdateInput(ref Input input)
        {
            input.jump |= Keyboard.current.spaceKey.wasPressedThisFrame;
            input.fire |= Mouse.current.leftButton.isPressed;
            input.sprint |= Keyboard.current.leftShiftKey.isPressed;
        }

        protected override void SanitizeInput(ref Input input)
        {
            input.move.y = 0f;
            input.move = Vector3.ClampMagnitude(input.move, 1f);
        }

        protected override void GetFinalInput(ref Input input)
        {
            input.lookRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            input.move = GetWorldMoveVector(Quaternion.AngleAxis(_yaw, Vector3.up));
        }

        protected override void ModifyExtrapolatedInput(ref Input input)
        {
            input.lookRotation = null;
        }

        protected override void Simulate(Input input, ref State state, float delta)
        {
            // Move
            var grounded = IsGrounded();
            var moveSpeed = input.sprint ? _config.sprintSpeed : _config.walkSpeed;
            var wantedVelocity = input.move * moveSpeed;
            state.targetVelocity = MathUtility.Decay(state.targetVelocity, wantedVelocity, _config.velocityDecay, delta);
            var acceleration = state.targetVelocity - _rigidbody.linearVelocity;
            acceleration.y = 0f;
            
            if (!grounded) acceleration *= _config.airSlowdown;

            _rigidbody.AddForce(acceleration, ForceMode.VelocityChange);

            // Jump
            if (input.jump && grounded && state.jumpCooldown <= 0f)
            {
                _rigidbody.AddForce(new Vector3(0f, _config.jumpVelocity, 0f), ForceMode.VelocityChange);
                state.jumpCooldown = _config.jumpCooldown;
            }
            else if (state.jumpCooldown > 0f)
            {
                state.jumpCooldown -= delta;

                if (state.jumpCooldown < 0f)
                    state.jumpCooldown = 0f;
            }

            // Look
            if (input.lookRotation.HasValue)
                state.lookRotation = input.lookRotation.Value;

            // Spells
            if (input.fire && state.spellCooldown <= 0f)
            {
                SpawnProjectile(state);
                state.spellCooldown = _config.spellCooldown;
            }
            else if (state.spellCooldown > 0f)
            {
                state.spellCooldown -= delta;

                if (state.spellCooldown < 0f)
                    state.spellCooldown = 0f;
            }
        }

        protected override void UpdateView(State state, State? verified)
        {
            _camera.localRotation = state.lookRotation;
        }

        private void SpawnProjectile(State state)
        {
            var pos = transform.position +
                      new Vector3(0f, _config.projectileOffsetY, 0f) +
                      state.lookRotation * new Vector3(0f, 0f, _config.projectileOffsetZ);
            var projectileId = hierarchy.Create(_config.projectilePrefab, pos, transform.rotation);
            var rb = hierarchy.GetComponent<PredictedRigidbody>(projectileId);
            rb.linearVelocity = state.lookRotation * new Vector3(0f, 0f, _config.projectileSpeed);
        }

        private Vector3 GetWorldMoveVector(Quaternion cameraRotation)
        {
            var input = Vector2.zero;

            var keyboard = Keyboard.current;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;

            return cameraRotation * new Vector3(input.x, 0f, input.y);
        }

        private bool IsGrounded()
        {
            return Physics.OverlapSphereNonAlloc(
                GetGroundCheckOrigin(),
                _config.groundCheckRadius,
                COLLIDER_BUFFER,
                _config.groundMask) > 0;
        }

        private Vector3 GetGroundCheckOrigin()
        {
            return transform.position + new Vector3(0f, _config.groundCheckOffset, 0f);
        }

        private void OnDrawGizmos()
        {
            if (_config == null)
                return;

            Gizmos.color = IsGrounded() ? Color.green : Color.red;
            Gizmos.DrawWireSphere(GetGroundCheckOrigin(), _config.groundCheckRadius);
        }

        public struct Input : IPredictedData
        {
            public Quaternion? lookRotation;
            public Vector3 move;
            public bool fire;
            public bool jump;
            public bool sprint;

            public void Dispose() { }

            public override string ToString()
            {
                return $"lookRotation: {lookRotation}\n" +
                       $"move: {move}\n" +
                       $"fire: {fire}\n" +
                       $"jump: {jump}\n" +
                       $"sprint: {sprint}";
            }
        }

        public struct State : IPredictedData<State>
        {
            public Quaternion lookRotation;
            public float spellCooldown;
            public float jumpCooldown;
            public Vector3 targetVelocity;

            public void Dispose() { }

            public override string ToString()
            {
                return $"lookRotation: {lookRotation}\n" +
                       $"spellCooldown: {spellCooldown}\n" +
                       $"jumpCooldown: {jumpCooldown}\n" +
                       $"targetVelocity: {targetVelocity}";
            }
        }
    }
}