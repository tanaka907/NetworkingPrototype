using PurrNet.Prediction;
using UnityEngine;

namespace NetworkingPrototype
{
    public class Projectile : PredictedIdentity<Projectile.State>
    {
        [SerializeField] private float _lifetime = 5f;

        protected override State GetInitialState()
        {
            return new State
            {
                lifetime = _lifetime
            };
        }

        protected override void Simulate(ref State state, float delta)
        {
            state.lifetime -= delta;
            
            if (state.lifetime <= 0f)
                hierarchy.Delete(id.objectId);
        }

        public struct State : IPredictedData<State>
        {
            public float lifetime;

            public void Dispose() { }
        }
    }
}