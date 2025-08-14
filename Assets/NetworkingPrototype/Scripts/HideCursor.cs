using UnityEngine;

namespace NetworkingPrototype
{
    public class HideCursor : MonoBehaviour
    {
        public int fps = 60;
        public int vSync = 1;

        private void Start()
        {
            QualitySettings.vSyncCount = vSync;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void OnDestroy()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void Update()
        {
            // if (QualitySettings.vSyncCount != vSync)
            //     QualitySettings.vSyncCount = vSync;
            //
            // if (Application.targetFrameRate != fps)
            //     Application.targetFrameRate = fps;
        }
    }
}