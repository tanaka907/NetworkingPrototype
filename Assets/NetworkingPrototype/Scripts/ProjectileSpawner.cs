using PurrNet.Logging;
using PurrNet.Prediction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetworkingPrototype
{
    /// <summary>
    /// This is a slightly modified version of the <see cref="PurrNet.Prediction.Prebuilt.RigidbodyShooter"/>
    /// to use InputSystem and camera rotation for direction.
    /// </summary>
    public class ProjectileSpawner : PredictedIdentity<ProjectileSpawner.ShootInput, ProjectileSpawner.ShootData>
    {
        [SerializeField] private new Transform camera;
        [SerializeField] private GameObject projectile;
        [SerializeField] private float shootCooldown = 0.5f;
        [SerializeField] private Vector3 spawnOffset = Vector3.forward;
        [SerializeField] private float projectileInitialVelocity = 10;

#if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;
#endif

        protected override ShootData GetInitialState()
        {
            var state = new ShootData()
            {
                timeSinceShot = shootCooldown
            };
            return state;
        }

        protected override void Simulate(ShootInput input, ref ShootData state, float delta)
        {
            state.timeSinceShot += delta;
            if (input.shoot && state.timeSinceShot >= shootCooldown && input.direction.HasValue)
            {
                Shoot(input.direction.Value);
                state.timeSinceShot = 0;
            }
        }

        private void Shoot(Vector3 direction)
        {
            if (!projectile)
                return;

            var pos = transform.TransformPoint(new Vector3(0f, 1f, 0f));
            var projectileId = hierarchy.Create(projectile.gameObject, pos, transform.rotation);
            var projectileRb = hierarchy.GetComponent<Rigidbody>(projectileId);
            if(projectileRb)
                projectileRb.linearVelocity = direction * projectileInitialVelocity;
            else
                PurrLogger.LogError($"Failed to get Rigidbody component from projectile ({projectile.gameObject.name})", projectile);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
                return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.TransformPoint(spawnOffset), 0.2f);
        }
#endif

        protected override void GetFinalInput(ref ShootInput input)
        {
            input.shoot = Mouse.current.leftButton.isPressed;
            input.direction = camera.forward;
        }

        protected override void ModifyExtrapolatedInput(ref ShootInput input)
        {
            input.direction = null;
        }

        public struct ShootData : IPredictedData<ShootData>
        {
            public float timeSinceShot;

            public void Dispose() { }
        }

        public struct ShootInput : IPredictedData
        {
            public Vector3? direction;
            public bool shoot;

            public void Dispose() { }
        }
    }
}
