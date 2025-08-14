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

        protected override void UpdateInput(ref Input input)
        {
            input.jump |= Keyboard.current.spaceKey.wasPressedThisFrame;
            input.fire |= Mouse.current.leftButton.isPressed;
            input.sprint |= Keyboard.current.leftShiftKey.isPressed;

            _lookSpeed = MathUtility.Decay(
                _lookSpeed,
                Mouse.current.delta.ReadValue() * _config.lookSensitivity,
                _config.lookAcceleration,
                Time.deltaTime);

            _pitch -= _lookSpeed.y;
            _pitch = Mathf.Clamp(_pitch, -85f, 85f);
            _yaw += _lookSpeed.x;
        }

        protected override void SanitizeInput(ref Input input)
        {
            input.move.y = 0f;
            input.move = Vector3.ClampMagnitude(input.move, 1f);
        }

        protected override void GetFinalInput(ref Input input)
        {
            input.yaw = _yaw;
            input.pitch = _pitch;
            input.move = GetWorldMoveVector(Quaternion.AngleAxis(_yaw, Vector3.up));
        }

        protected override void Simulate(Input input, ref State state, float delta)
        {
            // Move
            var grounded = IsGrounded();
            var moveSpeed = input.sprint ? _config.sprintSpeed : _config.walkSpeed;
            var wantedVelocity = input.move * moveSpeed;
            var acceleration = wantedVelocity - _rigidbody.linearVelocity;
            acceleration.y = 0f;
            acceleration = Vector3.ClampMagnitude(acceleration, _config.acceleration);
            acceleration *= _config.accelerationBoost;
            if (!grounded) acceleration *= _config.airSlowdown;

            _rigidbody.AddForce(acceleration, ForceMode.Acceleration);

            // Jump
            if (input.jump && grounded && state.jumpCooldown <= 0f)
            {
                _rigidbody.AddForce(new Vector3(0f, _config.jumpVelocity, 0f), ForceMode.Impulse);
                state.jumpCooldown = _config.jumpCooldown;
            }
            else if (state.jumpCooldown > 0f)
            {
                state.jumpCooldown -= delta;

                if (state.jumpCooldown < 0f)
                    state.jumpCooldown = 0f;
            }

            // Look
            if (input.yaw != 0f && input.pitch != 0f) // why does this return zero sometimes?
            {
                state.yaw = input.yaw;
                state.pitch = input.pitch;
            }
            _rigidbody.rb.MoveRotation(Quaternion.AngleAxis(state.yaw, Vector3.up));

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
            _camera.localRotation = Quaternion.AngleAxis(state.pitch, Vector3.right);
        }

        private void SpawnProjectile(State state)
        {
            var rot = Quaternion.Euler(state.pitch, state.yaw, 0f);
            var pos = transform.position +
                      new Vector3(0f, _config.projectileOffsetY, 0f) +
                      rot * new Vector3(0f, 0f, _config.projectileOffsetZ);
            var projectileId = hierarchy.Create(_config.projectilePrefab, pos, transform.rotation);
            var rb = hierarchy.GetComponent<PredictedRigidbody>(projectileId);
            rb.linearVelocity = rot * new Vector3(0f, 0f, _config.projectileSpeed);
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
            public float yaw;
            public float pitch;
            public Vector3 move;
            public bool fire;
            public bool jump;
            public bool sprint;

            public void Dispose() { }

            public override string ToString()
            {
                return $"yaw: {yaw}\n" +
                       $"pitch: {pitch}\n" +
                       $"move: {move}\n" +
                       $"fire: {fire}\n" +
                       $"jump: {jump}\n" +
                       $"sprint: {sprint}";
            }
        }

        public struct State : IPredictedData<State>
        {
            public float yaw;
            public float pitch;
            public float spellCooldown;
            public float jumpCooldown;

            public void Dispose() { }

            public override string ToString()
            {
                return $"yaw: {yaw}\n" +
                       $"pitch: {pitch}\n" +
                       $"spellCooldown: {spellCooldown}\n" +
                       $"jumpCooldown: {jumpCooldown}";
            }
        }
    }
}