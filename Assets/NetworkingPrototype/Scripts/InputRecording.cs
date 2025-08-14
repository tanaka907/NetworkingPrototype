using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace NetworkingPrototype
{
    [CreateAssetMenu]
    public class InputRecording : ScriptableObject
    {
        public enum Mode
        {
            Disabled,
            Record,
            Replay
        }

        public Mode mode;
        public List<Frame> frames = new();

        public void Clear()
        {
            frames.Clear();
        }

        public Frame GetFrame(int frameIndex)
        {
            Assert.IsTrue(frames.Count > 0);
            
            if (frameIndex >= frames.Count)
                frameIndex = 0;
            
            return frames[frameIndex];
        }

        public void AddFrame(Frame frame)
        {
            frames.Add(frame);
        }

        [Serializable]
        public struct Frame
        {
            public Vector2 look;
            public Vector2 move;
            public float deltaTime;
        }
    }
}