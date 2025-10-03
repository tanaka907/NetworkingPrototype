using PurrNet.Prediction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetworkingPrototype
{
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(Stats))]
    public class SpellCaster : PredictedIdentity<SpellCaster.Input, SpellCaster.State>
    {
        [SerializeField] private float m_ManaCost = 10f;

        private Character m_Controller;
        private Stats m_Stats;
        private PredictedEvent m_OnCastFailed;
        private PredictedEvent m_OnCastComplete;

        protected override void LateAwake()
        {
            m_Controller = GetComponent<Character>();
            m_Stats = GetComponent<Stats>();

            m_OnCastFailed = new PredictedEvent(predictionManager, this);
            m_OnCastComplete = new PredictedEvent(predictionManager, this);
            
            m_OnCastFailed.AddListener(OnCastFailed);
            m_OnCastComplete.AddListener(OnCastComplete);
        }

        protected override void Destroyed()
        {
            m_OnCastFailed.RemoveListener(OnCastFailed);
            m_OnCastComplete.RemoveListener(OnCastComplete);
        }

        protected override void UpdateInput(ref Input input)
        {
            var mouse = Mouse.current;
            
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                    input.castRotation = m_Controller.currentState.lookDirection;
            }
        }

        protected override void Simulate(Input input, ref State state, float delta)
        {
            if (input.castRotation.HasValue)
            {
                if (m_Stats.Get(StatType.Mana).finalValue < m_ManaCost)
                {
                    m_OnCastFailed.Invoke();
                    return;
                }
                
                m_Stats.Change(StatType.Mana, -m_ManaCost);
                
                m_OnCastComplete.Invoke();
            }
        }
        
        private void OnCastFailed()
        {
            Game.Log($"{owner} failed cast");
        }

        private void OnCastComplete()
        {
            Game.Log($"{owner} completed cast");
        }
        
        public struct Input : IPredictedData
        {
            public Vector3? castRotation;

            public void Dispose() { }
        }

        public struct State : IPredictedData<State>
        {
            public void Dispose() { }
        }
    }
}