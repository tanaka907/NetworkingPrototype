using PurrNet.Prediction;

namespace NetworkingPrototype
{
    public class Player : PredictedIdentity<Player.Input, Player.State>
    {
        protected override void Simulate(Input input, ref State state, float delta) { }

        public struct Input : IPredictedData
        {
            public void Dispose() { }
        }

        public struct State : IPredictedData<State>
        {
            public void Dispose() { }
        }
    }
}