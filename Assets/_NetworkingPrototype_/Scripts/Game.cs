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
        private const int FONT_SIZE = 13;

        private static List<OnScreenLog> s_Logs;
        private static GUIStyle s_LabelStyle;

        private void Start()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            s_Logs = new List<OnScreenLog>();
            
            s_LabelStyle = new GUIStyle
            {
                fontSize = FONT_SIZE,
                normal = { textColor = Color.white },
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
            var lineHeight = FONT_SIZE * 1.25f;
            var rect = new Rect(10f, 10f, 200f, lineHeight);
            var validLogTime = Time.realtimeSinceStartup - 3f;
            
            for (var i = 0; i < s_Logs.Count;)
            {
                if (s_Logs[i].logTime < validLogTime)
                {
                    s_Logs.RemoveAt(i);
                    continue;
                }
                
                GUI.Label(rect, s_Logs[i].message, s_LabelStyle);
                rect.y += lineHeight;
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