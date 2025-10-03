using System;
using System.Linq;
using PurrNet.Pooling;
using PurrNet.Prediction;
using UnityEngine;
using UnityEngine.Assertions;

namespace NetworkingPrototype
{
    [Serializable]
    public struct StatConfig
    {
        public StatType type;
        public float value;
        public float regeneration;
    }

    public enum StatType
    {
        None,
        Health,
        Mana,
        MoveSpeed,
        Stamina
    }

    public struct Stat
    {
        public bool regeneration;
        public float finalValue;
        public float maxValue;
        public float normalized => maxValue == 0f ? 0f : value / maxValue;

        public float value
        {
            get => finalValue;
            set => finalValue = Mathf.Clamp(value, 0f, maxValue);
        }

        public bool IsEmpty() => value <= 0f;
        public bool IsFull() => value >= maxValue;

        public Stat(StatConfig config)
        {
            regeneration = config.regeneration > 0f;
            finalValue = config.value;
            maxValue = config.value;
        }
    }

    public class Stats : PredictedIdentity<Stats.State>
    {
        [SerializeField] private StatConfig[] m_Stats = Array.Empty<StatConfig>();

        public StatConfig[] configs => m_Stats;

        private GUIStyle m_LabelStyle;

        private void Start()
        {
            m_LabelStyle = new GUIStyle
            {
                fontSize = Game.FONT_SIZE,
                normal = { textColor = Game.FONT_COLOR },
                alignment = TextAnchor.LowerLeft
            };
        }

        protected override State GetInitialState()
        {
            return new State
            {
                stats = DisposableDictionary<StatType, Stat>.Create(m_Stats.ToDictionary(
                    config => config.type,
                    config => new Stat(config))
                )
            };
        }

        protected override void Simulate(ref State state, float delta)
        {
            for (var i = 0; i < configs.Length; i++)
            {
                if (configs[i].regeneration > 0f)
                {
                    var stat = state.stats[configs[i].type];

                    if (!stat.regeneration)
                        continue;

                    if (!stat.IsFull())
                    {
                        stat.value += configs[i].regeneration * delta;
                        state.stats[configs[i].type] = stat;
                    }
                }
            }
        }

        public Stat Get(StatType type)
        {
            if (predictionManager.isSimulating)
            {
                if (currentState.stats.TryGetValue(type, out var stat))
                    return stat;
            }
            else
            {
                if (viewState.stats.TryGetValue(type, out var stat))
                    return stat;
            }

            return default;
        }

        public float Change(StatType type, float value)
        {
            Assert.IsTrue(predictionManager.isSimulating);

            if (currentState.stats.TryGetValue(type, out var stat))
            {
                var oldValue = stat.value;
                stat.value += value;
                var delta = stat.value - oldValue;
                
                if (delta == 0) 
                    return stat.value;
                
                currentState.stats[type] = stat;
                return stat.value;
            }

            return 0f;
        }

        private void OnGUI()
        {
            if (!isOwner || viewState.stats.isDisposed)
                return;

            var rect = new Rect(Game.PADDING, Screen.height - Game.PADDING - 200f, 200f, 200f);
            GUI.Label(rect, viewState.ToString(), m_LabelStyle);
        }

        public struct State : IPredictedData<State>
        {
            public DisposableDictionary<StatType, Stat> stats;

            public void Dispose()
            {
                stats.Dispose();
            }

            public override string ToString()
            {
                var s = "";

                if (!stats.isDisposed)
                {
                    var isFirst = true;
                    foreach (var (type, stat) in stats)
                    {
                        if (!isFirst) s += "\n";
                        s += $"{type.ToString()}: {stat.value:0} / {stat.maxValue:0}";
                        isFirst = false;
                    }
                }
                else
                {
                    s += "stats: disposed";
                }

                return s;
            }
        }
    }
}