using UnityEngine;

namespace NetworkingPrototype
{
    [CreateAssetMenu(menuName = "Footstep Config", fileName = "FootstepConfig")]
    public class FootstepConfig : ScriptableObject
    {
        [Range(0, 1f)]
        public float volume = 1f;
        public AudioClip[] jump;
        public AudioClip[] land;
        public AudioClip[] walk;
        public AudioClip[] run;

        public void PlayJump(Vector3 position) => PlayClip(jump, position);
        public void PlayLand(Vector3 position) => PlayClip(land, position);
        public void PlayWalk(Vector3 position) => PlayClip(walk, position);
        public void PlayRun(Vector3 position) => PlayClip(run, position);

        private void PlayClip(AudioClip[] clips, Vector3 position)
        {
            if (clips == null || clips.Length == 0)
                return;

            var index = Random.Range(0, clips.Length);
            AudioSource.PlayClipAtPoint(clips[index], position, volume);
        }
    }
}