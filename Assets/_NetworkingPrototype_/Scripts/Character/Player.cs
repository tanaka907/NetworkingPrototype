using PurrNet;
using PurrNet.Prediction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetworkingPrototype
{
    public class Player : StatelessPredictedIdentity, ICharacterInputProvider
    {
        [SerializeField] private CharacterCamera m_Camera;
        [SerializeField] private Character m_Character;

        public override void OnViewOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner)
        {
            if (isOwner)
            {
                m_Camera.transform.parent = null;
                m_Camera.gameObject.SetActive(true);
            }
            else
            {
                m_Camera.transform.parent = transform;
                m_Camera.gameObject.SetActive(false);
            }
        }

        public void UpdateInput(ref Character.Input input)
        {
            UpdateCameraInput(ref m_Camera.input);
            m_Camera.UpdateLook(Time.deltaTime);
            UpdateCharacterInput(ref input);
        }

        private void LateUpdate()
        {
            m_Camera.UpdateObstruction(Time.deltaTime);
        }

        private void UpdateCameraInput(ref CharacterCamera.Input input)
        {
            var mouse = Mouse.current;

            if (mouse != null)
            {
                input.look = mouse.delta.ReadValue();
                input.zoom = mouse.scroll.ReadValue().y;
            }
        }

        private void UpdateCharacterInput(ref Character.Input input)
        {
            var keyboard = Keyboard.current;

            if (keyboard != null)
            {
                var moveInput = Vector2.zero;

                if (keyboard.wKey.isPressed) moveInput.y += 1f;
                if (keyboard.aKey.isPressed) moveInput.x -= 1f;
                if (keyboard.sKey.isPressed) moveInput.y -= 1f;
                if (keyboard.dKey.isPressed) moveInput.x += 1f;

                moveInput = Vector2.ClampMagnitude(moveInput, 1f);

                input.toggleCrouch |= keyboard.leftCtrlKey.wasPressedThisFrame;
                input.toggleSprint |= keyboard.leftShiftKey.wasPressedThisFrame;
                input.holdSprint = keyboard.leftShiftKey.isPressed;
                input.jump |= keyboard.spaceKey.wasPressedThisFrame;

                input.lookDirection = m_Camera.state.LookRotation() * Vector3.forward;
                input.moveDirection = m_Camera.state.YawRotation() * new Vector3(moveInput.x, 0f, moveInput.y);
                input.moveDirectionNullable = m_Camera.state.YawRotation() * new Vector3(moveInput.x, 0f, moveInput.y);
            }
        }
    }
}