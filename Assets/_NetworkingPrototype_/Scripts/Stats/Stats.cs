using System;
using UnityEngine;

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

    public interface IStats
    {
        Stat Get(StatType type);
        float Change(StatType type, float value);
    }
}