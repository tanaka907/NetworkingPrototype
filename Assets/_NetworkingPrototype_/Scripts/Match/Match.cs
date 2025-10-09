using System;
using PurrNet;

namespace NetworkingPrototype
{
    [Serializable]
    public struct PlayerStats
    {
        public StringUTF8 name;
        public float timer;
    }

    public class Match : NetworkBehaviour
    {
        public SyncList<PlayerStats> players = new();
    }
}