using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintSpeed = 6.5f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float deceleration = 10f;
    [SerializeField] private float airControl = 0.5f;
    [SerializeField] private float gravity = -13f;
    [SerializeField] private float slopeLimit = 45f;

    private CharacterController controller;
    private Vector2 inputMove;
    private Vector3 velocity;   // Current player velocity
    private bool isSprinting;
    private bool isGrounded;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        HandleMovement(Time.deltaTime);
    }

    // Called by InputSystem (Player Input Component)
    public void OnMove(InputAction.CallbackContext context)
    {
        inputMove = context.ReadValue<Vector2>();
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        isSprinting = context.ReadValueAsButton();
    }

    private void HandleMovement(float deltaTime)
    {
        // 1. Ground check
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f; // small stick-to-ground force

        // 2. Get camera-relative input direction
        Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 camRight   = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        Vector3 inputDir   = (camForward * inputMove.y + camRight * inputMove.x).normalized;

        // 3. Target speed based on sprint/walk
        float targetSpeed = (isSprinting ? sprintSpeed : walkSpeed) * inputDir.magnitude;
        Vector3 targetVelocity = inputDir * targetSpeed;

        // 4. Apply slope adjustment
        if (isGrounded && OnSlope(out Vector3 slopeNormal))
        {
            targetVelocity = Vector3.ProjectOnPlane(targetVelocity, slopeNormal);
        }

        // 5. Smooth acceleration / deceleration
        float accelRate = isGrounded ? acceleration : acceleration * airControl;
        float decelRate = isGrounded ? deceleration : deceleration * airControl;
        
        if (inputDir.magnitude > 0.1f)
            velocity = velocity.xz(Vector3.MoveTowards(velocity.xz(), targetVelocity, accelRate * deltaTime));
        else
            velocity = velocity.xz(Vector3.MoveTowards(velocity.xz(), Vector3.zero, decelRate * deltaTime));

        // 6. Gravity
        velocity.y += gravity * deltaTime;

        // 7. Move the controller
        controller.Move(velocity * deltaTime);
    }

    private bool OnSlope(out Vector3 slopeNormal)
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, controller.height / 2f + 0.5f))
        {
            slopeNormal = hit.normal;
            float angle = Vector3.Angle(Vector3.up, slopeNormal);
            return angle > 0f && angle <= slopeLimit;
        }
        slopeNormal = Vector3.up;
        return false;
    }
}

// --- Helper Extensions ---
public static class VectorExtensions
{
    public static Vector3 xz(this Vector3 v) => new Vector3(v.x, 0f, v.z);

    public static Vector3 xz(this Vector3 v, Vector3 newXZ) => new Vector3(newXZ.x, v.y, newXZ.z);
}

