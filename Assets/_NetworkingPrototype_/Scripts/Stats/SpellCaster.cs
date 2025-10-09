using PurrNet.Prediction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetworkingPrototype
{
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(IStats))]
    public class SpellCaster : PredictedIdentity<SpellCaster.Input, SpellCaster.State>
    {
        [SerializeField] private bool m_log;
        [SerializeField] private float m_manaCost = 10f;

        private Character m_controller;
        private IStats m_stats;
        private PredictedEvent m_onCastFailed;
        private PredictedEvent m_onCastComplete;

        protected override void LateAwake()
        {
            m_controller = GetComponent<Character>();
            m_stats = GetComponent<IStats>();

            m_onCastFailed = new PredictedEvent(predictionManager, this);
            m_onCastComplete = new PredictedEvent(predictionManager, this);
            
            m_onCastFailed.AddListener(OnCastFailed);
            m_onCastComplete.AddListener(OnCastComplete);
        }

        protected override void Destroyed()
        {
            m_onCastFailed.RemoveListener(OnCastFailed);
            m_onCastComplete.RemoveListener(OnCastComplete);
        }

        protected override void UpdateInput(ref Input input)
        {
            var mouse = Mouse.current;
            
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                    input.castRotation = m_controller.currentState.lookDirection;
            }
        }

        protected override void Simulate(Input input, ref State state, float delta)
        {
            if (input.castRotation.HasValue)
            {
                if (m_stats.Get(StatType.Mana).value < m_manaCost)
                {
                    m_onCastFailed.Invoke();
                    return;
                }
                
                m_stats.Change(StatType.Mana, -m_manaCost);
                
                m_onCastComplete.Invoke();
            }
        }
        
        private void OnCastFailed()
        {
            if (m_log)
                Game.Log($"{owner} {tickModule.localTick} failed cast");
        }

        private void OnCastComplete()
        {
            if (m_log)
                Game.Log($"{owner} {tickModule.localTick} completed cast");
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