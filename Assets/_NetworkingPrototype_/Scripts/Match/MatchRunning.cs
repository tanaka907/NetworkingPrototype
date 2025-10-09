using PurrNet.StateMachine;
using UnityEngine;

namespace NetworkingPrototype
{
    public class MatchRunning : StateNode
    {
        public Match match;

        public override void Enter(bool asServer)
        {
            if (asServer)
            {
                match.players.Add(new PlayerStats { name = "Player 1", });
                match.players.Add(new PlayerStats { name = "Player 2", });
            }
        }

        public override void StateUpdate(bool asServer)
        {
            if (asServer)
            {
                for (var i = 0; i < match.players.Count; i++)
                {
                    var player = match.players[i];
                    player.timer += Time.deltaTime;
                    match.players[i] = player;
                }
            }
        }

        public override void Exit(bool asServer)
        {
            if (asServer)
            {
                match.players.Clear();
            }
        }
    }
}