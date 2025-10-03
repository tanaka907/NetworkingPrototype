using System.Collections.Generic;
using UnityEngine;

namespace NetworkingPrototype
{
    public struct OnScreenLog
    {
        public string message;
        public float logTime;
    }
    
    public class Game : MonoBehaviour
    {
        public const int FONT_SIZE = 13;
        public const float PADDING = 10f; 
        public const float LABEL_HEIGHT = FONT_SIZE * 1.25f;
        public static readonly Color FONT_COLOR = Color.white;
        
        private static GUIStyle s_LabelStyle;
        private static List<OnScreenLog> s_Logs;

        private void Start()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            s_Logs = new List<OnScreenLog>();
            
            s_LabelStyle = new GUIStyle
            {
                fontSize = FONT_SIZE,
                normal = { textColor = FONT_COLOR },
                alignment = TextAnchor.MiddleLeft
            };
        }

        private void OnDestroy()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            s_Logs = null;
            s_LabelStyle = null;
        }

        private void OnGUI()
        {
            var rect = new Rect(PADDING, PADDING, 200f, LABEL_HEIGHT);
            var validLogTime = Time.realtimeSinceStartup - 3f;
            
            for (var i = 0; i < s_Logs.Count;)
            {
                if (s_Logs[i].logTime < validLogTime)
                {
                    s_Logs.RemoveAt(i);
                    continue;
                }
                
                GUI.Label(rect, s_Logs[i].message, s_LabelStyle);
                rect.y += LABEL_HEIGHT;
                i++;
            }
        }

        public static void Log(string message)
        {
            s_Logs.Add(new OnScreenLog
            {
                message = message,
                logTime = Time.realtimeSinceStartup
            });
        }
    }
}