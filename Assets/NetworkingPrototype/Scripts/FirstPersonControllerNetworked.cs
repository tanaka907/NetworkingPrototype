using PurrNet.Prediction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetworkingPrototype
{
    [RequireComponent(typeof(PredictedRigidbody))]
    public class FirstPersonControllerNetworked : PredictedIdentity<FirstPersonControllerNetworked.Input, FirstPersonControllerNetworked.State>
    {
        [SerializeField] private FirstPersonControllerConfig _config;
        [SerializeField] private Transform _camera;

        private PredictedRigidbody _rigidbody;
        private LocalState _local;

        private void Awake()
        {
            _rigidbody = GetComponent<PredictedRigidbody>();
        }

        protected override void UpdateInput(ref Input input)
        {
            var deltaTime = Time.deltaTime;
            _local.lookSpeed = MathUtility.Decay(
                _local.lookSpeed,
                Mouse.current.delta.ReadValue() * _config.lookSensitivity,
                _config.lookDecay,
                deltaTime);

            _local.pitch -= _local.lookSpeed.y;
            _local.pitch = Mathf.Clamp(_local.pitch, -85f, 85f);
            _local.yaw += _local.lookSpeed.x;
        }

        protected override void GetFinalInput(ref Input input)
        {
            input.yaw = _local.yaw;
            input.pitch = _local.pitch;

            input.move = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) input.move.y += 1f;
            if (Keyboard.current.aKey.isPressed) input.move.x -= 1f;
            if (Keyboard.current.sKey.isPressed) input.move.y -= 1f;
            if (Keyboard.current.dKey.isPressed) input.move.x += 1f;

            input.sprint = Keyboard.current.leftShiftKey.isPressed;
        }

        protected override void ModifyExtrapolatedInput(ref Input input)
        {
            input.yaw = null;
            input.pitch = null;
        }

        protected override void Simulate(Input input, ref State state, float delta)
        {
            if (input.yaw.HasValue) state.yaw = input.yaw.Value;
            if (input.pitch.HasValue) state.pitch = input.pitch.Value;

            var worldInput = state.YawRotation() * new Vector3(input.move.x, 0f, input.move.y);
            var speed = input.sprint ? _config.sprintSpeed : _config.walkSpeed;
            var velocity = new Vector3(_rigidbody.linearVelocity.x, 0f, _rigidbody.linearVelocity.z);
            var targetVelocity = MathUtility.Decay(velocity, worldInput * speed, _config.velocityDecay, delta);
            var acceleration = targetVelocity - velocity;

            _rigidbody.linearVelocity += acceleration;
        }

        protected override void UpdateView(State state, State? verified)
        {
            _camera.localRotation = state.LookRotation();
        }

        public struct LocalState
        {
            public float yaw;
            public float pitch;
            public Vector2 lookSpeed;
        }

        public struct Input : IPredictedData
        {
            public float? yaw;
            public float? pitch;
            public Vector2 move;
            public bool sprint;

            public void Dispose() { }

            public override string ToString()
            {
                return $"yaw: {yaw}\n" +
                       $"pitch: {pitch}\n" +
                       $"move: {move}\n" +
                       $"sprint: {sprint}";
            }
        }

        public struct State : IPredictedData<State>
        {
            public float yaw;
            public float pitch;

            public Quaternion YawRotation() => Quaternion.AngleAxis(yaw, Vector3.up);
            public Quaternion PitchRotation() => Quaternion.AngleAxis(pitch, Vector3.right);
            public Quaternion LookRotation() => Quaternion.Euler(pitch, yaw, 0f);

            public void Dispose() { }

            public override string ToString()
            {
                return $"yaw: {yaw}\n" +
                       $"pitch: {pitch}";
            }
        }
    }
}