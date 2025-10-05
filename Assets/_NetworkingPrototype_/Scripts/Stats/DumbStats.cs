using System;
using PurrNet.Prediction;
using UnityEngine;

namespace NetworkingPrototype
{
    public class DumbStats : PredictedIdentity<DumbStats.State>, IStats
    {
        [SerializeField] private StatConfig[] m_Stats = Array.Empty<StatConfig>();

        public StatConfig[] configs => m_Stats;

        private GUIStyle m_LabelStyle;

        private void Start()
        {
            m_LabelStyle = new GUIStyle
            {
                fontSize = Game.config.fontSize,
                normal = { textColor = Game.config.fontColor },
                alignment = TextAnchor.LowerLeft
            };
        }

        protected override State GetInitialState()
        {
            return new State
            {
                health = new Stat(configs[0]),
                mana = new Stat(configs[1]),
                moveSpeed = new Stat(configs[2]),
                stamina = new Stat(configs[3])
            };
        }

        protected override void Simulate(ref State state, float delta)
        {
            if (state.health.regeneration) state.health.value += configs[0].regeneration * delta;
            if (state.mana.regeneration) state.mana.value += configs[1].regeneration * delta;
            if (state.moveSpeed.regeneration) state.moveSpeed.value += configs[2].regeneration * delta;
            if (state.stamina.regeneration) state.stamina.value += configs[3].regeneration * delta;
        }

        public Stat Get(StatType type)
        {
            return predictionManager.isSimulating
                ? Get(currentState, type)
                : Get(viewState, type);
        }

        public void Change(StatType type, float value)
        {
            switch (type)
            {
                case StatType.Health:
                    currentState.health.value += value;
                    break;
                case StatType.Mana:
                    currentState.mana.value += value;
                    break;
                case StatType.MoveSpeed:
                    currentState.moveSpeed.value += value;
                    break;
                case StatType.Stamina:
                    currentState.stamina.value += value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private Stat Get(State state, StatType type)
        {
            return type switch
            {
                StatType.Health => state.health,
                StatType.Mana => state.mana,
                StatType.MoveSpeed => state.moveSpeed,
                StatType.Stamina => state.stamina,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        private void OnGUI()
        {
            if (!isOwner)
                return;

            var rect = new Rect(
                Game.config.padding, 
                Screen.height - Game.config.padding - Game.config.labelWidth, 
                Game.config.labelWidth, 
                Game.config.labelWidth);
            
            GUI.Label(rect, viewState.ToString(), m_LabelStyle);
        }

        public struct State : IPredictedData<State>
        {
            public Stat health;
            public Stat mana;
            public Stat moveSpeed;
            public Stat stamina;

            public void Dispose() { }

            public override string ToString()
            {
                var s = "";

                s += $"health: {health.value:0} / {health.maxValue:0}\n";
                s += $"mana: {mana.value:0} / {mana.maxValue:0}\n";
                s += $"moveSpeed: {moveSpeed.value:0} / {moveSpeed.maxValue:0}\n";
                s += $"stamina: {stamina.value:0} / {stamina.maxValue:0}";

                return s;
            }
        }
    }
}