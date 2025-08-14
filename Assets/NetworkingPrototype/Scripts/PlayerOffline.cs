using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetworkingPrototype
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerOffline : MonoBehaviour
    {
        [SerializeField] private PlayerConfig _config;
        [SerializeField] private Transform _camera;
        [SerializeField] private Transform _graphics;

        private Rigidbody _rigidbody;
        private Input _input;
        private State _state;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            ReadInput(Time.deltaTime);
            Look();
        }

        private void FixedUpdate()
        {
            Move(Time.fixedDeltaTime);
        }

        private void ReadInput(float deltaTime)
        {
            var mouseMovement = Mouse.current.delta.ReadValue() * _config.lookSensitivity;

            switch (_config.lookMode)
            {
                case PlayerConfig.LookMode.Instant:
                    _input.lookSpeed = mouseMovement;
                    break;
                case PlayerConfig.LookMode.Decay:
                    _input.lookSpeed = MathUtility.Decay(
                        _input.lookSpeed,
                        mouseMovement,
                        _config.lookAcceleration,
                        deltaTime);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _input.move = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) _input.move.y += 1f;
            if (Keyboard.current.aKey.isPressed) _input.move.x -= 1f;
            if (Keyboard.current.sKey.isPressed) _input.move.y -= 1f;
            if (Keyboard.current.dKey.isPressed) _input.move.x += 1f;

            _input.sprint = Keyboard.current.leftShiftKey.isPressed;
        }

        private void Look()
        {
            _state.pitch -= _input.lookSpeed.y;
            _state.pitch = Mathf.Clamp(_state.pitch, -85f, 85f);
            _state.yaw += _input.lookSpeed.x;
            _graphics.localRotation = _state.YawRotation();
            _camera.localRotation = _state.PitchRotation();
        }

        private void Move(float deltaTime)
        {
            var worldInput = _state.YawRotation() * new Vector3(_input.move.x, 0f, _input.move.y);
            var speed = _input.sprint ? _config.sprintSpeed : _config.walkSpeed;
            var targetVelocity = worldInput * speed;
            switch (_config.velocityMode)
            {
                case PlayerConfig.VelocityMode.Instant:
                    _state.targetVelocity = targetVelocity;
                    break;
                case PlayerConfig.VelocityMode.Linear:
                    _state.targetVelocity = Vector3.MoveTowards(_state.targetVelocity, targetVelocity,
                        _config.acceleration * deltaTime);
                    break;
                case PlayerConfig.VelocityMode.Decay:
                    _state.targetVelocity = MathUtility.Decay(_state.targetVelocity, targetVelocity,
                        _config.velocityDecay, deltaTime);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var acceleration = _state.targetVelocity - _rigidbody.linearVelocity;
            acceleration.y = 0f;
            // _rigidbody.AddForce(acceleration, ForceMode.VelocityChange);
            _rigidbody.linearVelocity = _state.targetVelocity;
        }

        public struct Input
        {
            public Vector2 lookSpeed;
            public Vector2 move;
            public bool sprint;
        }

        public struct State
        {
            public float yaw;
            public float pitch;
            public Vector3 targetVelocity;

            public Quaternion YawRotation() => Quaternion.AngleAxis(yaw, Vector3.up);
            public Quaternion PitchRotation() => Quaternion.AngleAxis(pitch, Vector3.right);
            public Quaternion LookRotation() => Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}