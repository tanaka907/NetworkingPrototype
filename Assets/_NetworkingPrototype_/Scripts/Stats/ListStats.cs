using System;
using System.Linq;
using PurrNet.Pooling;
using PurrNet.Prediction;
using UnityEngine;
using UnityEngine.Assertions;

namespace NetworkingPrototype
{
    public class ListStats : PredictedIdentity<ListStats.State>, IStats
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
                stats = DisposableList<Stat>.Create(m_Stats.Select(config => new Stat(config)))
            };
        }

        protected override void Simulate(ref State state, float delta)
        {
            for (var i = 0; i < configs.Length; i++)
            {
                if (configs[i].regeneration > 0f)
                {
                    var stat = state.stats[i];

                    if (!stat.regeneration)
                        continue;

                    if (!stat.IsFull())
                    {
                        stat.value += configs[i].regeneration * delta;
                        state.stats[i] = stat;
                    }
                }
            }
        }

        public Stat Get(StatType type)
        {
            return predictionManager.isSimulating 
                ? currentState.stats[(int)type] 
                : viewState.stats[(int)type];
        }

        public void Change(StatType type, float value)
        {
            Assert.IsTrue(predictionManager.isSimulating);
            var stat = currentState.stats[(int)type];
            stat.value += value;
            currentState.stats[(int)type] = stat;
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
            public DisposableList<Stat> stats;

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
                    for (var i = 0; i < stats.Count; i++)
                    {
                        var type = (StatType)i;
                        if (!isFirst) s += "\n";
                        s += $"{type.ToString()}: {stats[i].value:0} / {stats[i].maxValue:0}";
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