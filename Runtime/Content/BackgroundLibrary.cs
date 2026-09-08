using System;
using UnityEngine;

namespace NovelForge.Runtime
{
    [CreateAssetMenu(menuName = "NovelForge/Background Library", fileName = "BackgroundLibrary")]
    public class BackgroundLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string id;
            public Sprite sprite;
        }

        [SerializeField] internal Entry[] backgrounds = Array.Empty<Entry>();
        [SerializeField] internal Entry[] cgs = Array.Empty<Entry>();

        public bool TryGetBackgroundSprite(string id, out Sprite sprite) => TryGet(backgrounds, id, out sprite);

        public bool TryGetCgSprite(string id, out Sprite sprite) => TryGet(cgs, id, out sprite);

        private static bool TryGet(Entry[] entries, string id, out Sprite sprite)
        {
            foreach (var entry in entries)
            {
                if (entry.id == id && entry.sprite != null)
                {
                    sprite = entry.sprite;
                    return true;
                }
            }

            sprite = null;
            return false;
        }
    }
}
