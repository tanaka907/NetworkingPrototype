using System;
using System.Collections.Generic;
using UnityEngine;

namespace NetworkingPrototype
{
    public struct OnScreenLog
    {
        public string message;
        public float logTime;
    }

    [Flags]
    public enum LogModeFlags
    {
        OnScreen = 1 << 0,
        ToConsole = 1 << 1
    }

    [Serializable]
    public class GameConfig
    {
        public LogModeFlags logMode;
        public float onScreenLogLifetime = 3f;
        public int fontSize = 13;
        public int padding = 10;
        public int labelWidth = 200;
        public Color fontColor = Color.white;
        public float labelHeight => fontSize * 1.25f;
    }
    
    public class Game : MonoBehaviour
    {
        public static GameConfig config { get; private set; }

        [SerializeField] private GameConfig m_config = new();
        
        private static GUIStyle s_labelStyle;
        private static List<OnScreenLog> s_logs;

        private void Awake()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            
            config = m_config;
            
            s_logs = new List<OnScreenLog>();
            
            s_labelStyle = new GUIStyle
            {
                fontSize = config.fontSize,
                normal = { textColor = config.fontColor },
                alignment = TextAnchor.MiddleLeft
            };
        }

        private void OnApplicationQuit()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            s_logs = null;
            s_labelStyle = null;
            config = null;
        }

        private void OnGUI()
        {
            var rect = new Rect(config.padding, config.padding, config.labelWidth, config.labelHeight);
            var validLogTime = Time.realtimeSinceStartup - config.onScreenLogLifetime;
            
            for (var i = 0; i < s_logs.Count;)
            {
                if (s_logs[i].logTime < validLogTime)
                {
                    s_logs.RemoveAt(i);
                    continue;
                }
                
                GUI.Label(rect, s_logs[i].message, s_labelStyle);
                rect.y += config.labelHeight;
                i++;
            }
        }

        public static void Log(string message)
        {
            if ((config.logMode & LogModeFlags.OnScreen) != 0)
            {
                s_logs.Add(new OnScreenLog
                {
                    message = message,
                    logTime = Time.realtimeSinceStartup
                });
            }
            
            if ((config.logMode & LogModeFlags.ToConsole) != 0)
                Debug.Log(message);
        }
    }
}