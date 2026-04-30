using UnityEngine;

namespace TableLaughs
{
    public enum SfxCue
    {
        Tap,
        Reveal,
        Vote,
        Score,
        Win
    }

    [RequireComponent(typeof(AudioSource))]
    public sealed class SoundHooks : MonoBehaviour
    {
        [SerializeField] private AudioClip tap;
        [SerializeField] private AudioClip reveal;
        [SerializeField] private AudioClip vote;
        [SerializeField] private AudioClip score;
        [SerializeField] private AudioClip win;

        private AudioSource audioSource;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        public void Play(SfxCue cue)
        {
            var clip = GetClip(cue);
            if (clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private AudioClip GetClip(SfxCue cue)
        {
            return cue switch
            {
                SfxCue.Tap => tap,
                SfxCue.Reveal => reveal,
                SfxCue.Vote => vote,
                SfxCue.Score => score,
                SfxCue.Win => win,
                _ => null
            };
        }
    }
}
