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
        [Tooltip("Starting value for the stat")]
        public float value;
        [Tooltip("Regeneration per second")]
        [Min(0f)]
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
        public float baseValue;
        public float maxValue;
        public float finalValue;
        public float normalized => maxValue == 0f ? 0f : finalValue / maxValue;

        public bool IsEmpty() => finalValue <= 0f;
        public bool IsFull() => finalValue >= maxValue;

        public Stat(StatConfig config)
        {
            regeneration = config.regeneration > 0f;
            baseValue = config.value;
            maxValue = config.value;
            finalValue = config.value;
        }
    }

    public struct StatChangedEvent
    {
        public StatType type;
        public float delta;
        public float value;
        public float max;
        public float normalized;
    }

    public class Stats : PredictedIdentity<Stats.State>
    {
        [SerializeField] private StatConfig[] _stats = Array.Empty<StatConfig>();

        public StatConfig[] configs => _stats;

        public PredictedEvent<StatChangedEvent> onStatChange;

        protected override void LateAwake()
        {
            onStatChange = new PredictedEvent<StatChangedEvent>(predictionManager, this);
        }

        protected override State GetInitialState()
        {
            return new State
            {
                stats = DisposableDictionary<StatType, Stat>.Create(_stats.ToDictionary(
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
                        stat.baseValue = Mathf.Clamp(stat.baseValue + configs[i].regeneration * delta, 0f, stat.maxValue);
                        Reevaluate(ref stat);
                        currentState.stats[configs[i].type] = stat;
                    }
                }
            }
        }

        public void SetRegeneration(StatType type, bool regenerate)
        {
            Assert.IsTrue(predictionManager.isSimulating);

            if (currentState.stats.TryGetValue(type, out var stat))
            {
                if (stat.regeneration == regenerate)
                    return;

                stat.regeneration = regenerate;
                currentState.stats[type] = stat;
            }
        }

        public void SetRegeneration(StatType type, float regeneration)
        {
            Assert.IsTrue(predictionManager.isSimulating);

            for (var i = 0; i < configs.Length; i++)
            {
                if (configs[i].type == type)
                {
                    configs[i].regeneration = regeneration;
                    break;
                }
            }

            SetRegeneration(type, regeneration != 0f);
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

        public void Set(StatType type, float value)
        {
            Assert.IsTrue(predictionManager.isSimulating);
            Assert.IsTrue(value >= 0f);

            if (currentState.stats.TryGetValue(type, out var stat))
            {
                stat.baseValue = value;
                Reevaluate(ref stat);
                currentState.stats[type] = stat;
            }
        }

        public float Change(StatType type, float value)
        {
            Assert.IsTrue(predictionManager.isSimulating);

            if (currentState.stats.TryGetValue(type, out var stat))
            {
                var oldValue = stat.baseValue;
                stat.baseValue = Mathf.Clamp(stat.baseValue + value, 0f, stat.maxValue);
                var delta = stat.baseValue - oldValue;

                if (delta == 0)
                    return stat.finalValue;

                Reevaluate(ref stat);
                currentState.stats[type] = stat;

                onStatChange.Invoke(new StatChangedEvent
                {
                    type = type,
                    delta = delta,
                    value = stat.finalValue,
                    max = stat.maxValue,
                    normalized = stat.normalized
                });

                return stat.finalValue;
            }

            return 0f;
        }

        public void ResetValues()
        {
            currentState.stats.Dispose();
            currentState.stats = DisposableDictionary<StatType, Stat>.Create(_stats.ToDictionary(config => config.type, config => new Stat(config)));
        }

        private void Reevaluate(ref Stat stat)
        {
            stat.finalValue = stat.baseValue;
            // Accumulate modifiers
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
                    s += "stats:\n";

                    foreach (var (type, stat) in stats)
                        s += $"   {((StatType)type).ToString()}: {stat.finalValue}/{stat.maxValue}\n";
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