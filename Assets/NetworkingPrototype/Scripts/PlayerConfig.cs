using UnityEngine;

namespace NetworkingPrototype
{
    [CreateAssetMenu(menuName = "Networking Prototype/Player Config", fileName = "PlayerConfig")]
    public class PlayerConfig : ScriptableObject
    {
        public enum LookMode
        {
            Instant,
            Linear,
            Decay
        }
        
        public enum VelocityMode
        {
            Instant,
            Linear,
            Decay
        }

        public LookMode lookMode = LookMode.Decay;
        public VelocityMode velocityMode = VelocityMode.Decay;
        
        [Header("Movement")]
        public float walkSpeed = 4f;
        public float sprintSpeed = 8f;
        public float velocityDecay = 14f;
        public float acceleration = 14f;
        [Range(0f, 1f)]
        public float airSlowdown = 0.1f;

        [Header("Look")]
        public float lookSensitivity = 1f;
        public float lookAcceleration = 1f;
        public float lookDecay = 1f;

        [Header("Jump")]
        public LayerMask groundMask;
        public float groundCheckRadius = 0.3f;
        public float groundCheckOffset = 0.15f;
        public float jumpCooldown = 0.2f;
        public float jumpVelocity = 5f;

        [Header("Spells")]
        public float spellCooldown = 0.5f;
        public float projectileSpeed = 20f;
        public float projectileOffsetY = 1.65f;
        public float projectileOffsetZ = 0.3f;
        public GameObject projectilePrefab;
    }
}