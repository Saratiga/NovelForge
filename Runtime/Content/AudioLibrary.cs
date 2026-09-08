using System;
using UnityEngine;

namespace NovelForge.Runtime
{
    [CreateAssetMenu(menuName = "NovelForge/Audio Library", fileName = "AudioLibrary")]
    public class AudioLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string id;
            public AudioClip clip;
        }

        [SerializeField] internal Entry[] musicTracks = Array.Empty<Entry>();
        [SerializeField] internal Entry[] sfxClips = Array.Empty<Entry>();

        public bool TryGetMusicClip(string id, out AudioClip clip) => TryGet(musicTracks, id, out clip);

        public bool TryGetSfxClip(string id, out AudioClip clip) => TryGet(sfxClips, id, out clip);

        private static bool TryGet(Entry[] entries, string id, out AudioClip clip)
        {
            foreach (var entry in entries)
            {
                if (entry.id == id && entry.clip != null)
                {
                    clip = entry.clip;
                    return true;
                }
            }

            clip = null;
            return false;
        }
    }
}
