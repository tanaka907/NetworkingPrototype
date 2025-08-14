using UnityEngine;

namespace NetworkingPrototype
{
    [CreateAssetMenu]
    public class FirstPersonControllerConfig : ScriptableObject
    {
        [Header("Movement")]
        public float walkSpeed = 4f;
        public float sprintSpeed = 8f;
        public float velocityDecay = 14f;

        [Header("Look")]
        public float lookSensitivity = 1f;
        public float lookDecay = 1f;
    }
}