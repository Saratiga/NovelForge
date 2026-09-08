using System.Collections;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class AudioPresenter : MonoBehaviour, IAudioPresenter
    {
        [SerializeField] internal AudioLibrary library;
        [SerializeField] internal AudioSource musicSourceA;
        [SerializeField] internal AudioSource musicSourceB;
        [SerializeField] internal AudioSource sfxSource;
        [SerializeField] internal float musicCrossfadeSeconds = 1f;

        internal IDeltaTimeSource TimeSource = new UnityDeltaTimeSource();

        private AudioSource _activeMusicSource;

        public IEnumerator PlayMusic(string trackId)
        {
            if (library == null || !library.TryGetMusicClip(trackId, out var clip))
            {
                Debug.LogError($"NovelForge: no music clip registered for id '{trackId}' — skipping.");
                yield break;
            }

            if (musicSourceA == null || musicSourceB == null)
            {
                Debug.LogError("NovelForge: AudioPresenter is missing musicSourceA/musicSourceB — skipping music.");
                yield break;
            }

            _activeMusicSource ??= musicSourceA;
            var from = _activeMusicSource;
            var to = _activeMusicSource == musicSourceA ? musicSourceB : musicSourceA;
            to.clip = clip;
            to.volume = 0f;
            to.Play();

            float t = 0f;
            while (t < musicCrossfadeSeconds)
            {
                t += TimeSource.DeltaTime;
                float ratio = Mathf.Clamp01(t / musicCrossfadeSeconds);
                to.volume = ratio;
                from.volume = 1f - ratio;
                yield return null;
            }

            to.volume = 1f;
            from.Stop();
            from.volume = 1f;
            _activeMusicSource = to;
        }

        public IEnumerator PlaySfx(string clipId)
        {
            if (library == null || !library.TryGetSfxClip(clipId, out var clip))
            {
                Debug.LogError($"NovelForge: no sfx clip registered for id '{clipId}' — skipping.");
                yield break;
            }

            if (sfxSource == null)
            {
                Debug.LogError("NovelForge: AudioPresenter is missing sfxSource — skipping sfx.");
                yield break;
            }

            sfxSource.clip = clip;
            sfxSource.Play();

            float elapsed = 0f;
            while (elapsed < clip.length)
            {
                elapsed += TimeSource.DeltaTime;
                yield return null;
            }
        }
    }
}
